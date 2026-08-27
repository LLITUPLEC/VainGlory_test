using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ashfold.Editor
{
    [InitializeOnLoad]
    static class PlayFromBootScene
    {
        const string Path = "Assets/_Game/Scenes/Boot.unity";

        static PlayFromBootScene()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);
            if (scene != null)
                EditorSceneManager.playModeStartScene = scene;
        }
    }
}
