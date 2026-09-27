using System.Collections.Generic;
using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Builds the whole race (lighting, camera, track, karts, item boxes, HUD) and runs the
    /// title screen → countdown → race → results flow. Everything it creates is parented to its
    /// own GameObject, so restarting is just "destroy me and spawn a fresh manager".
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public enum RaceState { Title, Countdown, Racing, Finished }

        public struct EngineClass
        {
            public string name;
            public float maxSpeed;
        }

        public static readonly EngineClass[] Classes =
        {
            new EngineClass { name = "50cc", maxSpeed = 22f },
            new EngineClass { name = "100cc", maxSpeed = 27f },
            new EngineClass { name = "150cc", maxSpeed = 32f },
        };

        static readonly (string name, Color color)[] Roster =
        {
            ("Verde", new Color(0.15f, 0.7f, 0.2f)),
            ("Rosa", new Color(1f, 0.45f, 0.7f)),
            ("Giallo", new Color(1f, 0.85f, 0.1f)),
            ("Azzurro", new Color(0.25f, 0.6f, 1f)),
            ("Viola", new Color(0.55f, 0.25f, 0.85f)),
            ("Arancio", new Color(1f, 0.5f, 0.1f)),
            ("Nero", new Color(0.2f, 0.2f, 0.22f)),
        };

        const int PlayerGridSlot = 5;

        static int selectedClass = 1;
        static bool skipTitle;

        public static RaceManager Instance { get; private set; }

        public int TotalLaps = 3;
        public Track Track { get; private set; }
        public List<KartController> Karts { get; } = new List<KartController>();
        public List<KartController> Standings { get; } = new List<KartController>();
        public List<KartController> FinishOrder { get; } = new List<KartController>();
        public KartController Player { get; private set; }
        public RaceState State { get; private set; }
        public float RaceTime { get; private set; }
        public float Countdown { get; private set; }
        public float StateTime { get; private set; }
        public string Message { get; private set; }
        public float MessageTime { get; private set; }
        public bool Paused { get; private set; }
        public static int SelectedClass => selectedClass;

        KartCamera kartCamera;
        float playerThrottlePressedAt = -1f;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            DisableForeignCamerasAndLights();
            BuildEnvironment();

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(transform, false);
            Track = trackGo.AddComponent<Track>();
            Track.Build(1234);

            SpawnKarts();
            SpawnItemBoxes();

            kartCamera.target = Player;
            kartCamera.orbitCenter = Track.Points[0] - Track.Tangents[0] * 15f;

            gameObject.AddComponent<RaceHUD>();

            if (skipTitle) BeginCountdown();
            else EnterState(RaceState.Title);
            kartCamera.orbit = State == RaceState.Title;
            if (!kartCamera.orbit) kartCamera.Snap();
            UpdateStandings();
        }

        // ------------------------------------------------------------------ setup

        void DisableForeignCamerasAndLights()
        {
            foreach (Camera c in FindObjectsOfType<Camera>())
                if (!c.transform.IsChildOf(transform)) c.gameObject.SetActive(false);
            foreach (Light l in FindObjectsOfType<Light>())
                if (!l.transform.IsChildOf(transform)) l.gameObject.SetActive(false);
            foreach (AudioListener a in FindObjectsOfType<AudioListener>())
                if (!a.transform.IsChildOf(transform)) a.enabled = false;
        }

        void BuildEnvironment()
        {
            var sun = new GameObject("Sun");
            sun.transform.SetParent(transform, false);
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.8f, 0.98f);
            RenderSettings.fogStartDistance = 180f;
            RenderSettings.fogEndDistance = 520f;

            var camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.farClipPlane = 800f;
            cam.fieldOfView = 62f;
            camGo.AddComponent<AudioListener>();
            kartCamera = camGo.AddComponent<KartCamera>();
        }

        void SpawnKarts()
        {
            var root = new GameObject("Karts");
            root.transform.SetParent(transform, false);
            float maxSpeed = Classes[selectedClass].maxSpeed;
            int total = Roster.Length + 1;
            int cpu = 0;

            for (int slot = 0; slot < total; slot++)
            {
                int row = slot / 2, col = slot % 2;
                float back = 8f + row * 7f + col * 3f;
                int idx = Track.Advance(0, -back);
                Vector3 pos = Track.Points[idx] + Track.Right(idx) * (col == 0 ? -3.5f : 3.5f) + Vector3.up * 0.3f;
                Quaternion rot = Quaternion.LookRotation(Track.Tangents[idx]);

                KartController kart;
                if (slot == PlayerGridSlot)
                {
                    kart = KartController.Create(root.transform, "You", new Color(0.9f, 0.1f, 0.1f), true, maxSpeed, pos, rot);
                    kart.gameObject.AddComponent<PlayerDriver>();
                    kart.gameObject.AddComponent<EngineAudio>();
                    Player = kart;
                }
                else
                {
                    var (name, color) = Roster[cpu++];
                    kart = KartController.Create(root.transform, name, color, false, maxSpeed, pos, rot);
                    var ai = kart.gameObject.AddComponent<AIDriver>();
                    ai.skill = Mathf.Lerp(0.99f, 0.9f, slot / (float)(total - 1)) + Random.Range(-0.015f, 0.015f);
                }
                kart.Progress.ResetIndex(idx);
                Karts.Add(kart);
            }
        }

        void SpawnItemBoxes()
        {
            var root = new GameObject("ItemBoxes");
            root.transform.SetParent(transform, false);
            Texture2D tex = Factory.ItemBoxTexture();
            foreach (float f in new[] { 0.14f, 0.38f, 0.62f, 0.85f })
            {
                int idx = Track.Wrap(Mathf.RoundToInt(Track.Count * f));
                for (int lane = -2; lane <= 2; lane++)
                {
                    Vector3 pos = Track.Points[idx] + Track.Right(idx) * lane * 3.2f + Vector3.up * 1.3f;
                    ItemBox.Create(root.transform, pos, tex);
                }
            }
        }

        // ------------------------------------------------------------------ flow

        void EnterState(RaceState s)
        {
            State = s;
            StateTime = 0f;
        }

        void BeginCountdown()
        {
            EnterState(RaceState.Countdown);
            Countdown = 3.999f;
            kartCamera.orbit = false;
            kartCamera.Snap();
        }

        void Update()
        {
            StateTime += Time.unscaledDeltaTime;
            MessageTime -= Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.R)) { Restart(true); return; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == RaceState.Title) return;
                Restart(false);
                return;
            }

            switch (State)
            {
                case RaceState.Title: UpdateTitle(); break;
                case RaceState.Countdown: UpdateCountdown(); break;
                case RaceState.Racing: UpdateRacing(); break;
                case RaceState.Finished: UpdateRacing(); break;
            }
        }

        void UpdateTitle()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                ChangeClass(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                ChangeClass(1);

            if (StateTime > 0.3f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                                     Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton7) ||
                                     Input.GetKeyDown(KeyCode.JoystickButton0)))
                BeginCountdown();
        }

        void ChangeClass(int delta)
        {
            selectedClass = Mathf.Clamp(selectedClass + delta, 0, Classes.Length - 1);
            foreach (KartController k in Karts)
            {
                k.maxSpeed = Classes[selectedClass].maxSpeed;
                k.acceleration = k.maxSpeed * 0.62f;
            }
        }

        void UpdateCountdown()
        {
            Countdown -= Time.deltaTime;

            // Rocket start: press accelerate as "1" is showing and hold it through "GO".
            bool accel = Input.GetAxisRaw("Vertical") > 0.5f || Input.GetKey(KeyCode.JoystickButton0);
            if (accel && playerThrottlePressedAt < 0f) playerThrottlePressedAt = Countdown;
            if (!accel) playerThrottlePressedAt = -1f;

            if (Countdown <= 0f)
            {
                EnterState(RaceState.Racing);
                RaceTime = 0f;
                foreach (KartController k in Karts) k.controlsEnabled = true;

                if (playerThrottlePressedAt > 0f && playerThrottlePressedAt < 1.3f) Player.Boost(1.2f);
                foreach (KartController k in Karts)
                    if (k != Player && Random.value < 0.4f) k.Boost(Random.Range(0.4f, 1.2f));
                ShowMessage("GO!", 1f);
            }
        }

        void UpdateRacing()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                Paused = !Paused;
                Time.timeScale = Paused ? 0f : 1f;
            }
            if (State == RaceState.Racing) RaceTime += Time.deltaTime;
            UpdateStandings();
        }

        void UpdateStandings()
        {
            Standings.Clear();
            Standings.AddRange(Karts);
            Standings.Sort((a, b) =>
            {
                int fa = FinishOrder.IndexOf(a), fb = FinishOrder.IndexOf(b);
                if (fa >= 0 || fb >= 0)
                {
                    if (fa < 0) return 1;
                    if (fb < 0) return -1;
                    return fa.CompareTo(fb);
                }
                return b.Progress.RaceDistance.CompareTo(a.Progress.RaceDistance);
            });
            for (int i = 0; i < Standings.Count; i++) Standings[i].Progress.Place = i + 1;
        }

        public void OnLapCompleted(KartController kart, int lap)
        {
            if (kart != Player) return;
            ShowMessage(lap == TotalLaps ? "FINAL LAP!" : "LAP " + lap, 2f);
        }

        public void OnKartFinished(KartController kart)
        {
            FinishOrder.Add(kart);
            if (kart != Player) return;

            ShowMessage("FINISH!", 3f);
            EnterState(RaceState.Finished);

            // Let the CPU drive the player's kart for a victory lap.
            Destroy(kart.GetComponent<PlayerDriver>());
            kart.gameObject.AddComponent<AIDriver>().skill = 0.85f;
            kartCamera.orbit = true;
        }

        void ShowMessage(string msg, float duration)
        {
            Message = msg;
            MessageTime = duration;
        }

        /// <summary>The kart one place ahead of <paramref name="kart"/> (or the next one ahead on track if leading).</summary>
        public KartController KartAhead(KartController kart)
        {
            int place = kart.Progress.Place;
            if (place > 1) return Standings[place - 2];
            return null;
        }

        void Restart(bool straightToCountdown)
        {
            skipTitle = straightToCountdown;
            Time.timeScale = 1f;
            var go = new GameObject("Race");
            go.AddComponent<RaceManager>();
            Destroy(gameObject);
        }
    }
}
