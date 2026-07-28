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
        public string LastError { get; private set; } = string.Empty;

        public LaunchCoordinator(Action<string> sceneLoader = null)
        {
            loadScene = sceneLoader ?? SceneManager.LoadScene;
        }

        public bool ApplyAndLaunch(LaunchRequest request)
        {
            LastError = string.Empty;
            if (request == null)
            {
                LastError = "Launch request is missing.";
                return false;
            }

            if (request.HasErrors)
            {
                LastError = string.Join("\n", request.Errors);
                return false;
            }

            if (request.Mode == LaunchMode.Welcome)
            {
                LastError = "No launch mode was selected.";
                return false;
            }

            if (request.Mode == LaunchMode.Simulation)
            {
                RuntimeDataSourceState.UseSimulation(request.SimulationProfile);
            }
            else
            {
                if (!ValidateCsvForLaunch(request.CsvPath)) return false;
                RuntimePathResolver.SetCsvPathOverride(request.CsvPath);
                SaveSuccessfulCsvPath(request.CsvPath);
            }

            loadScene(MainSceneName);
            return true;
        }

        private bool ValidateCsvForLaunch(string csvPath)
        {
            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
            {
                LastError = $"CSV file does not exist: {csvPath}";
                return false;
            }

            var loadResult = new CsvTelemetrySource(csvPath).Load();
            if (loadResult.Errors.Count > 0)
            {
                LastError = string.Join("\n", loadResult.Errors);
                return false;
            }

            if (loadResult.Frames.Count == 0)
            {
                LastError = "CSV did not contain any usable telemetry frames.";
                return false;
            }

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
