using UnityEngine;

namespace KartRacer
{
    /// <summary>Marks a ground collider as road (full speed) or off-road (slows karts down).</summary>
    public class TrackSurface : MonoBehaviour
    {
        public bool isRoad = true;
    }
}
