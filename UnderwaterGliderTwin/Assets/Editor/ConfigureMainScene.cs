using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnderwaterGliderTwin.Bootstrap;

namespace UnderwaterGliderTwin.Editor
{
    public static class ConfigureMainScene
    {
        public static void Configure()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var bootstrap = GameObject.Find("TwinBootstrap") ?? new GameObject("TwinBootstrap");
            if (bootstrap.GetComponent<TwinBootstrap>() == null)
            {
                bootstrap.AddComponent<TwinBootstrap>();
            }

            EditorSceneManager.SaveScene(scene);
        }
    }
}
