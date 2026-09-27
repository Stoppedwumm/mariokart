using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Builds a kart + driver out of primitives and animates it: wheel spin and steering,
    /// drift lean, hop, spin-out, mini-turbo sparks, boost flame and star flashing.
    /// </summary>
    public class KartVisual : MonoBehaviour
    {
        static readonly Color[] SparkColors =
        {
            Color.clear,
            new Color(0.3f, 0.6f, 1f),   // blue
            new Color(1f, 0.55f, 0.1f),  // orange
            new Color(0.8f, 0.3f, 1f),   // purple
        };

        Transform body;          // everything that leans / hops / spins
        Transform[] frontPivots;
        Transform[] wheels;
        Renderer[] sparks;
        GameObject flame;
        Renderer[] paint;
        Color baseColor;

        float wheelAngle;
        float hopTime = 1f;
        float spinAngle;
        float spinRemaining;
        float lean;
        float flashTime;

        public static KartVisual Build(Transform kart, Color color)
        {
            var root = new GameObject("Visual");
            root.transform.SetParent(kart, false);
            var v = root.AddComponent<KartVisual>();
            v.baseColor = color;

            v.body = new GameObject("Body").transform;
            v.body.SetParent(root.transform, false);
            Transform b = v.body;

            Material paint = Factory.Mat(color, 0.7f);
            Material dark = Factory.Mat(new Color(0.12f, 0.12f, 0.14f), 0.3f);
            Material metal = Factory.Mat(new Color(0.7f, 0.7f, 0.75f), 0.8f);
            Material skin = Factory.Mat(new Color(1f, 0.8f, 0.62f), 0.2f);
            Material overalls = Factory.Mat(new Color(0.15f, 0.25f, 0.75f), 0.2f);

            var painted = new System.Collections.Generic.List<Renderer>();
            GameObject P(PrimitiveType t, Vector3 p, Vector3 s, Material m, Quaternion? r = null)
            {
                GameObject go = Factory.Part(t, b, p, s, m, r);
                if (m == paint) painted.Add(go.GetComponent<Renderer>());
                return go;
            }

            // Chassis
            P(PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(1.3f, 0.3f, 2.1f), paint);
            P(PrimitiveType.Cube, new Vector3(0f, 0.45f, 1.25f), new Vector3(1.0f, 0.22f, 0.6f), paint);
            P(PrimitiveType.Cube, new Vector3(0f, 0.35f, 1.55f), new Vector3(1.5f, 0.12f, 0.3f), dark); // front bumper
            P(PrimitiveType.Cube, new Vector3(0f, 0.75f, -0.55f), new Vector3(0.85f, 0.55f, 0.25f), dark); // seat
            P(PrimitiveType.Cube, new Vector3(0f, 0.95f, -1.05f), new Vector3(1.4f, 0.08f, 0.35f), paint); // spoiler
            foreach (float s in new[] { -0.45f, 0.45f })
            {
                P(PrimitiveType.Cylinder, new Vector3(s, 0.55f, -1.15f), new Vector3(0.22f, 0.2f, 0.22f), metal,
                    Quaternion.Euler(90f, 0f, 0f)); // exhausts
                P(PrimitiveType.Cube, new Vector3(s * 1.2f, 0.85f, -1.05f), new Vector3(0.06f, 0.35f, 0.2f), dark);
            }

            // Driver
            P(PrimitiveType.Capsule, new Vector3(0f, 1.0f, -0.25f), new Vector3(0.62f, 0.4f, 0.5f), overalls);
            P(PrimitiveType.Cube, new Vector3(0f, 1.15f, -0.25f), new Vector3(0.64f, 0.3f, 0.45f), paint); // shirt
            P(PrimitiveType.Sphere, new Vector3(0f, 1.6f, -0.2f), new Vector3(0.55f, 0.55f, 0.55f), skin); // head
            P(PrimitiveType.Sphere, new Vector3(0f, 1.57f, 0.08f), new Vector3(0.14f, 0.12f, 0.14f), skin); // nose
            P(PrimitiveType.Cube, new Vector3(0f, 1.5f, 0.05f), new Vector3(0.3f, 0.06f, 0.06f), dark); // moustache
            P(PrimitiveType.Sphere, new Vector3(0f, 1.8f, -0.22f), new Vector3(0.6f, 0.3f, 0.6f), paint); // cap
            P(PrimitiveType.Cube, new Vector3(0f, 1.74f, 0.1f), new Vector3(0.45f, 0.05f, 0.25f), paint); // cap brim
            P(PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0.35f), new Vector3(0.45f, 0.03f, 0.45f), dark,
                Quaternion.Euler(-60f, 0f, 0f)); // steering wheel

            // Wheels
            v.frontPivots = new Transform[2];
            v.wheels = new Transform[4];
            Vector3[] wheelPos =
            {
                new Vector3(-0.78f, 0.3f, 0.85f), new Vector3(0.78f, 0.3f, 0.85f),
                new Vector3(-0.8f, 0.34f, -0.8f), new Vector3(0.8f, 0.34f, -0.8f),
            };
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("WheelPivot").transform;
                pivot.SetParent(b, false);
                pivot.localPosition = wheelPos[i];
                if (i < 2) v.frontPivots[i] = pivot;

                var wheel = new GameObject("Wheel").transform;
                wheel.SetParent(pivot, false);
                v.wheels[i] = wheel;
                float d = i < 2 ? 0.58f : 0.68f;
                Factory.Part(PrimitiveType.Cylinder, wheel, Vector3.zero, new Vector3(d, 0.18f, d), dark,
                    Quaternion.Euler(0f, 0f, 90f));
                Factory.Part(PrimitiveType.Cylinder, wheel, Vector3.zero, new Vector3(d * 0.5f, 0.19f, d * 0.2f),
                    metal, Quaternion.Euler(0f, 0f, 90f)); // hub stripe makes the spin visible
            }

            // Drift sparks behind the rear wheels.
            v.sparks = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                GameObject s = Factory.Part(PrimitiveType.Sphere, b, new Vector3(i == 0 ? -0.8f : 0.8f, 0.12f, -1.15f),
                    new Vector3(0.35f, 0.25f, 0.5f), Factory.Glow(Color.white));
                v.sparks[i] = s.GetComponent<Renderer>();
                s.SetActive(false);
            }

            v.flame = new GameObject("BoostFlame");
            v.flame.transform.SetParent(b, false);
            Material flameMat = Factory.Glow(new Color(1f, 0.55f, 0.1f));
            foreach (float s in new[] { -0.45f, 0.45f })
                Factory.Part(PrimitiveType.Sphere, v.flame.transform, new Vector3(s, 0.55f, -1.55f),
                    new Vector3(0.3f, 0.3f, 0.9f), flameMat);
            v.flame.SetActive(false);

            v.paint = painted.ToArray();
            return v;
        }

        public void Hop() => hopTime = 0f;

        public void StartSpin() => spinRemaining = 720f;

        public void Flash() => flashTime = 0.15f;

        public void Tick(KartController kart, float dt)
        {
            // Wheels
            wheelAngle += kart.ForwardSpeed / 0.3f * Mathf.Rad2Deg * dt;
            foreach (Transform w in wheels) w.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
            float steerVisual = kart.IsDrifting ? -kart.DriftDirection * 15f : kart.steer * 25f;
            if (!kart.controlsEnabled) steerVisual = 0f;
            foreach (Transform p in frontPivots) p.localRotation = Quaternion.Euler(0f, steerVisual, 0f);

            // Hop
            hopTime += dt;
            float hop = hopTime < 0.25f ? Mathf.Sin(hopTime / 0.25f * Mathf.PI) * 0.35f : 0f;

            // Drift lean / yaw offset
            float targetLean = kart.IsDrifting ? kart.DriftDirection : 0f;
            lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-10f * dt));

            // Spin-out
            if (spinRemaining > 0f)
            {
                float step = Mathf.Min(spinRemaining, 720f / 1.2f * dt);
                spinRemaining -= step;
                spinAngle = (spinAngle + step) % 360f;
            }
            else
            {
                spinAngle = Mathf.LerpAngle(spinAngle, 0f, 1f - Mathf.Exp(-12f * dt));
            }

            body.localPosition = new Vector3(0f, hop, 0f);
            body.localRotation = Quaternion.Euler(0f, lean * 22f + spinAngle, -lean * 6f);

            // Sparks
            bool showSparks = kart.IsDrifting && kart.DriftTier > 0;
            foreach (Renderer s in sparks)
            {
                s.gameObject.SetActive(showSparks);
                if (!showSparks) continue;
                Factory.SetColor(s, SparkColors[kart.DriftTier]);
                s.transform.localScale = new Vector3(0.35f, 0.25f, 0.5f) * Random.Range(0.7f, 1.3f);
            }

            flame.SetActive(kart.IsBoosting);
            if (kart.IsBoosting)
                flame.transform.localScale = new Vector3(1f, 1f, Random.Range(0.8f, 1.4f));

            // Star rainbow / boost flash
            flashTime -= dt;
            Color c = baseColor;
            if (kart.HasStar) c = Color.HSVToRGB(Time.time * 3f % 1f, 0.8f, 1f);
            else if (flashTime > 0f) c = Color.white;
            foreach (Renderer r in paint) Factory.SetColor(r, c);
        }
    }
}
