using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnderwaterGliderTwin.Editor;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class BuildSceneSettingsTests
    {
        [Test]
        public void BuildSettings_StartDirectlyInMainScene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(scenes[0].path, Is.EqualTo("Assets/Scenes/Main.unity"));
            Assert.That(scenes.Any(scene => scene.path == "Assets/Scenes/Welcome.unity"), Is.False);
            Assert.That(File.Exists(Path.Combine(Application.dataPath, "Scenes", "Welcome.unity")), Is.False);
            Assert.That(BuildWindows.BuildScenePaths, Is.EqualTo(new[] { "Assets/Scenes/Main.unity" }));
        }
    }
}
