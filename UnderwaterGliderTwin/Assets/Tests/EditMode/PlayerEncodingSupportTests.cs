using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class PlayerEncodingSupportTests
    {
        [Test]
        public void ProjectIncludesGbkEncodingAssembliesForPlayerBuild()
        {
            var pluginsPath = Path.Combine(Application.dataPath, "Plugins");

            Assert.That(File.Exists(Path.Combine(pluginsPath, "I18N.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(pluginsPath, "I18N.CJK.dll")), Is.True);
        }
    }
}
