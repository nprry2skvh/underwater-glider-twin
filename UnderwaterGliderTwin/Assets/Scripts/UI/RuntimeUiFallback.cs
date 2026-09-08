using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public static class RuntimeUiFallback
    {
        public static bool AllowRuntimeFallback { get; set; }
        internal static Canvas LegacyCanvas { get; private set; }
        private static GameObject generatedRuntimeRoot;

        internal static void RememberLegacyCanvas(Canvas canvas)
        {
            LegacyCanvas = canvas;
        }

        internal static void RememberGeneratedRuntimeRoot(GameObject root)
        {
            generatedRuntimeRoot = root;
        }

        internal static void CleanupGeneratedRuntimeRoot()
        {
            if (generatedRuntimeRoot == null)
            {
                return;
            }

            // This is only called while rejecting a stale generated root. It
            // must be removed before the next bootstrap validates the scene;
            // deferred Destroy would leave its RuntimeCanvas visible for one
            // more test/frame.
            if (Application.isEditor)
            {
                Object.DestroyImmediate(generatedRuntimeRoot);
            }
            else
            {
                Object.Destroy(generatedRuntimeRoot);
            }
            generatedRuntimeRoot = null;
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
            generatedRuntimeRoot = null;
        }
    }
}
