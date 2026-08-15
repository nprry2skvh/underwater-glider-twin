using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public interface IUiReferenceProvider
    {
        void CollectReferenceIssues(List<UiReferenceIssue> issues);
    }

    public interface IUiReferenceGroup
    {
        void CollectReferenceIssues(Component owner, string prefabName, string groupPath, List<UiReferenceIssue> issues);
    }
}
