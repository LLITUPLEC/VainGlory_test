using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ashfold.Editor
{
    [InitializeOnLoad]
    static class PlayFromBootScene
    {
        const string Boot = "Assets/_Game/Scenes/Boot.unity";

        static PlayFromBootScene()
        {
            EditorApplication.playModeStateChanged += _ => Refresh();
            EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Refresh();
            Refresh();
        }

        static void Refresh()
        {
            var current = EditorSceneManager.GetActiveScene().path.Replace('\\', '/');
            if (current.EndsWith("Sandbox.unity"))
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Boot);
            if (scene != null)
                EditorSceneManager.playModeStartScene = scene;
        }
    }
}
