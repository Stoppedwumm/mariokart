using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Immediate-mode (OnGUI) HUD: title screen, countdown, lap/time, position, item slot,
    /// speedometer, standings, minimap and results. Laid out on a virtual 1280x720 canvas.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        const float VirtualHeight = 720f;

        RaceManager race;
        Texture2D white;
        GUIStyle big, medium, small, center;
        Vector2 mapMin, mapMax;

        void Awake()
        {
            race = GetComponent<RaceManager>();
            white = Texture2D.whiteTexture;
        }

        void EnsureStyles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            medium = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            center = new GUIStyle(medium) { alignment = TextAnchor.MiddleCenter };
        }

        float VirtualWidth => Screen.width * VirtualHeight / Screen.height;

        void OnGUI()
        {
            if (race == null || race.Track == null) return;
            EnsureStyles();
            float scale = Screen.height / VirtualHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = VirtualWidth, h = VirtualHeight;

            if (race.State == RaceManager.RaceState.Title)
            {
                DrawTitle(w, h);
                return;
            }

            KartController player = race.Player;
            RaceProgress p = player.Progress;

            // Lap + time (top-left)
            Box(new Rect(16, 16, 250, 86), new Color(0, 0, 0, 0.45f));
            Text(new Rect(30, 20, 240, 40), $"LAP {p.DisplayLap}/{race.TotalLaps}", medium, Color.white);
            float time = p.Finished ? p.FinishTime : race.RaceTime;
            Text(new Rect(30, 60, 240, 30), FormatTime(time), small, new Color(1f, 0.9f, 0.4f));

            // Item slot (top-centre)
            DrawItemSlot(new Rect(w * 0.5f - 55, 14, 110, 110), player);

            // Position (top-right)
            string place = Ordinal(p.Place);
            Color placeColor = p.Place == 1 ? new Color(1f, 0.85f, 0.1f) : p.Place <= 3 ? new Color(0.5f, 0.85f, 1f) : Color.white;
            var placeStyle = new GUIStyle(big) { alignment = TextAnchor.UpperRight, fontSize = 80 };
            Text(new Rect(w - 260, 6, 240, 100), place, placeStyle, placeColor);
            Text(new Rect(w - 260, 96, 240, 30), $"of {race.Karts.Count}", new GUIStyle(small) { alignment = TextAnchor.UpperRight }, Color.white);

            DrawStandings(new Rect(16, 116, 200, 24 * race.Karts.Count + 10));
            DrawMinimap(new Rect(16, h - 216, 200, 200));

            // Speed (bottom-right)
            float kmh = Mathf.Abs(player.ForwardSpeed) * 3.6f;
            Text(new Rect(w - 260, h - 90, 240, 70), $"{kmh:0} km/h", new GUIStyle(medium) { alignment = TextAnchor.LowerRight, fontSize = 36 },
                player.IsBoosting ? new Color(1f, 0.6f, 0.2f) : Color.white);
            if (player.IsDrifting && player.DriftTier > 0)
            {
                string[] tiers = { "", "MINI-TURBO", "SUPER MINI-TURBO", "ULTRA MINI-TURBO" };
                Color[] tierColors = { Color.white, new Color(0.4f, 0.7f, 1f), new Color(1f, 0.6f, 0.2f), new Color(0.85f, 0.4f, 1f) };
                Text(new Rect(w - 360, h - 130, 340, 40), tiers[player.DriftTier],
                    new GUIStyle(small) { alignment = TextAnchor.LowerRight, fontSize = 22 }, tierColors[player.DriftTier]);
            }

            // Countdown / messages (centre)
            if (race.State == RaceManager.RaceState.Countdown)
            {
                int n = Mathf.CeilToInt(race.Countdown);
                if (n <= 3)
                {
                    float frac = race.Countdown - Mathf.Floor(race.Countdown);
                    var s = new GUIStyle(big) { fontSize = (int)(110 + frac * 60) };
                    Text(new Rect(0, h * 0.3f, w, 180), n.ToString(), s, new Color(1f, 0.3f + 0.2f * n, 0.2f));
                }
                Text(new Rect(0, h * 0.62f, w, 40), "Hold accelerate as \"1\" appears for a rocket start!", center, Color.white);
            }
            else if (race.MessageTime > 0f && !string.IsNullOrEmpty(race.Message))
            {
                Text(new Rect(0, h * 0.28f, w, 120), race.Message, new GUIStyle(big) { fontSize = 90 }, new Color(1f, 0.85f, 0.15f));
            }

            if (p.WrongWay && race.State == RaceManager.RaceState.Racing)
                Text(new Rect(0, h * 0.45f, w, 80), "WRONG WAY!", big, new Color(1f, 0.25f, 0.2f));

            if (race.Paused)
            {
                Box(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.5f));
                Text(new Rect(0, h * 0.4f, w, 80), "PAUSED", big, Color.white);
                Text(new Rect(0, h * 0.55f, w, 40), "P: resume   R: restart   Esc: title", center, Color.white);
            }

            if (race.State == RaceManager.RaceState.Finished) DrawResults(w, h);

            // Controls hint
            if (race.State != RaceManager.RaceState.Finished)
                Text(new Rect(w - 620, h - 30, 600, 26),
                    "WASD/Arrows drive · Space drift · E/Ctrl item · Q look back · T respawn · P pause · R restart",
                    new GUIStyle(small) { fontSize = 13, alignment = TextAnchor.LowerRight }, new Color(1, 1, 1, 0.7f));
        }

        // ------------------------------------------------------------------ panels

        void DrawTitle(float w, float h)
        {
            Box(new Rect(0, 0, w, h), new Color(0f, 0f, 0.1f, 0.35f));
            var title = new GUIStyle(big) { fontSize = 96 };
            Text(new Rect(0, h * 0.14f, w, 120), "KART RACER", title, new Color(1f, 0.25f, 0.2f));
            Text(new Rect(0, h * 0.14f + 100, w, 50), "Mushroom Circuit", center, new Color(1f, 0.9f, 0.3f));

            float cx = w * 0.5f;
            Text(new Rect(0, h * 0.46f, w, 40), "Engine class", center, Color.white);
            for (int i = 0; i < RaceManager.Classes.Length; i++)
            {
                var r = new Rect(cx - 240 + i * 165, h * 0.46f + 50, 150, 56);
                bool sel = i == RaceManager.SelectedClass;
                Box(r, sel ? new Color(1f, 0.3f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.5f));
                Text(r, RaceManager.Classes[i].name, center, Color.white);
            }
            Text(new Rect(0, h * 0.46f + 115, w, 30), "← / → to choose", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, Color.white);

            float blink = Mathf.PingPong(Time.unscaledTime * 2f, 1f);
            Text(new Rect(0, h * 0.78f, w, 50), "Press ENTER to race", center, new Color(1f, 1f, 1f, 0.4f + 0.6f * blink));
            Text(new Rect(0, h - 44, w, 30),
                "W/↑ accelerate · S/↓ brake · A/D steer · Space hop & drift (release for mini-turbo) · E/Ctrl use item",
                new GUIStyle(small) { fontSize = 15, alignment = TextAnchor.MiddleCenter }, new Color(1, 1, 1, 0.8f));
        }

        void DrawItemSlot(Rect r, KartController kart)
        {
            Box(r, new Color(0f, 0f, 0f, 0.55f));
            Box(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), new Color(1f, 1f, 1f, 0.12f));

            ItemType item = kart.Item;
            if (item == ItemType.None) return;
            if (kart.RouletteTime > 0f)
                item = Items.All[(int)(Time.time * 14f) % Items.All.Length];

            Color c = Items.Color(item);
            var inner = new Rect(r.x + 22, r.y + 14, r.width - 44, r.height - 50);
            Box(inner, c);
            string label = item == ItemType.TripleMushroom ? $"Mushroom x{(kart.RouletteTime > 0f ? 3 : kart.ItemCount)}" : Items.Name(item);
            Text(new Rect(r.x - 40, r.yMax - 36, r.width + 80, 30), label,
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 16 }, Color.white);
        }

        void DrawStandings(Rect r)
        {
            Box(r, new Color(0, 0, 0, 0.35f));
            for (int i = 0; i < race.Standings.Count; i++)
            {
                KartController k = race.Standings[i];
                var row = new Rect(r.x + 8, r.y + 5 + i * 24, r.width - 16, 24);
                Box(new Rect(row.x, row.y + 5, 14, 14), k.color);
                Text(new Rect(row.x + 22, row.y, row.width, 24), $"{i + 1}. {k.racerName}", small,
                    k == race.Player ? new Color(1f, 0.9f, 0.3f) : Color.white);
            }
        }

        void DrawMinimap(Rect r)
        {
            Track t = race.Track;
            if (mapMax == mapMin)
            {
                mapMin = new Vector2(float.MaxValue, float.MaxValue);
                mapMax = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector3 p in t.Points)
                {
                    mapMin = Vector2.Min(mapMin, new Vector2(p.x, p.z));
                    mapMax = Vector2.Max(mapMax, new Vector2(p.x, p.z));
                }
            }
            Box(r, new Color(0, 0, 0, 0.35f));
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector2 m = MapPoint(r, t.Points[i]);
                Box(new Rect(m.x - 2, m.y - 2, 4, 4), new Color(1, 1, 1, 0.8f));
            }
            Vector2 start = MapPoint(r, t.Points[0]);
            Box(new Rect(start.x - 4, start.y - 1, 8, 3), Color.black);

            foreach (KartController k in race.Karts)
            {
                Vector2 m = MapPoint(r, k.transform.position);
                float s = k == race.Player ? 11 : 8;
                if (k == race.Player) Box(new Rect(m.x - s / 2 - 2, m.y - s / 2 - 2, s + 4, s + 4), Color.white);
                Box(new Rect(m.x - s / 2, m.y - s / 2, s, s), k.color);
            }
        }

        Vector2 MapPoint(Rect r, Vector3 world)
        {
            Vector2 size = mapMax - mapMin;
            float span = Mathf.Max(size.x, size.y);
            float pad = 12f;
            float sc = (r.width - pad * 2) / span;
            Vector2 offset = new Vector2((span - size.x) * 0.5f, (span - size.y) * 0.5f);
            float x = (world.x - mapMin.x + offset.x) * sc;
            float y = (world.z - mapMin.y + offset.y) * sc;
            return new Vector2(r.x + pad + x, r.yMax - pad - y);
        }

        void DrawResults(float w, float h)
        {
            var r = new Rect(w * 0.5f - 230, h * 0.2f, 460, 90 + race.Standings.Count * 34);
            Box(r, new Color(0f, 0f, 0.05f, 0.75f));
            Text(new Rect(r.x, r.y + 12, r.width, 50), "RESULTS", center, new Color(1f, 0.85f, 0.2f));
            for (int i = 0; i < race.Standings.Count; i++)
            {
                KartController k = race.Standings[i];
                float y = r.y + 64 + i * 34;
                Color c = k == race.Player ? new Color(1f, 0.9f, 0.3f) : Color.white;
                Box(new Rect(r.x + 24, y + 8, 16, 16), k.color);
                Text(new Rect(r.x + 50, y, 240, 32), $"{Ordinal(i + 1)}  {k.racerName}", small, c);
                string t = k.Progress.Finished ? FormatTime(k.Progress.FinishTime) : "racing…";
                Text(new Rect(r.x + 250, y, 190, 32), t, new GUIStyle(small) { alignment = TextAnchor.UpperRight }, c);
            }
            Text(new Rect(0, r.yMax + 12, w, 40), "R: race again    Esc: title screen", center, Color.white);
        }

        // ------------------------------------------------------------------ helpers

        void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Text(Rect r, string text, GUIStyle style, Color color)
        {
            var s = new GUIStyle(style);
            s.normal.textColor = new Color(0, 0, 0, 0.75f * color.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, s);
            s.normal.textColor = color;
            GUI.Label(r, text, s);
        }

        static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m}:{s:00.00}";
        }
    }
}
