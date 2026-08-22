namespace UnderwaterGliderTwin.UI
{
    public enum UiTextRole
    {
        Title,
        SectionTitle,
        Label,
        Value,
        Button,
        Auxiliary,
        Error
    }

    public readonly struct ResponsiveUiTypographyProfile
    {
        public readonly int titleSize;
        public readonly int sectionTitleSize;
        public readonly int labelSize;
        public readonly int valueSize;
        public readonly int buttonSize;
        public readonly int auxiliarySize;
        public readonly int errorSize;
        public readonly float minimumReadablePixelSize;
        public readonly bool showLowPriorityText;

        public ResponsiveUiTypographyProfile(
            int titleSize,
            int sectionTitleSize,
            int labelSize,
            int valueSize,
            int buttonSize,
            int auxiliarySize,
            int errorSize,
            float minimumReadablePixelSize,
            bool showLowPriorityText)
        {
            this.titleSize = titleSize;
            this.sectionTitleSize = sectionTitleSize;
            this.labelSize = labelSize;
            this.valueSize = valueSize;
            this.buttonSize = buttonSize;
            this.auxiliarySize = auxiliarySize;
            this.errorSize = errorSize;
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
                        titleSize: 22,
                        sectionTitleSize: 20,
                        labelSize: 16,
                        valueSize: 16,
                        buttonSize: 16,
                        auxiliarySize: 16,
                        errorSize: 16,
                        minimumReadablePixelSize: 10f,
                        showLowPriorityText: true);

                case RuntimeUiLayoutMode.CompressedThreeColumn:
                    return new ResponsiveUiTypographyProfile(
                        titleSize: 20,
                        sectionTitleSize: 18,
                        labelSize: 18,
                        valueSize: 18,
                        buttonSize: 18,
                        auxiliarySize: 17,
                        errorSize: 18,
                        minimumReadablePixelSize: 11f,
                        showLowPriorityText: true);

                default:
                    return new ResponsiveUiTypographyProfile(
                        titleSize: 20,
                        sectionTitleSize: 20,
                        labelSize: 20,
                        valueSize: 20,
                        buttonSize: 20,
                        auxiliarySize: 18,
                        errorSize: 20,
                        minimumReadablePixelSize: 11f,
                        showLowPriorityText: false);
            }
        }

        public static int GetLogicalSize(ResponsiveUiTypographyProfile profile, UiTextRole role)
        {
            switch (role)
            {
                case UiTextRole.Title: return profile.titleSize;
                case UiTextRole.SectionTitle: return profile.sectionTitleSize;
                case UiTextRole.Label: return profile.labelSize;
                case UiTextRole.Value: return profile.valueSize;
                case UiTextRole.Button: return profile.buttonSize;
                case UiTextRole.Auxiliary: return profile.auxiliarySize;
                case UiTextRole.Error: return profile.errorSize;
                default: return profile.valueSize;
            }
        }
    }
}
