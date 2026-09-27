using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// CPU racer: follows the track centre line with a wandering lane offset, drifts through
    /// sharp corners, uses items tactically, rubber-bands around the player and recovers when stuck.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class AIDriver : MonoBehaviour
    {
        public float skill = 0.95f;     // fraction of max speed this driver aims for

        KartController kart;
        float laneSeed;
        float itemTimer;
        float stuckTimer;

        void Awake()
        {
            kart = GetComponent<KartController>();
            laneSeed = Random.value * 100f;
            itemTimer = Random.Range(1f, 4f);
        }

        void Update()
        {
            RaceManager race = RaceManager.Instance;
            if (race == null || kart.Progress.Index < 0) return;
            Track track = race.Track;
            int idx = kart.Progress.Index;
            float dt = Time.deltaTime;
            float speed = kart.ForwardSpeed;

            // ---- steering towards a look-ahead point on a wandering lane
            float lane = (Mathf.PerlinNoise(laneSeed, Time.time * 0.15f) - 0.5f) * 10f;
            float lookAhead = Mathf.Lerp(7f, 16f, Mathf.Clamp01(speed / kart.maxSpeed));
            int targetIdx = track.Advance(idx, lookAhead);
            Vector3 target = track.Points[targetIdx] + track.Right(targetIdx) * lane;
            target = AvoidBananas(target);

            Vector3 local = transform.InverseTransformPoint(target);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            kart.steer = Mathf.Clamp(angle / 22f, -1f, 1f);

            // ---- throttle
            kart.throttle = Mathf.Abs(angle) > 60f ? 0.4f : 1f;

            // ---- drifting through sharp corners
            int farIdx = track.Advance(idx, 30f);
            float curve = Vector3.SignedAngle(track.Tangents[idx], track.Tangents[farIdx], Vector3.up);
            if (!kart.IsDrifting)
                kart.driftHeld = Mathf.Abs(curve) > 40f && speed > 14f && Mathf.Sign(curve) == Mathf.Sign(angle);
            else
            {
                bool steeringWithDrift = Mathf.Sign(angle) == kart.DriftDirection || Mathf.Abs(angle) < 10f;
                kart.driftHeld = Mathf.Abs(curve) > 12f && steeringWithDrift;
            }

            // ---- rubber banding relative to the player
            KartController player = race.Player;
            float rubber = 1f;
            if (player != null && player != kart && !player.Progress.Finished)
            {
                float gap = player.Progress.RaceDistance - kart.Progress.RaceDistance; // + = we are behind
                rubber = gap > 0f
                    ? Mathf.Lerp(1f, 1.1f, Mathf.Clamp01(gap / 150f))
                    : Mathf.Lerp(1f, 0.92f, Mathf.Clamp01(-gap / 150f));
            }
            kart.speedMultiplier = skill * rubber;

            // ---- items
            if (kart.Item != ItemType.None && kart.RouletteTime <= 0f && kart.controlsEnabled)
            {
                itemTimer -= dt;
                if (itemTimer <= 0f && ShouldUseItem(race, Mathf.Abs(angle)))
                {
                    kart.RequestUseItem();
                    itemTimer = Random.Range(0.8f, 3f);
                }
            }

            // ---- stuck recovery
            if (kart.controlsEnabled && !kart.IsSpinning && Mathf.Abs(speed) < 1.5f)
            {
                stuckTimer += dt;
                if (stuckTimer > 2.5f)
                {
                    stuckTimer = 0f;
                    kart.Respawn();
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        Vector3 AvoidBananas(Vector3 target)
        {
            foreach (Banana b in Banana.Active)
            {
                Vector3 local = transform.InverseTransformPoint(b.transform.position);
                if (local.z > 2f && local.z < 25f && Mathf.Abs(local.x) < 2.5f)
                    target += transform.right * (local.x > 0f ? -4f : 4f);
            }
            return target;
        }

        bool ShouldUseItem(RaceManager race, float absAngle)
        {
            switch (kart.Item)
            {
                case ItemType.Mushroom:
                case ItemType.TripleMushroom:
                case ItemType.Star:
                    return absAngle < 12f;

                case ItemType.RedShell:
                    return kart.Progress.Place > 1;

                case ItemType.GreenShell:
                    foreach (KartController other in race.Karts)
                    {
                        if (other == kart) continue;
                        Vector3 local = transform.InverseTransformPoint(other.transform.position);
                        if (local.z > 3f && local.z < 35f && Mathf.Abs(local.x) < 2.5f) return true;
                    }
                    return itemTimer < -8f; // held it long enough: fire anyway

                case ItemType.Banana:
                    foreach (KartController other in race.Karts)
                    {
                        if (other == kart) continue;
                        Vector3 local = transform.InverseTransformPoint(other.transform.position);
                        if (local.z < -3f && local.z > -20f && Mathf.Abs(local.x) < 3f) return true;
                    }
                    return itemTimer < -10f;
            }
            return false;
        }
    }
}
