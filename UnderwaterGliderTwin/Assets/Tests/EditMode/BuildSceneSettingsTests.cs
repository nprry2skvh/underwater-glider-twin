using NUnit.Framework;
using UnityEditor;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class BuildSceneSettingsTests
    {
        [Test]
        public void BuildSettings_UseWelcomeThenMain()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(scenes[0].path, Is.EqualTo("Assets/Scenes/Welcome.unity"));
            Assert.That(scenes[1].path, Is.EqualTo("Assets/Scenes/Main.unity"));
        }
    }
}
