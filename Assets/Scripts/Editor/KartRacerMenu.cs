using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KartRacer.EditorTools
{
    /// <summary>Editor helpers: create a ready-to-build race scene.</summary>
    public static class KartRacerMenu
    {
        const string ScenePath = "Assets/Scenes/Race.unity";

        [MenuItem("Kart Racer/Create Race Scene")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Race").AddComponent<RaceManager>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.Refresh();
            Debug.Log($"Kart Racer: saved {ScenePath} and added it to Build Settings. Press Play to race!");
        }
    }
}
