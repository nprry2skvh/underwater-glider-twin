using System.Collections.Generic;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class OceanVolumeViewTests
    {
        [Test]
        public void BuildLayerSamples_CreatesOneSampleForEachCurrentLayer()
        {
            var profile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 50f, 0.2f, 0.1f),
                new OceanCurrentLayer(50f, 200f, -0.3f, 0.4f)
            });
            Assert.That(OceanVolumeView.BuildLayerSamples(profile, 200f), Has.Count.EqualTo(2));
        }

        [Test]
        public void BuildLayerSamples_ClampsSamplesToMissionDepth()
        {
            var profile = new OceanCurrentProfile(new[] { new OceanCurrentLayer(0f, 600f, 0.2f, 0f) });
            Assert.That(OceanVolumeView.BuildLayerSamples(profile, 160f)[0].DepthM, Is.LessThanOrEqualTo(160f));
        }

        [Test]
        public void SelectVisualLayerSamples_DownsamplesDenseProfilesWithoutChangingEndpoints()
        {
            var samples = new List<OceanVolumeView.LayerSample>();
            for (var index = 0; index < 37; index++)
            {
                samples.Add(new OceanVolumeView.LayerSample(index * 50f, new Vector2(index, -index)));
            }

            var visualSamples = OceanVolumeView.SelectVisualLayerSamples(samples, 12);

            Assert.That(visualSamples, Has.Count.EqualTo(12));
            Assert.That(visualSamples[0].DepthM, Is.EqualTo(0f));
            Assert.That(visualSamples[11].DepthM, Is.EqualTo(1800f));
        }

        [Test]
        public void BuildCurrentGridPoints_CreatesAStableSquareSamplingGrid()
        {
            var points = OceanVolumeView.BuildCurrentGridPoints(80f, 5);

            Assert.That(points, Has.Count.EqualTo(25));
            Assert.That(points[0].x, Is.EqualTo(-26.6667f).Within(0.01f));
            Assert.That(points[0].z, Is.EqualTo(-26.6667f).Within(0.01f));
            Assert.That(points[24].x, Is.EqualTo(26.6667f).Within(0.01f));
            Assert.That(points[24].z, Is.EqualTo(26.6667f).Within(0.01f));
        }

        [Test]
        public void BuildVolumeEdges_CreatesAllTwelveWireframeEdges()
        {
            var edges = OceanVolumeView.BuildVolumeEdges(80f, 160f);

            Assert.That(edges.Count, Is.EqualTo(12));
            Assert.That(edges[0].Start.y, Is.EqualTo(0f));
            Assert.That(edges[0].End.y, Is.EqualTo(0f));
        }

        [Test]
        public void CalculateVisualDepthScale_CompressesDeepMissionsToAReadableVolume()
        {
            Assert.That(OceanVolumeView.CalculateVisualDepthScale(1600f, 0.05f), Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(OceanVolumeView.CalculateVisualDepthScale(160f, 0.05f), Is.EqualTo(0.075f).Within(0.0001f));
        }

        [Test]
        public void WaterVolumeSurface_UsesADeeperAndMoreTransparentPresentation()
        {
            Assert.That(OceanVolumeView.CalculateVisualVolumeDepth(100f, 0.075f), Is.EqualTo(7.5f).Within(0.001f));
            Assert.That(OceanVolumeView.WaterFloorColor.a, Is.LessThanOrEqualTo(0.35f));
        }

        [Test]
        public void BuildCurrentArrowGeometry_SeparatesDirectionShaftAndArrowHead()
        {
            var geometry = OceanVolumeView.BuildCurrentArrowGeometry(new Vector2(0.6f, 0.8f));

            Assert.That(geometry.Direction.x, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(geometry.Direction.z, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(geometry.ShaftLength, Is.GreaterThan(0f));
            Assert.That(geometry.HeadLength, Is.GreaterThan(0f));
            Assert.That(geometry.TotalLength, Is.EqualTo(geometry.ShaftLength + geometry.HeadLength).Within(0.001f));
        }

        [Test]
        public void BuildCurrentArrowGeometry_KeepsLowSpeedVectorsVisible()
        {
            var geometry = OceanVolumeView.BuildCurrentArrowGeometry(new Vector2(0.3f, -0.2f));

            Assert.That(geometry.TotalLength, Is.GreaterThanOrEqualTo(3f));
            Assert.That(geometry.HeadLength, Is.GreaterThanOrEqualTo(0.8f));
            Assert.That(geometry.ShaftRadius, Is.LessThanOrEqualTo(0.08f));
            Assert.That(geometry.HeadRadius, Is.LessThanOrEqualTo(0.24f));
        }

        [Test]
        public void Initialize_BuildsAVisibleArrowHeadForEachGridSample()
        {
            var root = new GameObject("OceanVolumeTest");
            var view = root.AddComponent<OceanVolumeView>();
            var profile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 100f, 0.3f, -0.2f)
            });

            try
            {
                view.Initialize(profile, 100f, 20f, 0.05f);

                var arrowHeads = root.GetComponentsInChildren<MeshFilter>();
                var arrowHeadCount = 0;
                MeshFilter firstArrowHead = null;
                foreach (var meshFilter in arrowHeads)
                {
                    if (meshFilter.gameObject.name != "OceanCurrentArrowHead")
                    {
                        continue;
                    }

                    arrowHeadCount++;
                    firstArrowHead ??= meshFilter;
                }

                Assert.That(arrowHeadCount, Is.EqualTo(25));
                Assert.That(firstArrowHead.sharedMesh.vertexCount, Is.GreaterThanOrEqualTo(9));
                Assert.That(firstArrowHead.GetComponent<MeshRenderer>().sharedMaterial.name, Does.Contain("OceanCurrentArrowHeadMaterial"));
                Assert.That(firstArrowHead.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_EmissionColor").r, Is.GreaterThan(0.8f));
                Assert.That(firstArrowHead.transform.lossyScale.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(firstArrowHead.transform.lossyScale.y, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
