using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Arcade kart physics. A sphere collider does the rolling/colliding, while this script
    /// rewrites the horizontal velocity every physics step to give tight, grippy handling,
    /// power-slide drifting with three mini-turbo tiers, boosts, spin-outs and item usage.
    /// Drivers (player or AI) only write <see cref="throttle"/>, <see cref="steer"/>,
    /// <see cref="driftHeld"/> and call <see cref="RequestUseItem"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : MonoBehaviour
    {
        // ---- driver input
        [HideInInspector] public float throttle;
        [HideInInspector] public bool driftHeld;
        [HideInInspector] public float steer;
        bool useItemRequested;

        // ---- identity / tuning
        public string racerName = "Racer";
        public Color color = Color.red;
        public bool isPlayer;
        public float maxSpeed = 27f;
        public float acceleration = 17f;
        public float turnSpeed = 100f;
        public float offRoadFactor = 0.5f;
        public float speedMultiplier = 1f; // AI skill / rubber banding
        public bool controlsEnabled;

        public const float BoostFactor = 1.4f;
        static readonly float[] DriftTierCharge = { 0f, 1.0f, 2.2f, 3.4f };
        static readonly float[] DriftTierBoost = { 0f, 0.5f, 1.0f, 1.6f };

        // ---- state
        public ItemType Item { get; private set; }
        public int ItemCount { get; private set; }
        public float RouletteTime { get; private set; }
        public bool Grounded { get; private set; }
        public bool OnRoad { get; private set; } = true;
        public bool IsDrifting { get; private set; }
        public int DriftDirection { get; private set; }
        public int DriftTier { get; private set; }
        public float ForwardSpeed { get; private set; }
        public bool IsBoosting => boostTimer > 0f;
        public bool IsSpinning => spinTimer > 0f;
        public bool HasStar => starTimer > 0f;
        public float TopSpeed => maxSpeed * speedMultiplier;

        public RaceProgress Progress { get; private set; }
        public KartVisual Visual { get; private set; }
        public Rigidbody Body { get; private set; }

        float driftCharge;
        float boostTimer;
        float spinTimer;
        float starTimer;
        float freezeTimer;
        bool prevDriftHeld;

        public static KartController Create(Transform parent, string name, Color color, bool isPlayer,
            float maxSpeed, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 150f;
            rb.drag = 0f;
            rb.angularDrag = 0f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = go.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.radius = 0.6f;
            col.material = new PhysicMaterial("Kart")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0.35f,
                frictionCombine = PhysicMaterialCombine.Minimum,
                bounceCombine = PhysicMaterialCombine.Average,
            };

            var kart = go.AddComponent<KartController>();
            kart.racerName = name;
            kart.color = color;
            kart.isPlayer = isPlayer;
            kart.maxSpeed = maxSpeed;
            kart.acceleration = maxSpeed * 0.62f;
            kart.Body = rb;
            kart.Progress = go.AddComponent<RaceProgress>();
            kart.Visual = KartVisual.Build(go.transform, color);
            return kart;
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
        }

        public void RequestUseItem() => useItemRequested = true;

        void Update()
        {
            float dt = Time.deltaTime;
            if (RouletteTime > 0f) RouletteTime -= dt;

            if (useItemRequested)
            {
                useItemRequested = false;
                UseItem();
            }

            if (Visual != null) Visual.Tick(this, dt);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            boostTimer = Mathf.Max(0f, boostTimer - dt);
            spinTimer = Mathf.Max(0f, spinTimer - dt);
            starTimer = Mathf.Max(0f, starTimer - dt);
            freezeTimer = Mathf.Max(0f, freezeTimer - dt);

            // Ground probe (starts inside our own sphere, so it never hits ourselves).
            Grounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit,
                1.1f, ~0, QueryTriggerInteraction.Ignore);
            OnRoad = true;
            if (Grounded)
            {
                var surface = hit.collider.GetComponent<TrackSurface>();
                OnRoad = surface == null || surface.isRoad;
            }

            bool canDrive = controlsEnabled && !IsSpinning && freezeTimer <= 0f;
            float throttleIn = canDrive ? Mathf.Clamp(throttle, -1f, 1f) : 0f;
            float steerIn = canDrive ? Mathf.Clamp(steer, -1f, 1f) : 0f;

            float top = TopSpeed;
            if (HasStar) top *= 1.15f;
            else if (!OnRoad && !IsBoosting) top *= offRoadFactor;
            if (IsBoosting) top = TopSpeed * BoostFactor;

            Vector3 v = Body.velocity;
            Vector3 fwd = transform.forward;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float fwdSpeed = Vector3.Dot(flat, fwd);
            Vector3 lateral = flat - fwd * fwdSpeed;

            UpdateDrift(canDrive, steerIn, fwdSpeed, dt);

            if (Grounded)
            {
                if (IsSpinning || freezeTimer > 0f)
                    fwdSpeed = Mathf.MoveTowards(fwdSpeed, 0f, 30f * dt);
                else if (IsBoosting)
                    fwdSpeed = Mathf.MoveTowards(fwdSpeed, top, acceleration * 3f * dt);
                else if (throttleIn > 0.01f)
                {
                    float target = top * throttleIn;
                    float rate = fwdSpeed < target
                        ? acceleration * Mathf.Lerp(1.3f, 0.5f, Mathf.Clamp01(fwdSpeed / top))
                        : acceleration * 1.2f; // e.g. bleeding speed after leaving the road
                    fwdSpeed = Mathf.MoveTowards(fwdSpeed, target, rate * dt);
                }
                else if (throttleIn < -0.01f)
                {
                    fwdSpeed = fwdSpeed > 0.5f
                        ? Mathf.MoveTowards(fwdSpeed, 0f, 40f * dt)
                        : Mathf.MoveTowards(fwdSpeed, -top * 0.35f * -throttleIn, acceleration * dt);
                }
                else
                {
                    fwdSpeed = Mathf.MoveTowards(fwdSpeed, 0f, 7f * dt);
                }

                float grip = IsDrifting ? 2.2f : 10f;
                lateral = Vector3.Lerp(lateral, Vector3.zero, 1f - Mathf.Exp(-grip * dt));
                Body.velocity = fwd * fwdSpeed + lateral + Vector3.up * v.y;
            }

            // Extra gravity keeps the kart planted and makes hops snappy.
            Body.AddForce(Vector3.down * 18f, ForceMode.Acceleration);

            // Steering.
            float yawRate = 0f;
            if (!IsSpinning)
            {
                if (IsDrifting)
                {
                    // Steering into the drift tightens it, steering out widens it.
                    float s = Mathf.Lerp(0.45f, 1.35f, (steerIn * DriftDirection + 1f) * 0.5f);
                    yawRate = DriftDirection * s * turnSpeed;
                }
                else
                {
                    float speedFactor = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 5f);
                    float highSpeedDamp = Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(Mathf.Abs(fwdSpeed) / (maxSpeed * 1.3f)));
                    yawRate = steerIn * turnSpeed * speedFactor * highSpeedDamp * Mathf.Sign(fwdSpeed + 0.01f);
                }
                if (!Grounded) yawRate *= 0.6f;
            }
            Body.MoveRotation(Body.rotation * Quaternion.Euler(0f, yawRate * dt, 0f));

            ForwardSpeed = fwdSpeed;

            if (HasStar) StarContact();

            if (transform.position.y < -15f) Respawn();
        }

        void UpdateDrift(bool canDrive, float steerIn, float fwdSpeed, float dt)
        {
            bool pressed = driftHeld && !prevDriftHeld;
            prevDriftHeld = driftHeld;

            if (pressed && canDrive && Grounded && Visual != null) Visual.Hop();

            if (!IsDrifting)
            {
                if (driftHeld && canDrive && Mathf.Abs(steerIn) > 0.3f && fwdSpeed > 12f)
                {
                    IsDrifting = true;
                    DriftDirection = steerIn > 0f ? 1 : -1;
                    driftCharge = 0f;
                }
            }
            else if (!driftHeld)
            {
                EndDrift(true);
            }
            else if (!canDrive || fwdSpeed < 8f)
            {
                EndDrift(false);
            }
            else if (Grounded)
            {
                driftCharge += dt * (0.7f + 0.8f * Mathf.Clamp01(steerIn * DriftDirection + 0.2f));
            }

            DriftTier = 0;
            if (IsDrifting)
                for (int t = 1; t < DriftTierCharge.Length; t++)
                    if (driftCharge >= DriftTierCharge[t]) DriftTier = t;
        }

        void EndDrift(bool release)
        {
            if (IsDrifting && release && DriftTier > 0) Boost(DriftTierBoost[DriftTier]);
            IsDrifting = false;
            DriftTier = 0;
            driftCharge = 0f;
        }

        void StarContact()
        {
            foreach (KartController other in RaceManager.Instance.Karts)
            {
                if (other == this) continue;
                if ((other.transform.position - transform.position).sqrMagnitude < 2.2f * 2.2f)
                    other.SpinOut();
            }
        }

        // ------------------------------------------------------------------ public actions

        public void Boost(float duration)
        {
            boostTimer = Mathf.Max(boostTimer, duration);
            if (Visual != null) Visual.Flash();
        }

        public void SpinOut()
        {
            if (HasStar || IsSpinning) return;
            spinTimer = 1.3f;
            boostTimer = 0f;
            EndDrift(false);
            if (Visual != null) Visual.StartSpin();
        }

        public bool GiveItem(ItemType item)
        {
            if (Item != ItemType.None) return false;
            Item = item;
            ItemCount = item == ItemType.TripleMushroom ? 3 : 1;
            RouletteTime = 1.2f;
            return true;
        }

        void UseItem()
        {
            if (Item == ItemType.None || RouletteTime > 0f || !controlsEnabled || IsSpinning) return;

            switch (Item)
            {
                case ItemType.Mushroom:
                case ItemType.TripleMushroom:
                    Boost(1.3f);
                    break;
                case ItemType.Banana:
                    Banana.Spawn(this, transform.position - transform.forward * 2.4f);
                    break;
                case ItemType.GreenShell:
                    Shell.Spawn(this, false);
                    break;
                case ItemType.RedShell:
                    Shell.Spawn(this, true);
                    break;
                case ItemType.Star:
                    starTimer = 7f;
                    break;
            }

            if (--ItemCount <= 0)
            {
                Item = ItemType.None;
                ItemCount = 0;
            }
        }

        /// <summary>Puts the kart back on the centre line at its last known track position.</summary>
        public void Respawn()
        {
            Track track = RaceManager.Instance.Track;
            int i = Progress.Index >= 0 ? Progress.Index : track.FindNearest(transform.position);
            Vector3 pos = track.Points[i] + Vector3.up * 0.5f;
            Quaternion rot = Quaternion.LookRotation(track.Tangents[i]);
            Body.velocity = Vector3.zero;
            Body.position = pos;
            Body.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
            EndDrift(false);
            spinTimer = 0f;
            boostTimer = 0f;
            freezeTimer = 1f;
        }
    }
}
