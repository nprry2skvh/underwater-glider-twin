using UnityEngine;
using UnderwaterGliderTwin.Playback;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class UnderwaterEnvironmentBuilder : MonoBehaviour
    {
        private ParticleSystem marineSnow;
        private Material marineSnowMaterial;
        private WaterSurfaceView waterSurface;

        public bool ParticlesEnabled => marineSnow != null && marineSnow.gameObject.activeSelf;
        public bool WaterEnabled => waterSurface != null && waterSurface.IsVisible;
        public WaterSurfaceView WaterSurface => waterSurface;

        public void Build()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = new Color(0.03f, 0.24f, 0.31f);
            RenderSettings.ambientLight = new Color(0.02f, 0.08f, 0.12f);

            CreateSeabed();
            CreateDirectionalLight();
            CreateMarineSnow();
            CreateWaterSurface();
        }

        public void Initialize(PlaybackController playbackController, Camera targetCamera, Transform surfaceInteractionTarget = null)
        {
            waterSurface?.Bind(playbackController, targetCamera, surfaceInteractionTarget);
        }

        public void SetFogEnabled(bool enabled)
        {
            RenderSettings.fog = enabled;
        }

        public void SetParticlesEnabled(bool enabled)
        {
            if (marineSnow != null)
            {
                marineSnow.gameObject.SetActive(enabled);
            }
        }

        public void SetWaterEnabled(bool enabled)
        {
            waterSurface?.SetVisible(enabled);
        }

        private void CreateSeabed()
        {
            var seabed = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seabed.name = "Seabed";
            seabed.transform.SetParent(transform, false);
            seabed.transform.position = new Vector3(0f, -70f, 0f);
            seabed.transform.localScale = new Vector3(80f, 1f, 80f);
            seabed.GetComponent<Renderer>().sharedMaterial = RuntimeMaterialFactory.Opaque("SeabedMaterial", new Color(0.08f, 0.16f, 0.15f));
        }

        private void CreateDirectionalLight()
        {
            var lightObject = new GameObject("FilteredSunlight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            light.color = new Color(0.66f, 0.92f, 1f);
        }

        private void CreateMarineSnow()
        {
            var snowObject = new GameObject("MarineSnow");
            snowObject.transform.SetParent(transform, false);
            snowObject.transform.position = new Vector3(0f, 8f, 0f);
            marineSnow = snowObject.AddComponent<ParticleSystem>();

            var main = marineSnow.main;
            main.startLifetime = 18f;
            main.startSpeed = 0.25f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.68f, 0.84f, 0.88f, 0.22f);
            main.maxParticles = 450;

            var emission = marineSnow.emission;
            emission.rateOverTime = 18f;

            var shape = marineSnow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 18f, 60f);

            marineSnowMaterial = RuntimeMaterialFactory.Line(
                "Marine Snow Material",
                new Color(0.68f, 0.84f, 0.88f, 0.22f));
            var particleRenderer = marineSnow.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = marineSnowMaterial;
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(marineSnowMaterial);
            marineSnowMaterial = null;
        }

        private void CreateWaterSurface()
        {
            var waterObject = new GameObject("WaterSurface");
            waterObject.transform.SetParent(transform, false);
            waterSurface = waterObject.AddComponent<WaterSurfaceView>();
            waterSurface.Build(WaterSurfaceSettings.CreateDefault());
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
