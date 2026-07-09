using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnderwaterGliderTwin.Editor
{
    public static class CreateMainScene
    {
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("TwinBootstrap");
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 12f, -20f);
            cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        }
    }
}
