namespace UnderwaterGliderTwin.UI
{
    public readonly struct UiReferenceIssue
    {
        public UiReferenceIssue(string prefabName, string objectPath, string fieldName, string message)
        {
            PrefabName = prefabName;
            ObjectPath = objectPath;
            FieldName = fieldName;
            Message = message;
        }

        public string PrefabName { get; }
        public string ObjectPath { get; }
        public string FieldName { get; }
        public string Message { get; }

        public override string ToString()
        {
            return $"{PrefabName}: {ObjectPath} -> {FieldName}: {Message}";
        }
    }
}
