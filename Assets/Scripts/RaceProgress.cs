using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Tracks a kart's position along the circuit: nearest centre-line sample, laps
    /// (counted as signed crossings of the start line) and total race distance used for ranking.
    /// </summary>
    public class RaceProgress : MonoBehaviour
    {
        public int Index { get; private set; } = -1;
        public int Lap { get; private set; }            // 0 before first crossing, TotalLaps+1 once finished
        public int Place { get; set; }
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }
        public float RaceDistance { get; private set; }
        public bool WrongWay { get; private set; }

        /// <summary>Lap number to show on the HUD (1..TotalLaps).</summary>
        public int DisplayLap => Mathf.Clamp(Lap, 1, RaceManager.Instance.TotalLaps);

        KartController kart;
        float wrongWayTimer;

        void Awake()
        {
            kart = GetComponent<KartController>();
        }

        void FixedUpdate()
        {
            RaceManager race = RaceManager.Instance;
            if (race == null || race.Track == null) return;
            Track track = race.Track;
            int n = track.Count;

            int prev = Index;
            Index = track.FindNearest(transform.position, prev, 30);

            if (prev >= 0 && !Finished)
            {
                bool crossedForward = prev > n * 3 / 4 && Index < n / 4;
                bool crossedBackward = prev < n / 4 && Index > n * 3 / 4;
                if (crossedForward)
                {
                    Lap++;
                    if (Lap > race.TotalLaps)
                    {
                        Finished = true;
                        FinishTime = race.RaceTime;
                        race.OnKartFinished(kart);
                    }
                    else if (Lap > 1)
                    {
                        race.OnLapCompleted(kart, Lap);
                    }
                }
                else if (crossedBackward)
                {
                    Lap--;
                }
            }

            RaceDistance = Lap * track.Length + track.Distances[Index];

            // Wrong-way detection (only meaningful while moving).
            float dot = Vector3.Dot(transform.forward, track.Tangents[Index]);
            Vector3 v = kart.Body.velocity;
            bool movingBackward = new Vector3(v.x, 0f, v.z).magnitude > 4f && dot < -0.3f;
            wrongWayTimer = movingBackward ? wrongWayTimer + Time.fixedDeltaTime : 0f;
            WrongWay = wrongWayTimer > 1f;
        }

        public void ResetIndex(int index) => Index = index;
    }
}
