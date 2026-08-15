using NUnit.Framework;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ResponsiveUiTypographyTests
    {
        [Test]
        public void Typography_DrawerModeKeepsReadableMinimums()
        {
            const float effectiveScale = 0.533f;
            var profile = ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode.Drawer, 1024f, 640f);

            Assert.That(profile.labelSize, Is.GreaterThanOrEqualTo(18));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(profile.labelSize, effectiveScale), Is.GreaterThanOrEqualTo(10f));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(profile.buttonSize, effectiveScale), Is.GreaterThanOrEqualTo(10f));
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
    }
}
