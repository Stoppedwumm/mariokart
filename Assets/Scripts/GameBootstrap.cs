using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Starts the game automatically when entering Play mode in any scene (including an empty,
    /// untitled one), unless the scene already contains a <see cref="RaceManager"/>.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindObjectOfType<RaceManager>() != null) return;
            new GameObject("Race").AddComponent<RaceManager>();
        }
    }
}
