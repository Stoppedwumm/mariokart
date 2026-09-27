using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Green shells fly straight and ricochet off walls. Red shells follow the track and
    /// home in on the kart one place ahead of the thrower. Hitting a kart spins it out.
    /// Movement is done manually (sphere casts against walls) so shells never get stuck.
    /// </summary>
    public class Shell : MonoBehaviour
    {
        const float Radius = 0.45f;
        const float HoverHeight = 0.5f;

        KartController owner;
        KartController target;
        bool homing;
        Vector3 dir;
        float speed;
        float age;
        int bounces;
        int trackIndex;

        public static void Spawn(KartController owner, bool homing)
        {
            var go = new GameObject(homing ? "RedShell" : "GreenShell");
            go.transform.SetParent(RaceManager.Instance.transform, false);
            go.transform.position = owner.transform.position + owner.transform.forward * 2.2f + Vector3.up * HoverHeight;

            Color c = homing ? new Color(0.9f, 0.1f, 0.1f) : new Color(0.1f, 0.75f, 0.2f);
            Factory.Part(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.9f, 0.6f, 0.9f),
                Factory.Mat(c, 0.8f));
            Factory.Part(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.95f, 0.08f, 0.95f),
                Factory.Mat(Color.white, 0.5f));

            var shell = go.AddComponent<Shell>();
            shell.owner = owner;
            shell.homing = homing;
            shell.dir = owner.transform.forward;
            shell.speed = Mathf.Max(45f, owner.ForwardSpeed + 18f);
            shell.trackIndex = owner.Progress.Index;
            if (homing) shell.target = RaceManager.Instance.KartAhead(owner);
        }

        void FixedUpdate()
        {
            RaceManager race = RaceManager.Instance;
            Track track = race.Track;
            float dt = Time.fixedDeltaTime;
            age += dt;
            if (age > 12f) { Destroy(gameObject); return; }

            Vector3 pos = transform.position;
            trackIndex = track.FindNearest(pos, trackIndex, 20);

            if (homing)
            {
                Vector3 aim;
                if (target != null && (target.transform.position - pos).sqrMagnitude < 30f * 30f)
                    aim = target.transform.position;
                else
                    aim = track.Points[track.Advance(trackIndex, 10f)];
                Vector3 desired = aim - pos;
                desired.y = 0f;
                if (desired.sqrMagnitude > 0.01f)
                    dir = Vector3.RotateTowards(dir, desired.normalized, 5f * dt, 0f);
            }

            // Ricochet off walls.
            float step = speed * dt;
            RaycastHit[] hits = Physics.SphereCastAll(pos, Radius, dir, step, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            RaycastHit wallHit = default;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.GetComponent<TrackWall>() == null) continue;
                if (h.distance < nearest) { nearest = h.distance; wallHit = h; }
            }
            if (nearest < float.MaxValue)
            {
                if (homing || ++bounces > 6) { Destroy(gameObject); return; }
                Vector3 n = wallHit.normal;
                n.y = 0f;
                if (n.sqrMagnitude < 0.001f) n = -dir;
                dir = Vector3.Reflect(dir, n.normalized);
                step = Mathf.Max(0f, nearest - 0.05f);
            }
            dir.y = 0f;
            dir.Normalize();
            pos += dir * step;

            // Hug the ground.
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 10f, ~0,
                    QueryTriggerInteraction.Ignore) && ground.collider.GetComponent<TrackSurface>() != null)
                pos.y = ground.point.y + HoverHeight;

            transform.position = pos;
            transform.Rotate(0f, 720f * dt, 0f);

            // Hits
            foreach (KartController k in race.Karts)
            {
                if (k == owner && age < 0.6f) continue;
                if ((k.transform.position + Vector3.up * 0.6f - pos).sqrMagnitude < 1.5f * 1.5f)
                {
                    k.SpinOut();
                    Destroy(gameObject);
                    return;
                }
            }
            foreach (Banana b in Banana.Active)
            {
                if ((b.transform.position - pos).sqrMagnitude < 1.3f * 1.3f)
                {
                    Destroy(b.gameObject);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
