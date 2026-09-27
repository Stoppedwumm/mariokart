using System.Collections.Generic;
using UnityEngine;

namespace KartRacer
{
    /// <summary>A banana peel left on the track. Any kart that drives over it spins out.</summary>
    public class Banana : MonoBehaviour
    {
        public static readonly List<Banana> Active = new List<Banana>();

        KartController owner;
        float age;

        public static void Spawn(KartController owner, Vector3 position)
        {
            // Drop onto the highest ground surface below (ignoring karts).
            float groundY = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 10f, ~0,
                         QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponent<TrackSurface>() != null)
                    groundY = Mathf.Max(groundY, hit.point.y);
            if (!float.IsNegativeInfinity(groundY)) position.y = groundY;

            var go = new GameObject("Banana");
            go.transform.SetParent(RaceManager.Instance.transform, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Material yellow = Factory.Mat(new Color(1f, 0.88f, 0.15f), 0.4f);
            Material brown = Factory.Mat(new Color(0.4f, 0.28f, 0.1f));
            Factory.Part(PrimitiveType.Capsule, go.transform, new Vector3(-0.2f, 0.3f, 0f), new Vector3(0.3f, 0.35f, 0.3f),
                yellow, Quaternion.Euler(0f, 0f, 35f));
            Factory.Part(PrimitiveType.Capsule, go.transform, new Vector3(0.2f, 0.3f, 0f), new Vector3(0.3f, 0.35f, 0.3f),
                yellow, Quaternion.Euler(0f, 0f, -35f));
            Factory.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.08f, 0.1f, 0.08f),
                brown);

            var banana = go.AddComponent<Banana>();
            banana.owner = owner;
        }

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);

        void Update()
        {
            age += Time.deltaTime;
            RaceManager race = RaceManager.Instance;
            if (race == null) return;
            foreach (KartController k in race.Karts)
            {
                if (k == owner && age < 0.8f) continue;
                if ((k.transform.position - transform.position).sqrMagnitude < 1.4f * 1.4f)
                {
                    k.SpinOut();
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
