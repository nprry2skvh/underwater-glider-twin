using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    internal static class RuntimeMaterialFactory
    {
        public static Material Opaque(string name, Color color)
        {
            var material = new Material(FindShader("Standard", "Legacy Shaders/Diffuse", "Unlit/Color", "Sprites/Default"))
            {
                name = name,
                color = color
            };
            return material;
        }

        public static Material Transparent(string name, Color color)
        {
            var material = Opaque(name, color);
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

        public static Material Line(string name, Color color)
        {
            var material = new Material(FindShader("Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Unlit/Color"))
            {
                name = name,
                color = color
            };
            return material;
        }

        private static Shader FindShader(params string[] names)
        {
            foreach (var shaderName in names)
            {
                var shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    return shader;
                }
            }

            var fallback = Shader.Find("Hidden/InternalErrorShader");
            if (fallback == null)
            {
                throw new System.InvalidOperationException("No built-in fallback shader is available.");
            }

            return fallback;
        }
    }
}
