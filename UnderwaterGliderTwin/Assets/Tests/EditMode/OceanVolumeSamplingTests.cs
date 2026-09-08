using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class OceanVolumeSamplingTests
    {
        [Test]
        public void RebuildCandidateCache_UsesTheFixedTwelveByTwelveByTenBudget()
        {
            var profile = SimulationProfile.Default;
            var cache = new OceanVolumeSamplingCache(profile);

            cache.RebuildCandidateCache(Vector3.zero, new Vector2(24000f, 18000f), 1200f);

            Assert.That(cache.Candidates, Has.Count.EqualTo(1440));
            Assert.That(cache.Candidates[0].DepthM, Is.EqualTo(0f));
            Assert.That(cache.Candidates[cache.Candidates.Count - 1].DepthM, Is.EqualTo(1200f));
        }

        [Test]
        public void RefreshForTime_RequeriesCandidatesWithoutRebuildingCandidateCache()
        {
            var profile = SimulationProfile.Default;
            profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.NetworkPreferred;
            profile.IrregularFieldIdwRadiusKm = 500f;
            profile.OceanCurrentField = new OceanCurrentField(new[]
            {
                new OceanCurrentFieldSample(140d, 15d, 0f, 0f, 0.1f, 0f),
                new OceanCurrentFieldSample(140d, 15d, 0f, 60f, 0.9f, 0f)
            });
            profile.OriginLongitudeDeg = 140d;
            profile.OriginLatitudeDeg = 15d;
            var cache = new OceanVolumeSamplingCache(profile);
            cache.RebuildCandidateCache(Vector3.zero, new Vector2(1f, 1f), 0f);
            var firstCandidate = cache.Candidates[0];
            var resolver = new OceanCurrentResolver(profile);

            cache.RefreshForTime(resolver, 0f, Vector3.positiveInfinity, null);
            var firstSpeed = cache.SampledCandidates[0].Vector.VelocityMps.x;
            cache.RefreshForTime(resolver, 60f, Vector3.positiveInfinity, null);

            Assert.That(cache.Candidates[0].EnuPositionM, Is.EqualTo(firstCandidate.EnuPositionM));
            Assert.That(cache.SampledCandidates[0].Vector.VelocityMps.x, Is.GreaterThan(firstSpeed));
        }

        [Test]
        public void RefreshForCamera_CapsVisibleArrowsAndLeavesSafetyChannelsClear()
        {
            var profile = SimulationProfile.Default;
            profile.GliderClearanceRadiusM = 500f;
            profile.TrajectorySafetyCorridorRadiusM = 750f;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 2000f, 0.4f, 0.1f)
            });
            var cache = new OceanVolumeSamplingCache(profile);
            cache.RebuildCandidateCache(Vector3.zero, new Vector2(24000f, 24000f), 2000f);
            cache.RefreshForTime(new OceanCurrentResolver(profile), 0f, Vector3.zero, new List<Vector3> { new Vector3(3000f, -500f, 0f) });

            cache.RefreshForCamera(new Vector3(0f, 1000f, -1000f));

            Assert.That(cache.VisibleCandidates.Count, Is.LessThanOrEqualTo(OceanVolumeSamplingCache.MaximumVisibleArrowCount));
            foreach (var visible in cache.VisibleCandidates)
            {
                Assert.That(Vector3.Distance(visible.Candidate.EnuPositionM, Vector3.zero), Is.GreaterThanOrEqualTo(profile.GliderClearanceRadiusM));
            }
        }

        [Test]
        public void InstancedArrowRenderer_UsesLegibleArrowScaleForMissionVolume()
        {
            var profile = SimulationProfile.Default;
            profile.GliderClearanceRadiusM = 0f;
            profile.TrajectorySafetyCorridorRadiusM = 0f;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 1000f, 0.25f, -0.12f)
            });
            var cache = new OceanVolumeSamplingCache(profile);
            cache.RebuildCandidateCache(Vector3.zero, new Vector2(24000f, 18000f), 1000f);
            cache.RefreshForTime(new OceanCurrentResolver(profile), 0f, Vector3.positiveInfinity, null);
            cache.RefreshForCamera(Vector3.zero);

            var origin = new TelemetryFrame(0, "origin", 0f, 120d, 25d, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 100f, "", "", 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            var rendererObject = new GameObject("InstancedArrowRendererTest");
            var renderer = rendererObject.AddComponent<OceanCurrentInstancedRenderer>();
            try
            {
                SetPrivateField(renderer, "samplingCache", cache);
                SetPrivateField(renderer, "mapper", new GeoCoordinateMapper(origin, 0.0025f, 0.032f));
                InvokePrivateMethod(renderer, "RebuildInstanceData");

                var matrices = (Matrix4x4[])GetPrivateField(renderer, "matrices");
                Assert.That(matrices[0].GetColumn(0).magnitude, Is.GreaterThanOrEqualTo(0.12f));
                Assert.That(matrices[0].GetColumn(2).magnitude, Is.GreaterThanOrEqualTo(1.5f));
            }
            finally
            {
                Object.DestroyImmediate(rendererObject);
            }
        }

        [Test]
        public void InstancedArrowRenderer_UsesOneMeshSurfaceInsteadOfImmediateCameraSubmission()
        {
            var meshFilter = typeof(OceanCurrentInstancedRenderer)
                .GetField("meshFilter", BindingFlags.Instance | BindingFlags.NonPublic);
            var meshRenderer = typeof(OceanCurrentInstancedRenderer)
                .GetField("meshRenderer", BindingFlags.Instance | BindingFlags.NonPublic);
            var postCameraMethod = typeof(OceanCurrentInstancedRenderer)
                .GetMethod("OnRenderObject", BindingFlags.Instance | BindingFlags.NonPublic);
            var immediateRenderMethod = typeof(OceanCurrentInstancedRenderer)
                .GetMethod("RenderInstances", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(meshRenderer, Is.Not.Null);
            Assert.That(postCameraMethod, Is.Null);
            Assert.That(immediateRenderMethod, Is.Null);
        }

        [Test]
        public void InstancedArrowRenderer_UsesAnEmissiveMaterialColorForDarkWaterVisualization()
        {
            var field = typeof(OceanCurrentInstancedRenderer)
                .GetField("CurrentArrowEmissionColor", BindingFlags.Static | BindingFlags.Public);

            Assert.That(field, Is.Not.Null);
            var emission = (Color)field.GetValue(null);
            Assert.That(Mathf.Max(emission.r, emission.g, emission.b), Is.GreaterThan(1f));
        }

        [Test]
        public void InstancedArrowRenderer_LoadsTheDedicatedInstancedUnlitShader()
        {
            var field = typeof(OceanCurrentInstancedRenderer)
                .GetField("CurrentArrowShaderResourcePath", BindingFlags.Static | BindingFlags.Public);
            if (field == null)
            {
                Assert.Fail("The instanced current shader resource path must be explicit.");
            }

            var shader = Resources.Load<Shader>((string)field.GetValue(null));
            Assert.That(shader, Is.Not.Null);
        }

        [Test]
        public void InstancedArrowRenderer_StoresCombinedMeshVerticesInTheVolumeLocalSpace()
        {
            var method = typeof(OceanCurrentInstancedRenderer)
                .GetMethod("ToMeshLocalVertex", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var volume = new GameObject("CurrentVolume");
            volume.transform.position = new Vector3(18f, 0f, -5f);
            try
            {
                var worldMatrix = Matrix4x4.TRS(volume.transform.position, Quaternion.identity, Vector3.one);
                var local = (Vector3)method.Invoke(null, new object[] { volume.transform, worldMatrix, Vector3.zero });

                Assert.That(local, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(volume);
            }
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            typeof(OceanCurrentInstancedRenderer)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static object GetPrivateField(object target, string name)
        {
            return typeof(OceanCurrentInstancedRenderer)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void InvokePrivateMethod(object target, string name)
        {
            typeof(OceanCurrentInstancedRenderer)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }
    }
}
