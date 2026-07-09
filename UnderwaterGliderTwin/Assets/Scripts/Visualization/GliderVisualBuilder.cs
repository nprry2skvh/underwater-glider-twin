using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public static class GliderVisualBuilder
    {
        public static GameObject Build()
        {
            var root = new GameObject("Glider");

            var bodyMaterial = CreateMaterial("GliderBodyMaterial", new Color(0.63f, 0.71f, 0.74f));
            var wingMaterial = CreateMaterial("GliderWingMaterial", new Color(0.0f, 0.75f, 0.9f));
            var noseMaterial = CreateMaterial("GliderNoseMaterial", new Color(0.9f, 0.12f, 0.08f));

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.9f, 1.9f, 0.9f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            var leftWing = CreateWing("LeftWing", new Vector3(-1.35f, 0f, 0f), new Vector3(1.8f, 0.08f, 0.42f), wingMaterial);
            leftWing.transform.SetParent(root.transform, false);

            var rightWing = CreateWing("RightWing", new Vector3(1.35f, 0f, 0f), new Vector3(1.8f, 0.08f, 0.42f), wingMaterial);
            rightWing.transform.SetParent(root.transform, false);

            var tail = CreateWing("TailPlane", new Vector3(0f, 0f, -1.65f), new Vector3(1.1f, 0.07f, 0.28f), wingMaterial);
            tail.transform.SetParent(root.transform, false);

            var verticalTail = CreateWing("VerticalTail", new Vector3(0f, 0.45f, -1.55f), new Vector3(0.08f, 0.8f, 0.25f), wingMaterial);
            verticalTail.transform.SetParent(root.transform, false);

            var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "NoseMarker";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 1.95f);
            nose.transform.localScale = Vector3.one * 0.28f;
            nose.GetComponent<Renderer>().sharedMaterial = noseMaterial;

            return root;
        }

        private static GameObject CreateWing(string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = name;
            wing.transform.localPosition = localPosition;
            wing.transform.localScale = localScale;
            wing.GetComponent<Renderer>().sharedMaterial = material;
            return wing;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard"))
            {
                name = name,
                color = color
            };
            return material;
        }
    }
}
