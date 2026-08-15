using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public static class RuntimeUiFallback
    {
        public static bool AllowRuntimeFallback { get; set; }

        public static void LogFallback(string panelName)
        {
            var safePanelName = string.IsNullOrWhiteSpace(panelName) ? "<unnamed panel>" : panelName;
            Debug.LogWarning($"[UI Fallback] Runtime-generated UI is active: {safePanelName}");
        }

        public static void Reset()
        {
            AllowRuntimeFallback = false;
        }
    }
}
