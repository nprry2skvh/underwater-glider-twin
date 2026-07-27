# Welcome Launch Scene Design

## Objective

Add a real Unity welcome/launch scene that gates the underwater glider twin before the main runtime scene, while preserving existing CSV replay, simulation, prediction, build, and test behavior.

## Current Reality

- `UnderwaterGliderTwin/Assets/Scenes/Main.unity` is the only scene in the build today.
- `TwinBootstrap` already owns runtime startup, CSV loading, simulation startup, prediction wiring, camera setup, and UI composition.
- `RuntimePathResolver` currently resolves CSV and model paths, and `Main.unity` already supports `--csv` and `--simulation` style startup through runtime state.
- `BuildWindows.cs` hard-codes `Main.unity` as the only build scene.
- EditMode tests already exist and can be extended for launch-contract checks.

## Goals

- Provide a real `Welcome.unity` scene as the first user-facing entry point.
- Keep command-line launch working without showing Welcome when launch arguments are valid.
- Show Welcome with an inline error message when launch arguments are invalid.
- Support a path-input flow for selecting CSV files without adding a native file browser dependency.
- Persist the last successfully loaded CSV path across launches.
- Keep Main scene startup deterministic and independent of Welcome UI details.

## Non-Goals

- No native file dialog dependency.
- No redesign of runtime playback, visualization, prediction, or telemetry logic beyond what launch flow requires.
- No PlayMode test requirement for scene switching in this change set unless needed for a specific failure mode.

## Architecture

Split launch into three layers:

1. `LaunchRequest` is pure data. It describes the chosen launch mode, CSV path, simulation profile, and validation errors.
2. `LaunchRequestParser` is pure logic. It reads command-line arguments and returns a `LaunchRequest`, including the `simulation > csv > welcome` decision and invalid-argument diagnostics. It does not call Unity APIs.
3. `LaunchCoordinator` is the Unity-facing bridge. It applies launch state, writes the CSV override, switches scenes, and exposes methods for Welcome UI actions.

`Welcome.unity` owns only launch UI and user choice collection. `Main.unity` remains the runtime scene that builds the twin experience.

## Launch Rules

Fixed precedence:

```text
Valid simulation arguments -> enter Simulation mode, skip Welcome
Else valid --csv -> enter CSV mode, skip Welcome
Else no direct launch arguments -> show Welcome
Invalid direct launch arguments -> show Welcome + error
```

Valid direct launch arguments include:

- `--simulation`
- `--simulation-depth <value>`
- `--simulation-current <east> <north>`
- `--csv <path>`
- `--csv=<path>`

Invalid `--csv` input includes:

- missing value
- malformed path token
- path that does not exist

Invalid direct launch arguments must not enter `Main.unity` automatically.

## Components

### LaunchRequest

`LaunchRequest` is a small immutable data object with these fields:

- `LaunchMode Mode`
- `string CsvPath`
- `SimulationProfile SimulationProfile`
- `bool HasErrors`
- `IReadOnlyList<string> Errors`

It carries no Unity behavior.

### LaunchRequestParser

`LaunchRequestParser.Parse(IReadOnlyList<string> args, string preferredCsvPath, SimulationProfile defaultProfile)` returns a `LaunchRequest`.

Required behavior:

- detect `--csv path` and `--csv=path`
- detect simulation flags
- apply `simulation > csv` precedence
- preserve human-readable error messages for invalid CSV launch input
- leave the launch mode as `Welcome` when direct launch input is invalid

### LaunchCoordinator

`LaunchCoordinator` is the Unity-facing service used by Welcome and Main.

Required responsibilities:

- apply a `LaunchRequest` to `RuntimeDataSourceState`
- set `RuntimePathResolver` CSV override when CSV mode is chosen
- save the successful CSV path to `PlayerPrefs` only after Main loads usable telemetry
- expose a scene-load abstraction so tests can assert scene intent without depending on live `SceneManager`

### WelcomeBootstrap

`WelcomeBootstrap` is the scene-level MonoBehaviour for `Welcome.unity`.

Responsibilities:

- create or bind the welcome UI
- display launch status and validation errors
- bind buttons for:
  - start last/default CSV
  - enter simulation mode
  - confirm typed CSV path
  - show project notes
- hand off to `LaunchCoordinator` for state changes and scene switching

### Welcome UI

The welcome screen is a simple runtime-generated UI:

- title and short project description
- CSV path input field
- confirm button for typed CSV path
- start last/default CSV button
- simulation mode button
- compact help text that explains the GBK CSV requirement and the experimental nature of prediction
- visible error area

The UI must not require a prefab, theme system, or third-party browser control.

### Main Runtime Persistence

`TwinBootstrap` remains the runtime initializer, but it must save the last successfully loaded CSV path only after telemetry loads with `Frames.Count > 0`.

PlayerPrefs strategy:

- store only normalized absolute paths
- save only on successful load
- on startup, prefer command line, then stored last CSV path, then default candidates
- if the stored path no longer exists, fall back without deleting it

## Data Flow

1. App starts in `Welcome.unity` unless a valid direct launch request exists.
2. `LaunchRequestParser` reads command-line arguments.
3. If the request is direct and valid, `LaunchCoordinator` sets runtime state and loads `Main`.
4. If the request is invalid or absent, Welcome shows and lets the user choose a CSV path or simulation mode.
5. Welcome actions create a `LaunchRequest`, apply it through `LaunchCoordinator`, then load `Main`.
6. `TwinBootstrap` loads telemetry, builds prediction, renders the twin, and on success stores the last good CSV path.

## Error Handling

- Invalid `--csv` input stays on Welcome and is shown in the error area.
- Missing CSV path in Welcome does not switch scenes.
- Invalid or empty typed CSV path remains in Welcome with a visible error.
- Main runtime failures continue to surface through existing startup logging and exceptions.

## Scene and Build Rules

- `Welcome.unity` must be scene index 0 in build settings.
- `Main.unity` must be scene index 1 in build settings.
- `BuildWindows.cs` must build both scenes in that order.
- Scene loading from Welcome must target `Main` by name, not current active scene index.

## Testing Strategy

EditMode tests should cover:

- `LaunchRequestParser` precedence and invalid-input behavior
- `LaunchCoordinator` applying CSV and simulation requests
- `LaunchCoordinator` scene-load intent via injected delegate
- `EditorBuildSettings` scene order
- `BuildWindows` scene list
- successful CSV load persistence behavior
- non-persistence when load fails or yields zero frames

Behavioral cases that matter:

- valid simulation args skip Welcome
- valid CSV args skip Welcome
- invalid CSV args do not skip Welcome
- `--csv` with no value is treated as invalid
- `--csv=path` is accepted
- `simulation > csv` when both are present

## Acceptance Criteria

- A new `Welcome.unity` scene exists and is the first build scene.
- Launching without direct launch args shows Welcome.
- Valid `--csv` or simulation args skip Welcome and open Main directly.
- Invalid `--csv` keeps the app on Welcome and shows an error.
- Welcome can start CSV, confirm a typed CSV path, and enter simulation mode.
- Last successful CSV path persists across launches.
- EditMode tests verify launch precedence, persistence rules, and build scene order.
- Windows build still succeeds and includes the welcome entry path.

