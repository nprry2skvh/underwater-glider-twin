# Underwater Glider Digital Twin

Unity CSV replay prototype for underwater glider telemetry.

## Requirements

- Unity 2022.3.62f3c1
- Windows
- Data file: `2.csv` encoded as GBK

## Run In Unity

1. Open `UnderwaterGliderTwin` in Unity Hub.
2. Open `Assets/Scenes/Main.unity`.
3. Keep `2.csv` at `D:\Desktop\digital twin\2.csv`, or launch with `--csv`.
4. Press Play.

## Run Build

Run:

```powershell
.\Builds\UnderwaterGliderTwin\UnderwaterGliderTwin.exe --csv "D:\Desktop\digital twin\2.csv"
```

## Controls

- Play/Pause: start or stop replay.
- Speed: choose 0.5x, 1x, 2x, 5x, or 10x.
- Progress: drag to jump through CSV rows.
- Camera: Follow, Global, Free.
- Visual toggles: trajectory, fog, particles.

## Logs

Runtime logs are written under Unity's persistent data path in `Logs`.
