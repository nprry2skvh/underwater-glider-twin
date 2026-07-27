using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public static class GliderVisualBuilder
    {
        private static readonly Vector2[] HullProfile =
        {
            new Vector2(-2.3f, 0.09f),
            new Vector2(-2.02f, 0.3f),
            new Vector2(-1.35f, 0.47f),
            new Vector2(1.12f, 0.5f),
            new Vector2(1.82f, 0.38f),
            new Vector2(2.16f, 0.08f)
        };

        public static GameObject Build()
        {
            var root = new GameObject("Glider");
            root.transform.localScale = Vector3.one * 0.52f;

            var hullMaterial = RuntimeMaterialFactory.Opaque("GliderHullYellowMaterial", new Color(0.95f, 0.72f, 0.08f));
            var liftingMaterial = RuntimeMaterialFactory.Opaque("GliderCarbonMaterial", new Color(0.13f, 0.18f, 0.2f));
            var detailMaterial = RuntimeMaterialFactory.Opaque("GliderDetailMaterial", new Color(0.1f, 0.12f, 0.13f));
            var topMarkerMaterial = RuntimeMaterialFactory.Opaque("GliderTopMarkerMaterial", new Color(0.15f, 0.9f, 1f));
            var portMarkerMaterial = RuntimeMaterialFactory.Opaque("GliderPortMarkerMaterial", new Color(0.15f, 0.9f, 1f));
            var starboardMarkerMaterial = RuntimeMaterialFactory.Opaque("GliderStarboardMarkerMaterial", new Color(1f, 0.32f, 0.16f));
            var attitudeMaterial = RuntimeMaterialFactory.Line("GliderAttitudeMaterial", new Color(1f, 0.92f, 0.22f, 0.92f));
            hullMaterial.renderQueue = 3102;
            liftingMaterial.renderQueue = 3102;
            detailMaterial.renderQueue = 3102;
            topMarkerMaterial.renderQueue = 3102;
            portMarkerMaterial.renderQueue = 3102;
            starboardMarkerMaterial.renderQueue = 3102;
            attitudeMaterial.renderQueue = 3103;

            CreateTaperedHull("PressureHull", root.transform, hullMaterial);
            CreateSphere("NoseSensorCover", root.transform, new Vector3(0f, -0.12f, 2.13f), new Vector3(0.24f, 0.13f, 0.17f), detailMaterial);
            CreateSphere("LowerBallastFairing", root.transform, new Vector3(0f, -0.46f, 0.12f), new Vector3(0.2f, 0.14f, 0.38f), detailMaterial);
            CreateHullSeam("HullSeamForward", root.transform, 1.36f, detailMaterial);
            CreateHullSeam("HullSeamAft", root.transform, -1.42f, detailMaterial);
            CreateBox("TopAttitudeStripe", root.transform, new Vector3(0f, 0.5f, 0.25f), new Vector3(0.16f, 0.035f, 2.15f), topMarkerMaterial);

            var mainWing = new GameObject("MainWing");
            mainWing.transform.SetParent(root.transform, false);
            CreateWing("PortWing", mainWing.transform, -1f, liftingMaterial);
            CreateWing("StarboardWing", mainWing.transform, 1f, liftingMaterial);
            CreateBox("PortWingTip", mainWing.transform, new Vector3(-2.32f, 0.03f, 0.12f), new Vector3(0.16f, 0.12f, 0.56f), portMarkerMaterial);
            CreateBox("StarboardWingTip", mainWing.transform, new Vector3(2.32f, 0.03f, 0.12f), new Vector3(0.16f, 0.12f, 0.56f), starboardMarkerMaterial);
            CreateControlSurface("PortControlSurface", mainWing.transform, -1f, liftingMaterial);
            CreateControlSurface("StarboardControlSurface", mainWing.transform, 1f, liftingMaterial);

            var tailBoom = new GameObject("TailBoom");
            tailBoom.transform.SetParent(root.transform, false);
            CreateTailBoom(tailBoom.transform, detailMaterial);
            CreateBox("HorizontalTail", tailBoom.transform, new Vector3(0f, 0f, -1.52f), new Vector3(1.45f, 0.07f, 0.34f), liftingMaterial);
            CreateBox("VerticalTail", tailBoom.transform, new Vector3(0f, 0.48f, -1.42f), new Vector3(0.08f, 0.95f, 0.3f), liftingMaterial);

            var rollReference = new GameObject("RollReferenceLine");
            rollReference.transform.SetParent(root.transform, false);
            var line = rollReference.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-2.05f, 0.18f, 0.15f));
            line.SetPosition(1, new Vector3(2.05f, 0.18f, 0.15f));
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 2;
            line.sharedMaterial = attitudeMaterial;

            return root;
        }

        private static void CreateTaperedHull(string name, Transform parent, Material material)
        {
            const int radialSegments = 16;
            var vertices = new List<Vector3>(HullProfile.Length * radialSegments);
            var triangles = new List<int>((HullProfile.Length - 1) * radialSegments * 6);
            var uvs = new List<Vector2>(HullProfile.Length * radialSegments);

            for (var ring = 0; ring < HullProfile.Length; ring++)
            {
                var profilePoint = HullProfile[ring];
                for (var segment = 0; segment < radialSegments; segment++)
                {
                    var angle = segment / (float)radialSegments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * profilePoint.y, Mathf.Sin(angle) * profilePoint.y, profilePoint.x));
                    uvs.Add(new Vector2(segment / (float)radialSegments, ring / (float)(HullProfile.Length - 1)));
                }
            }

            for (var ring = 0; ring < HullProfile.Length - 1; ring++)
            {
                for (var segment = 0; segment < radialSegments; segment++)
                {
                    var nextSegment = (segment + 1) % radialSegments;
                    var current = ring * radialSegments + segment;
                    var next = ring * radialSegments + nextSegment;
                    var nextRing = (ring + 1) * radialSegments + segment;
                    var nextRingNext = (ring + 1) * radialSegments + nextSegment;
                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(nextRing);
                    triangles.Add(next);
                    triangles.Add(nextRingNext);
                    triangles.Add(nextRing);
                }
            }

            var mesh = new Mesh { name = name + "Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var hull = new GameObject(name);
            hull.transform.SetParent(parent, false);
            hull.AddComponent<MeshFilter>().sharedMesh = mesh;
            hull.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateWing(string name, Transform parent, float side, Material material)
        {
            var wing = CreateBox(name, parent, new Vector3(side * 1.28f, 0f, 0.12f), new Vector3(2.05f, 0.075f, 0.5f), material);
            wing.transform.localRotation = Quaternion.Euler(0f, side * 4f, 0f);
        }

        private static void CreateControlSurface(string name, Transform parent, float side, Material material)
        {
            var surface = CreateBox(name, parent, new Vector3(side * 1.38f, -0.02f, -0.16f), new Vector3(1.72f, 0.055f, 0.19f), material);
            surface.transform.localRotation = Quaternion.Euler(0f, side * 4f, 0f);
        }

        private static void CreateTailBoom(Transform parent, Material material)
        {
            var boom = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            boom.name = "TailBoomBody";
            boom.transform.SetParent(parent, false);
            boom.transform.localPosition = new Vector3(0f, 0f, -0.82f);
            boom.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            boom.transform.localScale = new Vector3(0.12f, 0.85f, 0.12f);
            boom.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(boom);
        }

        private static void CreateHullSeam(string name, Transform parent, float z, Material material)
        {
            var seam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seam.name = name;
            seam.transform.SetParent(parent, false);
            seam.transform.localPosition = new Vector3(0f, 0f, z);
            seam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            seam.transform.localScale = new Vector3(0.53f, 0.025f, 0.53f);
            seam.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(seam);
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = position;
            sphere.transform.localScale = scale;
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(sphere);
            return sphere;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(box);
            return box;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            var collider = gameObject.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
