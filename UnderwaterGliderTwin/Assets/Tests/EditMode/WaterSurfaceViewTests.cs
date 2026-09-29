using NUnit.Framework;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class WaterSurfaceViewTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MeshBuilder_CreatesStableGridTopologyAndExpandedBounds()
        {
            var mesh = WaterSurfaceMeshBuilder.Build(100f, 4, 0.5f);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(25));
                Assert.That(mesh.triangles.Length, Is.EqualTo(96));
                Assert.That(mesh.bounds.size.x, Is.EqualTo(100f));
                Assert.That(mesh.bounds.size.y, Is.GreaterThanOrEqualTo(1f));
                Assert.That(mesh.bounds.size.z, Is.EqualTo(100f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MeshBuilder_ResolvesTheInteractionAreaMoreDenselyThanTheFarField()
        {
            const int segments = 100;
            var mesh = WaterSurfaceMeshBuilder.Build(360f, segments, 0.5f);
            try
            {
                var verticesPerSide = segments + 1;
                var center = segments / 2;
                var centerIndex = center * verticesPerSide + center;
                var centerSpacing = mesh.vertices[centerIndex + 1].x - mesh.vertices[centerIndex].x;
                var farSpacing = mesh.vertices[1].x - mesh.vertices[0].x;

                Assert.That(mesh.vertexCount, Is.EqualTo(verticesPerSide * verticesPerSide));
                Assert.That(centerSpacing, Is.LessThan(farSpacing * 0.25f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Build_CreatesWaterRendererAndSupportsVisibilityToggle()
        {
            host = new GameObject("WaterSurfaceTest");
            var view = host.AddComponent<WaterSurfaceView>();

            view.Build(WaterSurfaceSettings.CreateDefault());
            view.SetVisible(false);

            Assert.That(view.SurfaceMesh, Is.Not.Null);
            Assert.That(host.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(view.SurfaceMesh));
            Assert.That(host.GetComponent<MeshRenderer>().sharedMaterial, Is.Not.Null);
            Assert.That(view.IsVisible, Is.False);

            view.SetVisible(true);
            Assert.That(view.IsVisible, Is.True);
        }

        [Test]
        public void SetSimulationTime_MakesSampleSeekableAndRepeatable()
        {
            host = new GameObject("WaterSurfaceTest");
            var view = host.AddComponent<WaterSurfaceView>();
            view.Build(WaterSurfaceSettings.CreateDefault());
            var position = new Vector2(11f, -9f);

            view.SetSimulationTime(2f);
            var first = view.SampleHeight(position);
            view.SetSimulationTime(9f);
            var changed = view.SampleHeight(position);
            view.SetSimulationTime(2f);
            var repeated = view.SampleHeight(position);

            Assert.That(changed, Is.Not.EqualTo(first).Within(0.0001f));
            Assert.That(repeated, Is.EqualTo(first));
        }

        [Test]
        public void SetInteractionTarget_TracksGliderWithoutChangingSampleContract()
        {
            host = new GameObject("WaterSurfaceTest");
            var target = new GameObject("GliderTarget");
            try
            {
                target.transform.position = new Vector3(12f, -3f, 8f);
                var view = host.AddComponent<WaterSurfaceView>();
                view.Build(WaterSurfaceSettings.CreateDefault());
                view.SetInteractionTarget(target.transform);
                view.SetSimulationTime(4f);

                Assert.That(view.InteractionTarget, Is.SameAs(target.transform));
                Assert.That(view.SampleHeight(new Vector2(2f, 3f)), Is.Not.NaN);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
