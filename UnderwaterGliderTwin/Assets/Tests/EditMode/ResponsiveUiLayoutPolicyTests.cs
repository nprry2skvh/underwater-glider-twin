using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ResponsiveUiLayoutPolicyTests
    {
        [TestCase(1920f, 1080f, RuntimeUiLayoutMode.FullThreeColumn)]
        [TestCase(1616f, 900f, RuntimeUiLayoutMode.FullThreeColumn)]
        [TestCase(1584f, 900f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1600f, 900f, RuntimeUiLayoutMode.FullThreeColumn)]
        [TestCase(1366f, 768f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1280f, 720f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1279f, 720f, RuntimeUiLayoutMode.Drawer)]
        [TestCase(1024f, 640f, RuntimeUiLayoutMode.Drawer)]
        [TestCase(1280f, 623f, RuntimeUiLayoutMode.Drawer)]
        [TestCase(1280f, 624f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1280f, 655f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1280f, 656f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1000f, 620f, RuntimeUiLayoutMode.Drawer)]
        public void Resolve_UsesWindowDimensions(float width, float height, RuntimeUiLayoutMode expected)
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(width, height, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_UsesHysteresisAroundDrawerBoundary()
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1280f, 720f, RuntimeUiLayoutMode.Drawer), Is.EqualTo(RuntimeUiLayoutMode.Drawer));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1296f, 720f, RuntimeUiLayoutMode.Drawer), Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1264f, 720f, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(RuntimeUiLayoutMode.Drawer));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1296f, 655f, RuntimeUiLayoutMode.Drawer), Is.EqualTo(RuntimeUiLayoutMode.Drawer));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1296f, 656f, RuntimeUiLayoutMode.Drawer), Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1279f, 720f, RuntimeUiLayoutMode.FullThreeColumn), Is.EqualTo(RuntimeUiLayoutMode.Drawer));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1584f, 900f, RuntimeUiLayoutMode.FullThreeColumn), Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1616f, 900f, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(RuntimeUiLayoutMode.FullThreeColumn));
        }
    }
}
