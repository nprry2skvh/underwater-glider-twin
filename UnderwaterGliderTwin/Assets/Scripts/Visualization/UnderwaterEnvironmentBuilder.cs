using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class UnderwaterEnvironmentBuilder : MonoBehaviour
    {
        private ParticleSystem marineSnow;

        public bool ParticlesEnabled => marineSnow != null && marineSnow.gameObject.activeSelf;

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
            CreateLightBeams();
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

        private void CreateSeabed()
        {
            var seabed = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seabed.name = "Seabed";
            seabed.transform.SetParent(transform, false);
            seabed.transform.position = new Vector3(0f, -70f, 0f);
            seabed.transform.localScale = new Vector3(80f, 1f, 80f);
            seabed.GetComponent<Renderer>().sharedMaterial = CreateMaterial("SeabedMaterial", new Color(0.08f, 0.16f, 0.15f));
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
            main.startSize = 0.08f;
            main.maxParticles = 900;

            var emission = marineSnow.emission;
            emission.rateOverTime = 60f;

            var shape = marineSnow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 18f, 60f);
        }

        private void CreateLightBeams()
        {
            for (var i = 0; i < 3; i++)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.name = $"GodRay{i + 1}";
                beam.transform.SetParent(transform, false);
                beam.transform.position = new Vector3(-14f + i * 14f, 2f, 18f - i * 8f);
                beam.transform.rotation = Quaternion.Euler(18f, 0f, 12f - i * 10f);
                beam.transform.localScale = new Vector3(2.2f, 22f, 2.2f);
                beam.GetComponent<Renderer>().sharedMaterial = CreateTransparentMaterial("GodRayMaterial", new Color(0.55f, 0.9f, 1f, 0.11f));
                var collider = beam.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyObject(collider);
                }
            }
        }

        private static void DestroyObject(Object target)
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

        private static Material CreateMaterial(string name, Color color)
        {
            return new Material(Shader.Find("Standard"))
            {
                name = name,
                color = color
            };
        }

        private static Material CreateTransparentMaterial(string name, Color color)
        {
            var material = CreateMaterial(name, color);
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }
    }
}
