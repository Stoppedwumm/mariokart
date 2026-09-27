using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Runtime helpers for building materials, procedural textures and primitive meshes.
    /// Everything in the game is generated from code, so the project needs no imported art.
    /// </summary>
    public static class Factory
    {
        static Material baseMaterial;

        static Material BaseMaterial
        {
            get
            {
                if (baseMaterial != null) return baseMaterial;

                // Resources/KartBase.mat references the Standard shader, which also guarantees
                // the shader is included in player builds.
                baseMaterial = Resources.Load<Material>("KartBase");
                if (baseMaterial == null)
                {
                    Shader shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Diffuse");
                    baseMaterial = new Material(shader);
                }
                return baseMaterial;
            }
        }

        public static Material Mat(Color color, float smoothness = 0.2f)
        {
            var m = new Material(BaseMaterial) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        public static Material Mat(Texture2D texture, Vector2 tiling, float smoothness = 0.1f)
        {
            Material m = Mat(Color.white, smoothness);
            m.mainTexture = texture;
            m.mainTextureScale = tiling;
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", texture);
                m.SetTextureScale("_BaseMap", tiling);
            }
            return m;
        }

        public static Material Glow(Color color)
        {
            Material m = Mat(color, 0.6f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 1.5f);
            return m;
        }

        public static void SetColor(Renderer r, Color color)
        {
            r.material.color = color;
            if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", color);
            if (r.material.HasProperty("_EmissionColor")) r.material.SetColor("_EmissionColor", color * 1.5f);
        }

        /// <summary>Creates a primitive without a collider, parented and colored.</summary>
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
            Material material, Quaternion? localRot = null, string name = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        // ------------------------------------------------------------------ textures

        static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
        }

        public static Texture2D GrassTexture()
        {
            var t = NewTexture(64, 64);
            var rng = new System.Random(7);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float n = (float)rng.NextDouble() * 0.12f;
                bool stripe = ((x / 16) + (y / 16)) % 2 == 0;
                float g = stripe ? 0.62f : 0.55f;
                t.SetPixel(x, y, new Color(0.22f + n * 0.5f, g + n, 0.18f + n * 0.3f));
            }
            t.Apply();
            return t;
        }

        /// <summary>Road texture: u runs across the road (kerb | asphalt | kerb), v runs along it.</summary>
        public static Texture2D RoadTexture()
        {
            const int w = 128, h = 64;
            var t = NewTexture(w, h);
            t.wrapMode = TextureWrapMode.Repeat;
            var rng = new System.Random(3);
            float kerb = 1f / 18f; // one metre of kerb on each side of an 18 m wide road
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float v = (y + 0.5f) / h;
                float n = (float)rng.NextDouble() * 0.06f;
                Color c = new Color(0.33f + n, 0.33f + n, 0.36f + n);

                if (u < kerb || u > 1f - kerb)
                    c = (v < 0.5f) ? new Color(0.85f, 0.1f, 0.1f) : Color.white;
                else if (Mathf.Abs(u - kerb) < 0.012f || Mathf.Abs(u - (1f - kerb)) < 0.012f)
                    c = Color.white;
                else if (Mathf.Abs(u - 0.5f) < 0.008f && v < 0.5f)
                    c = new Color(0.9f, 0.85f, 0.3f);

                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        public static Texture2D CheckerTexture(int cellsX, int cellsY, int cellSize = 8)
        {
            var t = NewTexture(cellsX * cellSize, cellsY * cellSize);
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
                t.SetPixel(x, y, ((x / cellSize) + (y / cellSize)) % 2 == 0 ? Color.white : Color.black);
            t.Apply();
            return t;
        }

        public static Texture2D ItemBoxTexture()
        {
            const int s = 32;
            var t = NewTexture(s, s);
            t.filterMode = FilterMode.Point;
            // A chunky "?" drawn on a 8x8 grid.
            string[] glyph =
            {
                "..####..",
                ".##..##.",
                ".....##.",
                "....##..",
                "...##...",
                "...##...",
                "........",
                "...##...",
            };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int gx = x / 4, gy = 7 - y / 4;
                bool on = glyph[gy][gx] == '#';
                bool border = x < 2 || y < 2 || x >= s - 2 || y >= s - 2;
                t.SetPixel(x, y, on || border ? Color.white : new Color(0.55f, 0.55f, 0.55f));
            }
            t.Apply();
            return t;
        }
    }
}
