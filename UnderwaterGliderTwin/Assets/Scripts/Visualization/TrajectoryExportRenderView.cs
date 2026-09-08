using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
            GameObject renderRoot = null;
            GameObject cameraObject = null;
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            yield return null;
            try
            {
                renderRoot = new GameObject("TrajectoryExportSnapshotRoot");
                renderRoot.hideFlags = HideFlags.HideAndDontSave;
                BuildTrajectoryGeometry(renderRoot.transform, snapshot);

                cameraObject = new GameObject("TrajectoryExportSnapshotCamera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                var camera = cameraObject.AddComponent<Camera>();
                ConfigureCamera(camera, snapshot, renderRoot.transform);
                var width = snapshot.Camera != null ? Mathf.Clamp(snapshot.Camera.PixelWidth, 256, 4096) : 1024;
                var height = snapshot.Camera != null ? Mathf.Clamp(snapshot.Camera.PixelHeight, 256, 4096) : 768;
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
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
                RenderTexture.active = null;
                if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); }
                if (cameraObject != null) Destroy(cameraObject);
                if (renderRoot != null) Destroy(renderRoot);
            }

            completed?.Invoke(success, error);
        }

        private static void BuildTrajectoryGeometry(Transform parent, TrajectoryExportSnapshot snapshot)
        {
            var origin = snapshot.Timeline.Frames[0];
            var metersPerLongitude = 111320d * Math.Cos(origin.LatitudeDeg * Math.PI / 180d);
            foreach (var segment in snapshot.Timeline.Segments)
            {
                var frames = snapshot.Timeline.Frames.Where(frame => frame.ProfileSequence == segment.ProfileSequence).ToArray();
                if (frames.Length == 0) continue;
                var lineObject = new GameObject("ProfileSequence-" + segment.ProfileSequence);
                lineObject.hideFlags = HideFlags.HideAndDontSave;
                lineObject.transform.SetParent(parent, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.widthMultiplier = 1.5f;
                line.positionCount = frames.Length;
                var positions = new Vector3[frames.Length];
                for (var i = 0; i < frames.Length; i++)
                {
                    var east = (frames[i].LongitudeDeg - origin.LongitudeDeg) * metersPerLongitude;
                    var north = (frames[i].LatitudeDeg - origin.LatitudeDeg) * 111320d;
                    positions[i] = new Vector3((float)east, -frames[i].DepthM, (float)north);
                }
                line.SetPositions(positions);
                line.sharedMaterial = RuntimeMaterialFactory.Line("TrajectoryExportLine-" + segment.ProfileSequence, new Color(0.15f, 0.85f, 1f, 1f));
            }
        }

        private static void ConfigureCamera(Camera camera, TrajectoryExportSnapshot snapshot, Transform geometry)
        {
            if (snapshot.Camera != null)
            {
                camera.transform.position = snapshot.Camera.Position;
                camera.transform.rotation = snapshot.Camera.Rotation;
                camera.fieldOfView = Mathf.Clamp(snapshot.Camera.FieldOfView, 1f, 179f);
                return;
            }

            var renderers = geometry.GetComponentsInChildren<LineRenderer>();
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            camera.transform.position = bounds.center + new Vector3(0f, Mathf.Max(20f, bounds.size.magnitude), -Mathf.Max(20f, bounds.size.magnitude));
            camera.transform.LookAt(bounds.center);
            camera.fieldOfView = 45f;
        }
    }
}
