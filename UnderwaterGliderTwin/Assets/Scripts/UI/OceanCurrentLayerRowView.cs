using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed class OceanCurrentLayerRowView : MonoBehaviour, IUiReferenceProvider
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text depthRangeText;
        [SerializeField] private Text velocityText;
        [SerializeField] private Button editButton;
        [SerializeField] private Button removeButton;

        public void Bind(int index, OceanCurrentLayer layer, Action<int> onEdit, Action<int> onRemove)
        {
            var issues = new List<UiReferenceIssue>();
            CollectReferenceIssues(issues);
            if (issues.Count > 0 || layer == null)
            {
                foreach (var issue in issues)
                {
                    Debug.LogError(issue.ToString(), this);
                }

                return;
            }

            titleText.text = $"Layer {index + 1}";
            depthRangeText.text = $"{layer.MinDepthM:0}-{layer.MaxDepthM:0} m";
            velocityText.text = $"E {layer.EastwardMps:0.00} / N {layer.NorthwardMps:0.00} m/s";
            editButton.onClick.RemoveAllListeners();
            removeButton.onClick.RemoveAllListeners();
            editButton.onClick.AddListener(() => onEdit?.Invoke(index));
            removeButton.onClick.AddListener(() => onRemove?.Invoke(index));
        }

        public void CollectReferenceIssues(List<UiReferenceIssue> issues)
        {
            UiReferenceValidator.Require(titleText, this, "OceanCurrentLayerRow.prefab", "titleText", issues);
            UiReferenceValidator.Require(depthRangeText, this, "OceanCurrentLayerRow.prefab", "depthRangeText", issues);
            UiReferenceValidator.Require(velocityText, this, "OceanCurrentLayerRow.prefab", "velocityText", issues);
            UiReferenceValidator.Require(editButton, this, "OceanCurrentLayerRow.prefab", "editButton", issues);
            UiReferenceValidator.Require(removeButton, this, "OceanCurrentLayerRow.prefab", "removeButton", issues);
        }
    }
}
