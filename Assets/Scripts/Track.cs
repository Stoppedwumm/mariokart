using System.Collections.Generic;
using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Procedurally generated closed-loop circuit. The centre line is a Catmull-Rom spline through
    /// <see cref="Layout"/>, resampled at an even spacing. Those samples double as the AI racing line
    /// and the reference used to measure every kart's race progress.
    /// </summary>
    public class Track : MonoBehaviour
    {
        public const float RoadHalfWidth = 9f;   // asphalt + 1 m kerb on each side
        public const float WallOffset = 15f;     // distance from the centre line to the barriers
        public const float SampleSpacing = 2f;

        static readonly Vector2[] Layout =
        {
            new Vector2(0, -120), new Vector2(0, 60), new Vector2(20, 120), new Vector2(80, 140),
            new Vector2(130, 110), new Vector2(140, 50), new Vector2(110, 0), new Vector2(120, -50),
            new Vector2(180, -70), new Vector2(230, -30), new Vector2(250, 40), new Vector2(290, 90),
            new Vector2(340, 70), new Vector2(350, -40), new Vector2(320, -130), new Vector2(250, -170),
            new Vector2(150, -160), new Vector2(60, -190), new Vector2(10, -170),
        };

        static readonly Vector3 StartLinePosition = new Vector3(0f, 0f, -40f);

        public Vector3[] Points { get; private set; }
        public Vector3[] Tangents { get; private set; }
        public float[] Distances { get; private set; }
        public float Length { get; private set; }
        public int Count => Points.Length;

        public void Build(int seed)
        {
            Sample();
            BuildGround();
            BuildRoad();
            BuildWalls();
            BuildStartLine();
            BuildScenery(seed);
        }

        // ------------------------------------------------------------------ queries

        public int Wrap(int i)
        {
            int n = Count;
            return ((i % n) + n) % n;
        }

        public Vector3 Right(int i) => Vector3.Cross(Vector3.up, Tangents[Wrap(i)]).normalized;

        /// <summary>Nearest centre-line sample. With a hint, only a window around it is searched.</summary>
        public int FindNearest(Vector3 pos, int hint = -1, int window = 30)
        {
            int best = 0;
            float bestSq = float.MaxValue;
            if (hint < 0)
            {
                for (int i = 0; i < Count; i++) Check(i);
            }
            else
            {
                for (int o = -window; o <= window; o++) Check(Wrap(hint + o));
            }
            return best;

            void Check(int i)
            {
                Vector3 d = Points[i] - pos;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
        }

        /// <summary>Signed lateral distance from the centre line (positive = right).</summary>
        public float LateralOffset(Vector3 pos, int index)
        {
            Vector3 d = pos - Points[Wrap(index)];
            d.y = 0f;
            return Vector3.Dot(d, Right(index));
        }

        /// <summary>Sample index that lies <paramref name="metres"/> along the track from <paramref name="index"/>.</summary>
        public int Advance(int index, float metres) => Wrap(index + Mathf.RoundToInt(metres / SampleSpacing));

        // ------------------------------------------------------------------ generation

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void Sample()
        {
            int n = Layout.Length;
            var ctrl = new Vector3[n];
            for (int i = 0; i < n; i++) ctrl[i] = new Vector3(Layout[i].x, 0f, Layout[i].y);

            var dense = new List<Vector3>();
            const int sub = 24;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = ctrl[(i - 1 + n) % n], p1 = ctrl[i], p2 = ctrl[(i + 1) % n], p3 = ctrl[(i + 2) % n];
                for (int s = 0; s < sub; s++) dense.Add(CatmullRom(p0, p1, p2, p3, s / (float)sub));
            }

            // Resample the closed polyline at an even spacing.
            var pts = new List<Vector3> { dense[0] };
            float need = SampleSpacing;
            int m = dense.Count;
            for (int j = 0; j < m; j++)
            {
                Vector3 a = dense[j], b = dense[(j + 1) % m];
                float seg = Vector3.Distance(a, b), pos = 0f;
                while (seg - pos >= need)
                {
                    pos += need;
                    pts.Add(Vector3.Lerp(a, b, pos / seg));
                    need = SampleSpacing;
                }
                need -= seg - pos;
            }
            if (Vector3.Distance(pts[pts.Count - 1], pts[0]) < SampleSpacing * 0.5f) pts.RemoveAt(pts.Count - 1);

            // Rotate the samples so index 0 sits on the start/finish line.
            int start = 0;
            float best = float.MaxValue;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - StartLinePosition).sqrMagnitude;
                if (d < best) { best = d; start = i; }
            }

            int c = pts.Count;
            Points = new Vector3[c];
            Tangents = new Vector3[c];
            Distances = new float[c];
            for (int i = 0; i < c; i++) Points[i] = pts[(i + start) % c];
            for (int i = 0; i < c; i++)
                Tangents[i] = (Points[(i + 1) % c] - Points[(i - 1 + c) % c]).normalized;
            float total = 0f;
            for (int i = 0; i < c; i++)
            {
                Distances[i] = total;
                total += Vector3.Distance(Points[i], Points[(i + 1) % c]);
            }
            Length = total;
        }

        Bounds TrackBounds()
        {
            var b = new Bounds(Points[0], Vector3.zero);
            foreach (Vector3 p in Points) b.Encapsulate(p);
            return b;
        }

        void BuildGround()
        {
            Bounds b = TrackBounds();
            Vector3 size = b.size + new Vector3(400f, 0f, 400f);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.position = new Vector3(b.center.x, 0f, b.center.z);
            ground.transform.localScale = new Vector3(size.x / 10f, 1f, size.z / 10f);
            ground.GetComponent<Renderer>().sharedMaterial =
                Factory.Mat(Factory.GrassTexture(), new Vector2(size.x / 16f, size.z / 16f));
            ground.AddComponent<TrackSurface>().isRoad = false;
        }

        void BuildRoad()
        {
            int c = Count;
            var verts = new Vector3[(c + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[c * 6];
            Vector3 lift = Vector3.up * 0.02f;

            for (int i = 0; i <= c; i++)
            {
                int k = i % c;
                Vector3 r = Right(k);
                float v = (i == c ? Length : Distances[k]) / 8f;
                verts[i * 2] = Points[k] - r * RoadHalfWidth + lift;
                verts[i * 2 + 1] = Points[k] + r * RoadHalfWidth + lift;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }
            for (int i = 0; i < c; i++)
            {
                int a = i * 2, t = i * 6;
                tris[t] = a; tris[t + 1] = a + 2; tris[t + 2] = a + 1;
                tris[t + 3] = a + 1; tris[t + 4] = a + 2; tris[t + 5] = a + 3;
            }

            var mesh = new Mesh { name = "Road", vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var road = new GameObject("Road");
            road.transform.SetParent(transform, false);
            road.AddComponent<MeshFilter>().sharedMesh = mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = Factory.Mat(Factory.RoadTexture(), Vector2.one);
            road.AddComponent<MeshCollider>().sharedMesh = mesh;
            road.AddComponent<TrackSurface>().isRoad = true;
        }

        void BuildWalls()
        {
            var root = new GameObject("Walls");
            root.transform.SetParent(transform, false);
            Material red = Factory.Mat(new Color(0.85f, 0.15f, 0.12f));
            Material white = Factory.Mat(new Color(0.95f, 0.95f, 0.95f));

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < Count; i++)
                {
                    int j = Wrap(i + 1);
                    Vector3 a = Points[i] + Right(i) * side * WallOffset;
                    Vector3 b = Points[j] + Right(j) * side * WallOffset;
                    Vector3 dir = b - a;
                    if (dir.sqrMagnitude < 0.01f) continue;

                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Wall";
                    wall.transform.SetParent(root.transform, false);
                    wall.transform.position = (a + b) * 0.5f + Vector3.up * 0.75f;
                    wall.transform.rotation = Quaternion.LookRotation(dir);
                    wall.transform.localScale = new Vector3(1f, 1.5f, dir.magnitude + 0.4f);
                    wall.GetComponent<Renderer>().sharedMaterial = (i / 2) % 2 == 0 ? red : white;
                    wall.AddComponent<TrackWall>();
                }
            }
            StaticBatchingUtility.Combine(root);
        }

        void BuildStartLine()
        {
            Vector3 p = Points[0];
            Quaternion rot = Quaternion.LookRotation(Tangents[0]);

            var line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.name = "StartLine";
            DestroyImmediate(line.GetComponent<Collider>());
            line.transform.SetParent(transform, false);
            line.transform.position = p + Vector3.up * 0.04f;
            line.transform.rotation = rot * Quaternion.Euler(90f, 0f, 0f);
            line.transform.localScale = new Vector3(RoadHalfWidth * 2f, 3f, 1f);
            line.GetComponent<Renderer>().sharedMaterial = Factory.Mat(Factory.CheckerTexture(18, 3), Vector2.one);

            // Gantry over the line.
            var gantry = new GameObject("Gantry");
            gantry.transform.SetParent(transform, false);
            gantry.transform.SetPositionAndRotation(p, rot);
            Material pole = Factory.Mat(new Color(0.2f, 0.2f, 0.25f), 0.6f);
            float x = RoadHalfWidth + 2f;
            foreach (float s in new[] { -1f, 1f })
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.transform.SetParent(gantry.transform, false);
                post.transform.localPosition = new Vector3(s * x, 3.5f, 0f);
                post.transform.localScale = new Vector3(0.8f, 7f, 0.8f);
                post.GetComponent<Renderer>().sharedMaterial = pole;
                post.AddComponent<TrackWall>();
            }
            Factory.Part(PrimitiveType.Cube, gantry.transform, new Vector3(0f, 7.2f, 0f),
                new Vector3(x * 2f + 0.8f, 1.6f, 0.5f), Factory.Mat(Factory.CheckerTexture(24, 2), Vector2.one));
        }

        void BuildScenery(int seed)
        {
            var root = new GameObject("Scenery");
            root.transform.SetParent(transform, false);
            var rng = new System.Random(seed);
            Bounds b = TrackBounds();
            b.Expand(new Vector3(160f, 0f, 160f));

            Material trunk = Factory.Mat(new Color(0.45f, 0.3f, 0.15f));
            Material[] leaves =
            {
                Factory.Mat(new Color(0.15f, 0.55f, 0.2f)),
                Factory.Mat(new Color(0.25f, 0.65f, 0.15f)),
                Factory.Mat(new Color(0.1f, 0.45f, 0.25f)),
            };
            Material pipe = Factory.Mat(new Color(0.1f, 0.75f, 0.2f), 0.7f);
            Material cloud = Factory.Mat(Color.white, 0f);
            Material block = Factory.Mat(new Color(0.95f, 0.7f, 0.1f), 0.5f);

            float R() => (float)rng.NextDouble();
            int placed = 0, attempts = 0;
            while (placed < 260 && attempts < 5000)
            {
                attempts++;
                var pos = new Vector3(Mathf.Lerp(b.min.x, b.max.x, R()), 0f, Mathf.Lerp(b.min.z, b.max.z, R()));
                int near = FindNearest(pos);
                if (Vector3.Distance(Points[near], pos) < WallOffset + 5f) continue;
                placed++;

                var go = new GameObject("Prop");
                go.transform.SetParent(root.transform, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, R() * 360f, 0f);
                float kind = R();
                if (kind < 0.75f)
                {
                    float h = 3f + R() * 4f;
                    Factory.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0, h * 0.5f, 0),
                        new Vector3(0.7f, h * 0.5f, 0.7f), trunk);
                    float s = 3.5f + R() * 3f;
                    Factory.Part(PrimitiveType.Sphere, go.transform, new Vector3(0, h + s * 0.3f, 0),
                        Vector3.one * s, leaves[rng.Next(leaves.Length)]);
                }
                else if (kind < 0.92f)
                {
                    float h = 2f + R() * 4f;
                    Factory.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0, h * 0.5f, 0),
                        new Vector3(3f, h * 0.5f, 3f), pipe);
                    Factory.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0, h, 0),
                        new Vector3(3.8f, 0.6f, 3.8f), pipe);
                }
                else
                {
                    Factory.Part(PrimitiveType.Cube, go.transform, new Vector3(0, 5f + R() * 6f, 0),
                        Vector3.one * 2.5f, block);
                }
            }

            for (int i = 0; i < 30; i++)
            {
                var c = new GameObject("Cloud");
                c.transform.SetParent(root.transform, false);
                c.transform.position = new Vector3(Mathf.Lerp(b.min.x, b.max.x, R()), 60f + R() * 30f,
                    Mathf.Lerp(b.min.z, b.max.z, R()));
                for (int k = 0; k < 3; k++)
                    Factory.Part(PrimitiveType.Sphere, c.transform, new Vector3((k - 1) * 7f, R() * 2f, R() * 3f),
                        new Vector3(12f, 6f, 9f), cloud);
            }
            StaticBatchingUtility.Combine(root);
        }
    }
}
