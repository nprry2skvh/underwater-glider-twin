using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ResponsiveUiTypographyTests
    {
        [Test]
        public void Typography_DrawerModeKeepsReadableMinimums()
        {
            var effectiveScale = ResponsiveUiLayoutPolicy.GetEffectiveCanvasScale(1024f, 640f, new Vector2(1920f, 1080f), 0.5f);
            var profile = ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode.Drawer, 1024f, 640f);

            Assert.That(profile.labelSize, Is.GreaterThanOrEqualTo(18));
            Assert.That(profile.minimumReadablePixelSize, Is.EqualTo(11f));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(profile.labelSize, effectiveScale), Is.GreaterThanOrEqualTo(11f));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(profile.buttonSize, effectiveScale), Is.GreaterThanOrEqualTo(11f));
            Assert.That(profile.showLowPriorityText, Is.False);
        }

        [Test]
        public void Typography_DesktopShowsAllPriorityLevels()
        {
            var profile = ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode.FullThreeColumn, 1920f, 1080f);

            Assert.That(profile.labelSize, Is.GreaterThanOrEqualTo(16));
            Assert.That(profile.valueSize, Is.GreaterThanOrEqualTo(16));
            Assert.That(profile.minimumReadablePixelSize, Is.EqualTo(10f));
            Assert.That(profile.showLowPriorityText, Is.True);
        }

        [Test]
        public void Typography_CompressedModeKeepsReadableMinimumsAtLowerBound()
        {
            var effectiveScale = ResponsiveUiLayoutPolicy.GetEffectiveCanvasScale(1280f, 624f, new Vector2(1920f, 1080f), 0.5f);
            var profile = ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode.CompressedThreeColumn, 1280f, 624f);

            Assert.That(ResponsiveUiTypography.GetActualPixelSize(profile.labelSize, effectiveScale), Is.GreaterThanOrEqualTo(11f));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(profile.valueSize, effectiveScale), Is.GreaterThanOrEqualTo(11f));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(profile.buttonSize, effectiveScale), Is.GreaterThanOrEqualTo(11f));
            Assert.That(profile.minimumReadablePixelSize, Is.EqualTo(11f));
        }
    }
}
