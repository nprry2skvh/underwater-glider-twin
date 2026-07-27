# Underwater Glider Digital Twin

Unity digital twin for underwater glider telemetry. It supports CSV replay,
parameter-driven mission simulation, trajectory visualization, and experimental
short-horizon prediction.

## Requirements

- Unity 2022.3.62f3c1
- Windows
- Data file: `2.csv` encoded as GBK
- The helper scripts auto-detect the matching Unity Hub installation.

## Run In Unity

1. Open `UnderwaterGliderTwin` in Unity Hub.
2. Open `Assets/Scenes/Main.unity`.
3. Enter the CSV path in the Mission Configuration panel, or launch with `--csv`.
4. Press Play.

## Run Build

Run:

```powershell
.\Builds\UnderwaterGliderTwin\UnderwaterGliderTwin.exe --csv ".\2.csv"
```

To produce the Windows build again:

```powershell
.\scripts\build-windows.cmd
```

## Controls

- Play/Pause: start or stop replay.
- Speed: choose 0.5x, 1x, 2x, 5x, or 10x.
- Progress: drag to jump through CSV rows.
- Data source: choose a CSV file or generate a parameter-driven mission profile.
- Prediction: select a model and forecast horizon, then start or stop prediction.
- Camera: Follow, Global, Free.
- Visual toggles: trajectory, fog, particles.

## Prediction Status

Prediction models are experimental. The Physics training metadata reports a
very low confidence value and a high worst-case validation error, so
predictions are for visualization and development only. Do not use them for
navigation, safety, or operational decisions.

## Verification

Run Unity EditMode tests from the workspace root:

```powershell
.\scripts\test-editmode.cmd
```

The script writes its XML result and Unity log under `TestResults`, which is
ignored by Git. The current test suite has 199 EditMode tests.

## Logs

Runtime logs are written under Unity's persistent data path in `Logs`.
