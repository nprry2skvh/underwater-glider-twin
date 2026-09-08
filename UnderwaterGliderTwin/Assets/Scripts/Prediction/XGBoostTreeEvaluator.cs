using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    [System.Serializable]
    public sealed class XGBoostTreeModel
    {
        public float base_score;
        public XGBoostTree[] trees;
    }

    [System.Serializable]
    public sealed class XGBoostTree
    {
        public XGBoostTreeNode[] nodes;
    }

    [System.Serializable]
    public sealed class XGBoostTreeNode
    {
        public int feature_index;
        public float threshold;
        public int yes_index;
        public int no_index;
        public int missing_index;
        public float leaf_value;
    }

    public static class XGBoostTreeEvaluator
    {
        public static float Evaluate(XGBoostTreeModel model, float[] features)
        {
            if (model?.trees == null || features == null)
            {
                return float.NaN;
            }

            var total = model.base_score;
            foreach (var tree in model.trees)
            {
                var value = EvaluateTree(tree, features);
                if (float.IsNaN(value))
                {
                    return float.NaN;
                }

                total += value;
            }

            return total;
        }

        private static float EvaluateTree(XGBoostTree tree, float[] features)
        {
            if (tree?.nodes == null || tree.nodes.Length == 0)
            {
                return float.NaN;
            }

            var nodeIndex = 0;
            for (var steps = 0; steps < tree.nodes.Length; steps++)
            {
                if (nodeIndex < 0 || nodeIndex >= tree.nodes.Length)
                {
                    return float.NaN;
                }

                var node = tree.nodes[nodeIndex];
                if (node.feature_index < 0)
                {
                    return node.leaf_value;
                }

                if (node.feature_index >= features.Length)
                {
                    return float.NaN;
                }

                var value = features[node.feature_index];
                nodeIndex = float.IsNaN(value)
                    ? node.missing_index
                    : value < node.threshold ? node.yes_index : node.no_index;
            }

            return float.NaN;
        }
    }
}
