using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ResponsiveUiLayoutPolicyTests
    {
        [TestCase(1920f, 1080f, RuntimeUiLayoutMode.FullThreeColumn)]
        [TestCase(1700f, 640f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1616f, 656f, RuntimeUiLayoutMode.FullThreeColumn)]
        [TestCase(1615f, 655f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1280f, 720f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1280f, 623f, RuntimeUiLayoutMode.Drawer)]
        [TestCase(1279f, 720f, RuntimeUiLayoutMode.Drawer)]
        [TestCase(1024f, 640f, RuntimeUiLayoutMode.Drawer)]
        public void Resolve_HandlesEveryDimensionBand(float width, float height, RuntimeUiLayoutMode expected)
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(width, height, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_UsesSpecifiedHysteresisRules()
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1296f, 656f, RuntimeUiLayoutMode.Drawer), Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1263f, 620f, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(RuntimeUiLayoutMode.Drawer));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1700f, 640f, RuntimeUiLayoutMode.FullThreeColumn), Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1616f, 900f, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(RuntimeUiLayoutMode.FullThreeColumn));
        }
    }
}
