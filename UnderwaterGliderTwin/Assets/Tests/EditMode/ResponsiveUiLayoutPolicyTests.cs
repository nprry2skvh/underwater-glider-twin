using NUnit.Framework;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ResponsiveUiLayoutPolicyTests
    {
        [TestCase(1280f, 720f)]
        [TestCase(1366f, 768f)]
        [TestCase(1399f, 624f)]
        [TestCase(1400f, 623f)]
        public void Resolve_EntersDrawerBelowTheCompressedFloor(float width, float height)
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(width, height, RuntimeUiLayoutMode.CompressedThreeColumn),
                Is.EqualTo(RuntimeUiLayoutMode.Drawer));
        }

        [TestCase(1400f, 624f)]
        [TestCase(1456f, 656f)]
        [TestCase(1599f, 656f)]
        [TestCase(1600f, 656f)]
        [TestCase(1615f, 656f)]
        public void Resolve_KeepsCompressedWithinTheCompressedBand(float width, float height)
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(width, height, RuntimeUiLayoutMode.CompressedThreeColumn),
                Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
        }

        [TestCase(RuntimeUiLayoutMode.Drawer)]
        [TestCase(RuntimeUiLayoutMode.CompressedThreeColumn)]
        public void Resolve_EntersFullAt1616By656(RuntimeUiLayoutMode previousMode)
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1616f, 656f, previousMode),
                Is.EqualTo(RuntimeUiLayoutMode.FullThreeColumn));
        }

        [Test]
        public void Resolve_UsesDrawerExitHysteresis()
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1455f, 656f, RuntimeUiLayoutMode.Drawer),
                Is.EqualTo(RuntimeUiLayoutMode.Drawer));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1456f, 655f, RuntimeUiLayoutMode.Drawer),
                Is.EqualTo(RuntimeUiLayoutMode.Drawer));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1456f, 656f, RuntimeUiLayoutMode.Drawer),
                Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
        }

        [Test]
        public void Resolve_UsesFullExitHysteresis()
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1600f, 640f, RuntimeUiLayoutMode.FullThreeColumn),
                Is.EqualTo(RuntimeUiLayoutMode.FullThreeColumn));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1599f, 656f, RuntimeUiLayoutMode.FullThreeColumn),
                Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(1600f, 639f, RuntimeUiLayoutMode.FullThreeColumn),
                Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
        }

        [TestCase(1400f, 623f, RuntimeUiLayoutMode.Drawer)]
        [TestCase(1400f, 624f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1600f, 639f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1600f, 640f, RuntimeUiLayoutMode.FullThreeColumn)]
        [TestCase(1616f, 655f, RuntimeUiLayoutMode.CompressedThreeColumn)]
        [TestCase(1616f, 656f, RuntimeUiLayoutMode.FullThreeColumn)]
        public void Resolve_HandlesHeightBoundaries(float width, float height, RuntimeUiLayoutMode expected)
        {
            Assert.That(ResponsiveUiLayoutPolicy.Resolve(width, height, RuntimeUiLayoutMode.FullThreeColumn), Is.EqualTo(expected));
        }
    }
}
