using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public static class UiReferenceValidator
    {
        public static void Require(
            UnityEngine.Object value,
            Component owner,
            string prefabName,
            string fieldName,
            List<UiReferenceIssue> issues)
        {
            if (value != null || issues == null)
            {
                return;
            }

            var safePrefabName = string.IsNullOrWhiteSpace(prefabName)
                ? (owner != null ? owner.gameObject.name : "<unknown ui>")
                : prefabName;
            var safeFieldName = string.IsNullOrWhiteSpace(fieldName) ? "<unnamed field>" : fieldName;
            var objectPath = owner != null ? GetPath(owner.transform) : "<missing owner>";

            issues.Add(new UiReferenceIssue(
                safePrefabName,
                objectPath,
                safeFieldName,
                "Required UI reference is missing."));
        }

        public static void RequireFields(
            object references,
            Component owner,
            string prefabName,
            string groupPath,
            List<UiReferenceIssue> issues)
        {
            if (references == null)
            {
                Require(null, owner, prefabName, groupPath, issues);
                return;
            }

            var type = references.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in type.GetFields(flags))
            {
                if (field.IsStatic || field.IsInitOnly || field.IsLiteral || field.IsNotSerialized)
                {
                    continue;
                }

                if (field.GetCustomAttribute<OptionalUiReferenceAttribute>() != null)
                {
                    continue;
                }

                var value = field.GetValue(references);
                var fieldPath = string.IsNullOrWhiteSpace(groupPath)
                    ? field.Name
                    : groupPath + "." + field.Name;

                if (value is IUiReferenceGroup nestedGroup)
                {
                    nestedGroup.CollectReferenceIssues(owner, prefabName, fieldPath, issues);
                    continue;
                }

                if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    Require(value as UnityEngine.Object, owner, prefabName, fieldPath, issues);
                }
            }
        }

        public static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing owner>";
            }

            var parts = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }
    }
}
