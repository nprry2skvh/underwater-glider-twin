using System;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public enum MapProviderMode
    {
        OpenStreetMap,
        Google
    }

    public enum MapStyle
    {
        Street,
        Satellite,
        Terrain
    }

    public sealed class MapProviderSettings
    {
        private const string FileName = "mission-map-settings.json";

        public MapProviderMode Mode { get; set; }
        public MapStyle Style { get; set; }
        public string GoogleApiKey { get; set; } = string.Empty;

        public string MaskedGoogleApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(GoogleApiKey)) return string.Empty;
                if (GoogleApiKey.Length <= 8) return "****";
                return GoogleApiKey.Substring(0, 4) + "..." + GoogleApiKey.Substring(GoogleApiKey.Length - 4);
            }
        }

        public static MapProviderSettings Load()
        {
            var path = Path.Combine(Application.persistentDataPath, FileName);
            if (!File.Exists(path)) return new MapProviderSettings();
            var payload = JsonUtility.FromJson<SettingsPayload>(File.ReadAllText(path));
            if (payload == null) return new MapProviderSettings();
            return new MapProviderSettings { Mode = payload.Mode, Style = payload.Style, GoogleApiKey = payload.GoogleApiKey ?? string.Empty };
        }

        public void Save()
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(Path.Combine(Application.persistentDataPath, FileName), JsonUtility.ToJson(new SettingsPayload
            {
                Mode = Mode,
                Style = Style,
                GoogleApiKey = GoogleApiKey ?? string.Empty
            }));
        }

        [Serializable]
        private sealed class SettingsPayload
        {
            public MapProviderMode Mode;
            public MapStyle Style;
            public string GoogleApiKey;
        }
    }
}
