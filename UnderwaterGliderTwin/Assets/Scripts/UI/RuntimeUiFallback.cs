using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public static class RuntimeUiFallback
    {
        public static bool AllowRuntimeFallback { get; set; }
        internal static Canvas LegacyCanvas { get; private set; }

        internal static void RememberLegacyCanvas(Canvas canvas)
        {
            LegacyCanvas = canvas;
        }

        public static void LogFallback(string panelName)
        {
            var safePanelName = string.IsNullOrWhiteSpace(panelName) ? "<unnamed panel>" : panelName;
            Debug.LogWarning($"[UI Fallback] Runtime-generated UI is active: {safePanelName}");
        }

        public static void Reset()
        {
            AllowRuntimeFallback = false;
            LegacyCanvas = null;
        }
    }
}
