namespace UnderwaterGliderTwin.UI
{
    public readonly struct ResponsiveUiTypographyProfile
    {
        public readonly int sectionTitleSize;
        public readonly int labelSize;
        public readonly int valueSize;
        public readonly int buttonSize;
        public readonly int auxiliarySize;
        public readonly float minimumReadablePixelSize;
        public readonly bool showLowPriorityText;

        public ResponsiveUiTypographyProfile(
            int sectionTitleSize,
            int labelSize,
            int valueSize,
            int buttonSize,
            int auxiliarySize,
            float minimumReadablePixelSize,
            bool showLowPriorityText)
        {
            this.sectionTitleSize = sectionTitleSize;
            this.labelSize = labelSize;
            this.valueSize = valueSize;
            this.buttonSize = buttonSize;
            this.auxiliarySize = auxiliarySize;
            this.minimumReadablePixelSize = minimumReadablePixelSize;
            this.showLowPriorityText = showLowPriorityText;
        }
    }

    public static class ResponsiveUiTypography
    {
        public static float GetActualPixelSize(int logicalSize, float effectiveScale)
        {
            return logicalSize * effectiveScale;
        }

        public static ResponsiveUiTypographyProfile ForMode(RuntimeUiLayoutMode mode, float width, float height)
        {
            switch (mode)
            {
                case RuntimeUiLayoutMode.FullThreeColumn:
                    return new ResponsiveUiTypographyProfile(
                        sectionTitleSize: 20,
                        labelSize: 16,
                        valueSize: 16,
                        buttonSize: 16,
                        auxiliarySize: 16,
                        minimumReadablePixelSize: 10f,
                        showLowPriorityText: true);

                case RuntimeUiLayoutMode.CompressedThreeColumn:
                    return new ResponsiveUiTypographyProfile(
                        sectionTitleSize: 18,
                        labelSize: 16,
                        valueSize: 16,
                        buttonSize: 16,
                        auxiliarySize: 16,
                        minimumReadablePixelSize: 10f,
                        showLowPriorityText: true);

                default:
                    return new ResponsiveUiTypographyProfile(
                        sectionTitleSize: 20,
                        labelSize: 20,
                        valueSize: 20,
                        buttonSize: 20,
                        auxiliarySize: 18,
                        minimumReadablePixelSize: 10f,
                        showLowPriorityText: false);
            }
        }
    }
}
