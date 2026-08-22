using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public enum RuntimeUiLayoutMode
    {
        Drawer,
        CompressedThreeColumn,
        FullThreeColumn
    }

    public static class ResponsiveUiLayoutPolicy
    {
        public static RuntimeUiLayoutMode Resolve(float width, float height, RuntimeUiLayoutMode previousMode)
        {
            if (previousMode == RuntimeUiLayoutMode.Drawer)
            {
                if (width >= 1296f && height >= 656f)
                {
                    return width >= 1616f ? RuntimeUiLayoutMode.FullThreeColumn : RuntimeUiLayoutMode.CompressedThreeColumn;
                }

                return RuntimeUiLayoutMode.Drawer;
            }

            if (previousMode == RuntimeUiLayoutMode.FullThreeColumn)
            {
                if (width < 1280f || height < 624f)
                {
                    return RuntimeUiLayoutMode.Drawer;
                }

                if (width < 1584f || height <= 640f)
                {
                    return RuntimeUiLayoutMode.CompressedThreeColumn;
                }

                return RuntimeUiLayoutMode.FullThreeColumn;
            }

            if (previousMode == RuntimeUiLayoutMode.CompressedThreeColumn)
            {
                if (width < 1280f || height < 624f)
                {
                    return RuntimeUiLayoutMode.Drawer;
                }

                if (width >= 1616f && height >= 656f)
                {
                    return RuntimeUiLayoutMode.FullThreeColumn;
                }

                return RuntimeUiLayoutMode.CompressedThreeColumn;
            }

            if (width < 1280f || height < 624f)
            {
                return RuntimeUiLayoutMode.Drawer;
            }

            if (width >= 1616f && height >= 656f)
            {
                return RuntimeUiLayoutMode.FullThreeColumn;
            }

            return RuntimeUiLayoutMode.CompressedThreeColumn;
        }

        public static float GetEffectiveCanvasScale(float width, float height, Vector2 referenceResolution, float match)
        {
            if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            {
                return 1f;
            }

            var widthScale = width / referenceResolution.x;
            var heightScale = height / referenceResolution.y;
            var matchWidthOrHeight = Mathf.Clamp01(match);
            return Mathf.Pow(widthScale, 1f - matchWidthOrHeight) * Mathf.Pow(heightScale, matchWidthOrHeight);
        }
    }
}
