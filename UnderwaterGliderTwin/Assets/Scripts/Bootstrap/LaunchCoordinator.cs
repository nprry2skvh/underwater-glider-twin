using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class LaunchCoordinator
    {
        public const string MainSceneName = "Main";
        public const string LastCsvPlayerPrefsKey = "UnderwaterGliderTwin.LastSuccessfulCsv";
        private readonly Action<string> loadScene;

        public LaunchCoordinator(Action<string> sceneLoader = null)
        {
            loadScene = sceneLoader ?? SceneManager.LoadScene;
        }

        public bool ApplyAndLaunch(LaunchRequest request)
        {
            if (request == null || request.HasErrors || request.Mode == LaunchMode.Welcome) return false;
            if (request.Mode == LaunchMode.Simulation)
                RuntimeDataSourceState.UseSimulation(request.SimulationProfile);
            else
            {
                if (!File.Exists(request.CsvPath)) return false;
                RuntimePathResolver.SetCsvPathOverride(request.CsvPath);
            }
            loadScene(MainSceneName);
            return true;
        }

        public LaunchRequest CreateCsvRequest(string path)
        {
            return LaunchRequestParser.Parse(new[] { "--csv", path ?? string.Empty }, string.Empty, SimulationProfile.Default);
        }

        public LaunchRequest CreateSimulationRequest() =>
            new LaunchRequest(LaunchMode.Simulation, string.Empty, SimulationProfile.Default, null);

        public static string GetLastSuccessfulCsvPath()
        {
            var path = PlayerPrefs.GetString(LastCsvPlayerPrefsKey, string.Empty);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? Path.GetFullPath(path) : string.Empty;
        }

        public static void SaveSuccessfulCsvPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            PlayerPrefs.SetString(LastCsvPlayerPrefsKey, Path.GetFullPath(path));
            PlayerPrefs.Save();
        }
    }
}
