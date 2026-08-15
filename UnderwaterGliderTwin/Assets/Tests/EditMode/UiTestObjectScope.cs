using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class UiTestObjectScope : IDisposable
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        public GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            roots.Add(root);
            return root;
        }

        public void Dispose()
        {
            for (var i = roots.Count - 1; i >= 0; i--)
            {
                if (roots[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }

            roots.Clear();
        }
    }
}
