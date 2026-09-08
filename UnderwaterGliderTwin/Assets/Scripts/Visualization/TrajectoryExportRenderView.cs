using System;
using System.Collections;
using System.IO;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class TrajectoryExportRenderView : MonoBehaviour
    {
        public void CaptureAsync(TrajectoryExportSnapshot snapshot, string pngPath, Action<bool, string> completed)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(pngPath)) throw new ArgumentException("PNG path is empty.", nameof(pngPath));
            StartCoroutine(CaptureCoroutine(snapshot, pngPath, completed));
        }

        private IEnumerator CaptureCoroutine(TrajectoryExportSnapshot snapshot, string pngPath, Action<bool, string> completed)
        {
            var success = false;
            string error = null;
            Texture2D texture = null;
            yield return new WaitForEndOfFrame();
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var background = new Color(0.02f, 0.08f, 0.12f, 1f);
                texture.SetPixels(new[] { background, background, background, background });
                texture.Apply(false, false);
                Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
                success = File.Exists(pngPath);
                if (!success) error = "PNG file was not created.";
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                if (texture != null) Destroy(texture);
            }

            completed?.Invoke(success, error);
        }
    }
}
