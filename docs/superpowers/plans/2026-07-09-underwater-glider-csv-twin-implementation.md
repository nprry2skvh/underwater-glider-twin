# Underwater Glider CSV Twin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Unity 2022.3 Windows digital twin prototype that replays `D:\Desktop\digital twin\2.csv` as a water glider trajectory with telemetry UI, underwater visualization, alarms, logs, and an executable build.

**Architecture:** The Unity project uses a data-source boundary around `TelemetryFrame`, so CSV replay, future TCP/UDP/serial feeds, and future 6-DOF simulation can all drive the same playback, mapping, visualization, UI, and logging layers. Runtime scene setup is code-driven to keep the first prototype reproducible without manually assembled prefabs.

**Tech Stack:** Unity `2022.3.62f3c1`, C# MonoBehaviour scripts, Unity Test Framework with NUnit EditMode tests, built-in render pipeline or URP-compatible materials, legacy UGUI, Windows standalone build.

## Global Constraints

- Work root: `D:\Desktop\digital twin`
- Unity project path: `D:\Desktop\digital twin\UnderwaterGliderTwin`
- Unity executable: `C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe`
- Data file: `D:\Desktop\digital twin\2.csv`
- CSV encoding: GBK
- CSV size: about 52 MB, 236163 records, 51 columns
- Keep `2.csv` out of Git.
- First phase includes CSV replay, visualization, UI, logging, alarms, and Windows build.
- First phase excludes TCP/UDP/serial live communication, 6-DOF physics, PID/LQR/MPC control, high-fidelity hydrodynamics, and a high-fidelity imported glider model.
- Use row order for playback progress; display raw `UTC时间` as a label.
- Use latitude/longitude local tangent-plane mapping with first valid coordinate as origin.
- Map depth to negative Unity Y with default `depthScale = 0.05`.
- Do not create one GameObject per CSV row.

---

## File Structure

Create these project files under `D:\Desktop\digital twin\UnderwaterGliderTwin`:

```text
Assets/
  Scenes/
    Main.unity
  Scripts/
    UnderwaterGliderTwin.Runtime.asmdef
    Telemetry/
      ITelemetrySource.cs
      TelemetryFrame.cs
      TelemetryLoadResult.cs
      CsvTelemetrySource.cs
    Mapping/
      GeoCoordinateMapper.cs
      PoseMapper.cs
      TrajectorySampler.cs
    Playback/
      PlaybackModel.cs
      PlaybackController.cs
    Logging/
      TwinLogger.cs
      AlarmEvaluator.cs
      AlarmState.cs
    Visualization/
      GliderVisualBuilder.cs
      GliderTransformDriver.cs
      TrajectoryView.cs
      UnderwaterEnvironmentBuilder.cs
      TwinCameraController.cs
    UI/
      DashboardView.cs
      PlaybackControlsView.cs
      StatusPanelView.cs
    Bootstrap/
      RuntimePathResolver.cs
      TwinBootstrap.cs
  Editor/
    BuildWindows.cs
Assets/Tests/EditMode/
  UnderwaterGliderTwin.Tests.asmdef
  CsvTelemetrySourceTests.cs
  GeoCoordinateMapperTests.cs
  PlaybackModelTests.cs
  AlarmEvaluatorTests.cs
ProjectSettings/
Packages/
```

Responsibilities:

- `Telemetry` owns CSV parsing and the stable telemetry contract.
- `Mapping` owns coordinate and pose conversion.
- `Playback` owns current-frame state and time progression.
- `Logging` owns local text log writing and alarm decisions.
- `Visualization` owns scene objects, glider geometry, trajectory lines, underwater atmosphere, and camera control.
- `UI` owns runtime-created UGUI controls and telemetry display.
- `Bootstrap` wires all runtime services together.
- `Editor/BuildWindows.cs` owns deterministic Windows build output.

---

### Task 1: Create Unity Project And Test Harness

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\UnderwaterGliderTwin.Runtime.asmdef`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Tests\EditMode\UnderwaterGliderTwin.Tests.asmdef`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scenes\Main.unity`

**Interfaces:**
- Produces: A Unity project that can compile runtime code and EditMode tests.
- Produces: Runtime assembly name `UnderwaterGliderTwin.Runtime`.
- Produces: Test assembly name `UnderwaterGliderTwin.Tests`.

- [ ] **Step 1: Create the Unity project**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' -quit -batchmode -createProject 'D:\Desktop\digital twin\UnderwaterGliderTwin'
```

Expected: exit code `0`, and `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets` exists.

- [ ] **Step 2: Create runtime and test folders**

Run:

```powershell
New-Item -ItemType Directory -Force `
  'D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts' `
  'D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Tests\EditMode' `
  'D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scenes'
```

Expected: all three folders exist.

- [ ] **Step 3: Create `UnderwaterGliderTwin.Runtime.asmdef`**

Create `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\UnderwaterGliderTwin.Runtime.asmdef`:

```json
{
  "name": "UnderwaterGliderTwin.Runtime",
  "rootNamespace": "UnderwaterGliderTwin",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 4: Create `UnderwaterGliderTwin.Tests.asmdef`**

Create `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Tests\EditMode\UnderwaterGliderTwin.Tests.asmdef`:

```json
{
  "name": "UnderwaterGliderTwin.Tests",
  "rootNamespace": "UnderwaterGliderTwin.Tests",
  "references": [
    "UnderwaterGliderTwin.Runtime"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 5: Create an empty scene**

Open Unity once in batchmode so it imports the project, then create `Assets/Scenes/Main.unity` through a temporary editor script or the Unity editor API in a small script. The scene must contain one GameObject named `TwinBootstrap`.

Verification command after the scene exists:

```powershell
Test-Path 'D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scenes\Main.unity'
```

Expected: `True`.

- [ ] **Step 6: Run an empty EditMode test pass**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' `
  -quit -batchmode `
  -projectPath 'D:\Desktop\digital twin\UnderwaterGliderTwin' `
  -runTests -testPlatform EditMode `
  -testResults 'D:\Desktop\digital twin\UnderwaterGliderTwin\EditModeResults.xml'
```

Expected: exit code `0`, or a Unity Test Framework result that reports no test assemblies with failures.

- [ ] **Step 7: Commit**

```powershell
git add UnderwaterGliderTwin
git commit -m "chore: create Unity project scaffold"
```

---

### Task 2: Telemetry Data Model And CSV Parser

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Telemetry\TelemetryFrame.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Telemetry\TelemetryLoadResult.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Telemetry\ITelemetrySource.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Telemetry\CsvTelemetrySource.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Tests\EditMode\CsvTelemetrySourceTests.cs`

**Interfaces:**
- Produces: `readonly struct TelemetryFrame`
- Produces: `sealed class TelemetryLoadResult`
- Produces: `interface ITelemetrySource { TelemetryLoadResult Load(); }`
- Produces: `sealed class CsvTelemetrySource : ITelemetrySource`
- Consumed by: `PlaybackModel`, `GeoCoordinateMapper`, `DashboardView`, `AlarmEvaluator`

- [ ] **Step 1: Write failing parser tests**

Create `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Tests\EditMode\CsvTelemetrySourceTests.cs`:

```csharp
using System.IO;
using System.Text;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CsvTelemetrySourceTests
    {
        [Test]
        public void Load_ParsesKnownColumnsFromGbkCsv()
        {
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "sample-telemetry.csv");
            var csv =
                "UTC时间,当前剖面序号,24V电压(V),24V电流(A),消耗电量(AH),运行状态位1,运行状态位2,运行状态位3,运行状态位4,故障状态位1,故障状态位2,警报状态位1,警报状态位2,工作模式,运行状态,原定航程(m),历时(min),转向角(°),活塞位置(mm),DIOL,DIOH,经度(°),纬度(°),深度(m),高度(m),航向(°),纵摇(°),横摇(°),主推进器转速(RPM),目标航段,目标航向(°),目标深度(m),目标高度(m),舱内温度(℃),舱内压力(KPa),CTD盐度(S/m),CTD温度(℃),CTD深度(m),记录/加载进度(%),文件状态位,高压力泵转速(RPM),电池电量(%),外接设备工作状态位,FLBBCD_695,FLBBCD_700,FLBBCD_460,BB3_470,BB3_530,BB3_650,SUNA,CTD_DO\n" +
                "000年00时00分09秒,1,28.5,0.3,0,32,10,0,7,88,3,0,0,水面模式,水面,2,0,32,17,0,16,120.00008333,25.00001728,2.3,100.0,30.4,-2,-5,0,30,44.3,1000,500,29.7,68.6,2.931,26.4086,2.35,0,27,0,95,89,0,5241,0,0,31162,0,0.0,1576\n";
            File.WriteAllText(path, csv, Encoding.GetEncoding(936));

            var result = new CsvTelemetrySource(path).Load();

            Assert.That(result.Frames, Has.Count.EqualTo(1));
            Assert.That(result.SkippedRows, Is.EqualTo(0));
            var frame = result.Frames[0];
            Assert.That(frame.RawTime, Is.EqualTo("000年00时00分09秒"));
            Assert.That(frame.LongitudeDeg, Is.EqualTo(120.00008333).Within(0.00000001));
            Assert.That(frame.LatitudeDeg, Is.EqualTo(25.00001728).Within(0.00000001));
            Assert.That(frame.DepthM, Is.EqualTo(2.3f).Within(0.0001f));
            Assert.That(frame.HeadingDeg, Is.EqualTo(30.4f).Within(0.0001f));
            Assert.That(frame.PitchDeg, Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(frame.RollDeg, Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(frame.WorkMode, Is.EqualTo("水面模式"));
            Assert.That(frame.RunState, Is.EqualTo("水面"));
            Assert.That(frame.BatteryPercent, Is.EqualTo(95f).Within(0.0001f));
        }

        [Test]
        public void Load_SkipsRowsWithInvalidNumbers()
        {
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "bad-telemetry.csv");
            var csv =
                "UTC时间,当前剖面序号,24V电压(V),24V电流(A),消耗电量(AH),运行状态位1,运行状态位2,运行状态位3,运行状态位4,故障状态位1,故障状态位2,警报状态位1,警报状态位2,工作模式,运行状态,原定航程(m),历时(min),转向角(°),活塞位置(mm),DIOL,DIOH,经度(°),纬度(°),深度(m),高度(m),航向(°),纵摇(°),横摇(°),主推进器转速(RPM),目标航段,目标航向(°),目标深度(m),目标高度(m),舱内温度(℃),舱内压力(KPa),CTD盐度(S/m),CTD温度(℃),CTD深度(m),记录/加载进度(%),文件状态位,高压力泵转速(RPM),电池电量(%),外接设备工作状态位,FLBBCD_695,FLBBCD_700,FLBBCD_460,BB3_470,BB3_530,BB3_650,SUNA,CTD_DO\n" +
                "bad,1,28.5,0.3,0,32,10,0,7,88,3,0,0,水面模式,水面,2,0,32,17,0,16,not-a-number,25.00001728,2.3,100.0,30.4,-2,-5,0,30,44.3,1000,500,29.7,68.6,2.931,26.4086,2.35,0,27,0,95,89,0,5241,0,0,31162,0,0.0,1576\n";
            File.WriteAllText(path, csv, Encoding.GetEncoding(936));

            var result = new CsvTelemetrySource(path).Load();

            Assert.That(result.Frames, Has.Count.EqualTo(0));
            Assert.That(result.SkippedRows, Is.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("line 2"));
        }
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' `
  -quit -batchmode `
  -projectPath 'D:\Desktop\digital twin\UnderwaterGliderTwin' `
  -runTests -testPlatform EditMode `
  -testResults 'D:\Desktop\digital twin\UnderwaterGliderTwin\EditModeResults.xml'
```

Expected: FAIL because `CsvTelemetrySource` and telemetry types are not defined.

- [ ] **Step 3: Implement telemetry files**

Create `TelemetryFrame.cs` with immutable fields:

```csharp
namespace UnderwaterGliderTwin.Telemetry
{
    public readonly struct TelemetryFrame
    {
        public readonly int RowIndex;
        public readonly string RawTime;
        public readonly double LongitudeDeg;
        public readonly double LatitudeDeg;
        public readonly float DepthM;
        public readonly float AltitudeM;
        public readonly float HeadingDeg;
        public readonly float PitchDeg;
        public readonly float RollDeg;
        public readonly float Voltage24V;
        public readonly float Current24A;
        public readonly float BatteryPercent;
        public readonly string WorkMode;
        public readonly string RunState;
        public readonly float TargetSegment;
        public readonly float TargetHeadingDeg;
        public readonly float TargetDepthM;
        public readonly float TargetAltitudeM;
        public readonly float PropellerRpm;
        public readonly float PistonMm;
        public readonly float TurnAngleDeg;

        public TelemetryFrame(
            int rowIndex,
            string rawTime,
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float altitudeM,
            float headingDeg,
            float pitchDeg,
            float rollDeg,
            float voltage24V,
            float current24A,
            float batteryPercent,
            string workMode,
            string runState,
            float targetSegment,
            float targetHeadingDeg,
            float targetDepthM,
            float targetAltitudeM,
            float propellerRpm,
            float pistonMm,
            float turnAngleDeg)
        {
            RowIndex = rowIndex;
            RawTime = rawTime;
            LongitudeDeg = longitudeDeg;
            LatitudeDeg = latitudeDeg;
            DepthM = depthM;
            AltitudeM = altitudeM;
            HeadingDeg = headingDeg;
            PitchDeg = pitchDeg;
            RollDeg = rollDeg;
            Voltage24V = voltage24V;
            Current24A = current24A;
            BatteryPercent = batteryPercent;
            WorkMode = workMode;
            RunState = runState;
            TargetSegment = targetSegment;
            TargetHeadingDeg = targetHeadingDeg;
            TargetDepthM = targetDepthM;
            TargetAltitudeM = targetAltitudeM;
            PropellerRpm = propellerRpm;
            PistonMm = pistonMm;
            TurnAngleDeg = turnAngleDeg;
        }
    }
}
```

Create `TelemetryLoadResult.cs`:

```csharp
using System.Collections.Generic;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class TelemetryLoadResult
    {
        public IReadOnlyList<TelemetryFrame> Frames { get; }
        public IReadOnlyList<string> Errors { get; }
        public int SkippedRows { get; }

        public TelemetryLoadResult(IReadOnlyList<TelemetryFrame> frames, IReadOnlyList<string> errors, int skippedRows)
        {
            Frames = frames;
            Errors = errors;
            SkippedRows = skippedRows;
        }
    }
}
```

Create `ITelemetrySource.cs`:

```csharp
namespace UnderwaterGliderTwin.Telemetry
{
    public interface ITelemetrySource
    {
        TelemetryLoadResult Load();
    }
}
```

Create `CsvTelemetrySource.cs` with index-based column mapping:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class CsvTelemetrySource : ITelemetrySource
    {
        private const int ExpectedColumnCount = 51;
        private readonly string path;

        public CsvTelemetrySource(string path)
        {
            this.path = path;
        }

        public TelemetryLoadResult Load()
        {
            var frames = new List<TelemetryFrame>(capacity: 240000);
            var errors = new List<string>();
            var skipped = 0;

            using var reader = new StreamReader(path, GetGbkEncoding(), detectEncodingFromByteOrderMarks: true);
            _ = reader.ReadLine();
            var lineNumber = 1;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var columns = line.Split(',');
                if (columns.Length < ExpectedColumnCount)
                {
                    skipped++;
                    errors.Add($"Skipped line {lineNumber}: expected {ExpectedColumnCount} columns, got {columns.Length}");
                    continue;
                }

                try
                {
                    frames.Add(new TelemetryFrame(
                        rowIndex: lineNumber - 2,
                        rawTime: columns[0],
                        longitudeDeg: ParseDouble(columns[21]),
                        latitudeDeg: ParseDouble(columns[22]),
                        depthM: ParseFloat(columns[23]),
                        altitudeM: ParseFloat(columns[24]),
                        headingDeg: ParseFloat(columns[25]),
                        pitchDeg: ParseFloat(columns[26]),
                        rollDeg: ParseFloat(columns[27]),
                        voltage24V: ParseFloat(columns[2]),
                        current24A: ParseFloat(columns[3]),
                        batteryPercent: ParseFloat(columns[41]),
                        workMode: columns[13],
                        runState: columns[14],
                        targetSegment: ParseFloat(columns[29]),
                        targetHeadingDeg: ParseFloat(columns[30]),
                        targetDepthM: ParseFloat(columns[31]),
                        targetAltitudeM: ParseFloat(columns[32]),
                        propellerRpm: ParseFloat(columns[28]),
                        pistonMm: ParseFloat(columns[18]),
                        turnAngleDeg: ParseFloat(columns[17])));
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                {
                    skipped++;
                    errors.Add($"Skipped line {lineNumber}: {ex.Message}");
                }
            }

            return new TelemetryLoadResult(frames, errors, skipped);
        }

        private static Encoding GetGbkEncoding()
        {
            try
            {
                return Encoding.GetEncoding(936);
            }
            catch (ArgumentException)
            {
                return Encoding.Default;
            }
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string value)
        {
            return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
```

- [ ] **Step 4: Run parser tests and verify pass**

Run the EditMode command from Step 2.

Expected: PASS for `CsvTelemetrySourceTests`.

- [ ] **Step 5: Run parser against real `2.csv` with a temporary editor or test hook**

Add a temporary NUnit test that calls `new CsvTelemetrySource(@"D:\Desktop\digital twin\2.csv").Load()` and asserts:

```csharp
Assert.That(result.Frames.Count, Is.GreaterThan(200000));
Assert.That(result.Frames[0].LongitudeDeg, Is.InRange(119.0, 121.0));
Assert.That(result.Frames[0].LatitudeDeg, Is.InRange(24.0, 26.0));
```

Run it once, confirm it passes, then remove this temporary test before commit so the test suite is portable.

- [ ] **Step 6: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/Telemetry UnderwaterGliderTwin/Assets/Tests/EditMode
git commit -m "feat: parse glider telemetry CSV"
```

---

### Task 3: Coordinate, Pose, And Trajectory Mapping

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Mapping\GeoCoordinateMapper.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Mapping\PoseMapper.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Mapping\TrajectorySampler.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Tests\EditMode\GeoCoordinateMapperTests.cs`

**Interfaces:**
- Consumes: `TelemetryFrame`
- Produces: `GeoCoordinateMapper.Map(TelemetryFrame frame) : Vector3`
- Produces: `PoseMapper.ToRotation(TelemetryFrame frame) : Quaternion`
- Produces: `TrajectorySampler.Sample(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, int maxPoints) : Vector3[]`

- [ ] **Step 1: Write failing mapping tests**

Create `GeoCoordinateMapperTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class GeoCoordinateMapperTests
    {
        [Test]
        public void Map_UsesOriginAndDepthScale()
        {
            var origin = Frame(120.0, 25.0, 0f, 0f, 0f, 0f);
            var mapper = new GeoCoordinateMapper(origin, horizontalScale: 0.01f, depthScale: 0.05f);
            var point = Frame(120.001, 25.001, 100f, 0f, 0f, 0f);

            var mapped = mapper.Map(point);

            Assert.That(mapped.x, Is.EqualTo(1.009f).Within(0.02f));
            Assert.That(mapped.z, Is.EqualTo(1.113f).Within(0.02f));
            Assert.That(mapped.y, Is.EqualTo(-5f).Within(0.001f));
        }

        [Test]
        public void PoseMapper_ConvertsHeadingPitchRoll()
        {
            var frame = Frame(120.0, 25.0, 0f, heading: 90f, pitch: 10f, roll: -5f);

            var rotation = PoseMapper.ToRotation(frame);

            var forward = rotation * Vector3.forward;
            Assert.That(forward.x, Is.GreaterThan(0.9f));
            Assert.That(Mathf.Abs(forward.z), Is.LessThan(0.2f));
        }

        private static TelemetryFrame Frame(double lon, double lat, float depth, float heading, float pitch, float roll)
        {
            return new TelemetryFrame(0, "t", lon, lat, depth, 100f, heading, pitch, roll, 28f, 0.2f, 95f, "mode", "state", 1f, 40f, 100f, 100f, 0f, 0f, 0f);
        }
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run EditMode tests.

Expected: FAIL because mapping classes are missing.

- [ ] **Step 3: Implement mapping classes**

Create `GeoCoordinateMapper.cs`:

```csharp
using System;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public sealed class GeoCoordinateMapper
    {
        private const double MetersPerDegreeLatitude = 111320.0;
        private readonly double originLongitudeDeg;
        private readonly double originLatitudeDeg;
        private readonly double metersPerDegreeLongitude;
        private readonly float horizontalScale;
        private readonly float depthScale;

        public GeoCoordinateMapper(TelemetryFrame originFrame, float horizontalScale, float depthScale)
        {
            originLongitudeDeg = originFrame.LongitudeDeg;
            originLatitudeDeg = originFrame.LatitudeDeg;
            metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(originLatitudeDeg * Math.PI / 180.0);
            this.horizontalScale = horizontalScale;
            this.depthScale = depthScale;
        }

        public Vector3 Map(TelemetryFrame frame)
        {
            var eastMeters = (frame.LongitudeDeg - originLongitudeDeg) * metersPerDegreeLongitude;
            var northMeters = (frame.LatitudeDeg - originLatitudeDeg) * MetersPerDegreeLatitude;
            return new Vector3((float)eastMeters * horizontalScale, -frame.DepthM * depthScale, (float)northMeters * horizontalScale);
        }
    }
}
```

Create `PoseMapper.cs`:

```csharp
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public static class PoseMapper
    {
        public static Quaternion ToRotation(TelemetryFrame frame)
        {
            return Quaternion.Euler(frame.PitchDeg, frame.HeadingDeg, -frame.RollDeg);
        }
    }
}
```

Create `TrajectorySampler.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public static class TrajectorySampler
    {
        public static Vector3[] Sample(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, int maxPoints)
        {
            if (frames == null || frames.Count == 0)
            {
                return Array.Empty<Vector3>();
            }

            var count = Math.Min(maxPoints, frames.Count);
            var points = new Vector3[count];
            var step = frames.Count <= 1 ? 1f : (frames.Count - 1f) / Math.Max(1, count - 1);

            for (var i = 0; i < count; i++)
            {
                var sourceIndex = Mathf.Clamp(Mathf.RoundToInt(i * step), 0, frames.Count - 1);
                points[i] = mapper.Map(frames[sourceIndex]);
            }

            return points;
        }
    }
}
```

- [ ] **Step 4: Run mapping tests and verify pass**

Run EditMode tests.

Expected: PASS for parser and mapping tests.

- [ ] **Step 5: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/Mapping UnderwaterGliderTwin/Assets/Tests/EditMode/GeoCoordinateMapperTests.cs
git commit -m "feat: map telemetry to Unity coordinates"
```

---

### Task 4: Playback Model And Controller

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Playback\PlaybackModel.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Playback\PlaybackController.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Tests\EditMode\PlaybackModelTests.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<TelemetryFrame>`
- Produces: `PlaybackModel.CurrentIndex`, `PlaybackModel.CurrentFrame`, `PlaybackModel.Progress01`
- Produces: `PlaybackModel.Tick(float deltaSeconds)`
- Produces: `PlaybackModel.SeekNormalized(float progress01)`
- Produces: `PlaybackController.FrameChanged` event for scene/UI synchronization

- [ ] **Step 1: Write failing playback tests**

Create `PlaybackModelTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class PlaybackModelTests
    {
        [Test]
        public void Tick_AdvancesByRowsPerSecondAndSpeed()
        {
            var model = new PlaybackModel(Frames(100), rowsPerSecond: 10f);
            model.SetPlaying(true);
            model.SetSpeed(2f);

            model.Tick(1f);

            Assert.That(model.CurrentIndex, Is.EqualTo(20));
        }

        [Test]
        public void SeekNormalized_ClampsToValidIndex()
        {
            var model = new PlaybackModel(Frames(100), rowsPerSecond: 10f);

            model.SeekNormalized(0.5f);

            Assert.That(model.CurrentIndex, Is.EqualTo(50).Within(1));
            Assert.That(model.CurrentFrame.RowIndex, Is.EqualTo(model.CurrentIndex));
        }

        private static IReadOnlyList<TelemetryFrame> Frames(int count)
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < count; i++)
            {
                frames.Add(new TelemetryFrame(i, $"t{i}", 120, 25, i, 100, 0, 0, 0, 28, 0, 95, "mode", "state", 1, 0, 0, 0, 0, 0, 0));
            }
            return frames;
        }
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run EditMode tests.

Expected: FAIL because `PlaybackModel` is missing.

- [ ] **Step 3: Implement playback files**

Create `PlaybackModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Playback
{
    public sealed class PlaybackModel
    {
        private readonly IReadOnlyList<TelemetryFrame> frames;
        private readonly float rowsPerSecond;
        private float continuousIndex;

        public bool IsPlaying { get; private set; }
        public float Speed { get; private set; } = 1f;
        public int CurrentIndex { get; private set; }
        public int FrameCount => frames.Count;
        public float Progress01 => frames.Count <= 1 ? 0f : CurrentIndex / (float)(frames.Count - 1);
        public TelemetryFrame CurrentFrame => frames[CurrentIndex];

        public PlaybackModel(IReadOnlyList<TelemetryFrame> frames, float rowsPerSecond)
        {
            this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
            if (frames.Count == 0)
            {
                throw new ArgumentException("Playback requires at least one frame.", nameof(frames));
            }
            this.rowsPerSecond = rowsPerSecond;
        }

        public void SetPlaying(bool isPlaying)
        {
            IsPlaying = isPlaying;
        }

        public void TogglePlaying()
        {
            IsPlaying = !IsPlaying;
        }

        public void SetSpeed(float speed)
        {
            Speed = Math.Max(0.1f, speed);
        }

        public bool Tick(float deltaSeconds)
        {
            if (!IsPlaying)
            {
                return false;
            }

            continuousIndex += rowsPerSecond * Speed * deltaSeconds;
            var nextIndex = Math.Min(frames.Count - 1, (int)continuousIndex);
            var changed = nextIndex != CurrentIndex;
            CurrentIndex = nextIndex;

            if (CurrentIndex >= frames.Count - 1)
            {
                IsPlaying = false;
            }

            return changed;
        }

        public void SeekNormalized(float progress01)
        {
            var clamped = Math.Max(0f, Math.Min(1f, progress01));
            CurrentIndex = (int)Math.Round(clamped * (frames.Count - 1));
            continuousIndex = CurrentIndex;
        }
    }
}
```

Create `PlaybackController.cs`:

```csharp
using System;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Playback
{
    public sealed class PlaybackController : MonoBehaviour
    {
        private PlaybackModel model;

        public event Action<TelemetryFrame, int, float> FrameChanged;

        public PlaybackModel Model => model;

        public void Initialize(PlaybackModel playbackModel)
        {
            model = playbackModel;
            Publish();
        }

        private void Update()
        {
            if (model != null && model.Tick(Time.deltaTime))
            {
                Publish();
            }
        }

        public void TogglePlaying()
        {
            model.TogglePlaying();
        }

        public void SetPlaying(bool playing)
        {
            model.SetPlaying(playing);
        }

        public void SetSpeed(float speed)
        {
            model.SetSpeed(speed);
        }

        public void Seek(float progress01)
        {
            model.SeekNormalized(progress01);
            Publish();
        }

        private void Publish()
        {
            FrameChanged?.Invoke(model.CurrentFrame, model.CurrentIndex, model.Progress01);
        }
    }
}
```

- [ ] **Step 4: Run playback tests and verify pass**

Run EditMode tests.

Expected: PASS for parser, mapping, and playback tests.

- [ ] **Step 5: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/Playback UnderwaterGliderTwin/Assets/Tests/EditMode/PlaybackModelTests.cs
git commit -m "feat: add telemetry playback model"
```

---

### Task 5: Logging And Alarm Evaluation

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Logging\AlarmState.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Logging\AlarmEvaluator.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Logging\TwinLogger.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Tests\EditMode\AlarmEvaluatorTests.cs`

**Interfaces:**
- Consumes: `TelemetryFrame`
- Produces: `AlarmEvaluator.Evaluate(TelemetryFrame frame) : AlarmState`
- Produces: `TwinLogger.AppendLoad`, `TwinLogger.AppendAlarm`, `TwinLogger.AppendPlayback`

- [ ] **Step 1: Write failing alarm tests**

Create `AlarmEvaluatorTests.cs`:

```csharp
using NUnit.Framework;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class AlarmEvaluatorTests
    {
        [Test]
        public void Evaluate_FlagsDepthBatteryAndAttitude()
        {
            var evaluator = new AlarmEvaluator(maxDepthM: 1000f, minBatteryPercent: 20f, maxAbsAttitudeDeg: 20f);
            var frame = new TelemetryFrame(0, "t", 120, 25, 1201f, 100f, 0f, 22f, -21f, 28f, 0f, 9f, "潜航", "潜航", 1f, 0f, 1000f, 100f, 0f, 0f, 0f);

            var alarm = evaluator.Evaluate(frame);

            Assert.That(alarm.HasAny, Is.True);
            Assert.That(alarm.DepthExceeded, Is.True);
            Assert.That(alarm.BatteryLow, Is.True);
            Assert.That(alarm.AttitudeExceeded, Is.True);
            Assert.That(alarm.Message, Does.Contain("depth"));
            Assert.That(alarm.Message, Does.Contain("battery"));
            Assert.That(alarm.Message, Does.Contain("attitude"));
        }
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run EditMode tests.

Expected: FAIL because logging classes are missing.

- [ ] **Step 3: Implement alarm and logger files**

Create `AlarmState.cs`:

```csharp
namespace UnderwaterGliderTwin.Logging
{
    public readonly struct AlarmState
    {
        public readonly bool DepthExceeded;
        public readonly bool BatteryLow;
        public readonly bool AttitudeExceeded;
        public readonly string Message;

        public bool HasAny => DepthExceeded || BatteryLow || AttitudeExceeded;

        public AlarmState(bool depthExceeded, bool batteryLow, bool attitudeExceeded, string message)
        {
            DepthExceeded = depthExceeded;
            BatteryLow = batteryLow;
            AttitudeExceeded = attitudeExceeded;
            Message = message;
        }
    }
}
```

Create `AlarmEvaluator.cs`:

```csharp
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Logging
{
    public sealed class AlarmEvaluator
    {
        private readonly float maxDepthM;
        private readonly float minBatteryPercent;
        private readonly float maxAbsAttitudeDeg;

        public AlarmEvaluator(float maxDepthM, float minBatteryPercent, float maxAbsAttitudeDeg)
        {
            this.maxDepthM = maxDepthM;
            this.minBatteryPercent = minBatteryPercent;
            this.maxAbsAttitudeDeg = maxAbsAttitudeDeg;
        }

        public AlarmState Evaluate(TelemetryFrame frame)
        {
            var depthExceeded = frame.DepthM > maxDepthM;
            var batteryLow = frame.BatteryPercent < minBatteryPercent;
            var attitudeExceeded = System.Math.Abs(frame.PitchDeg) > maxAbsAttitudeDeg || System.Math.Abs(frame.RollDeg) > maxAbsAttitudeDeg;
            var messages = new List<string>();

            if (depthExceeded)
            {
                messages.Add($"depth {frame.DepthM:0.0}m > {maxDepthM:0.0}m");
            }
            if (batteryLow)
            {
                messages.Add($"battery {frame.BatteryPercent:0.0}% < {minBatteryPercent:0.0}%");
            }
            if (attitudeExceeded)
            {
                messages.Add($"attitude pitch {frame.PitchDeg:0.0} roll {frame.RollDeg:0.0}");
            }

            return new AlarmState(depthExceeded, batteryLow, attitudeExceeded, string.Join("; ", messages));
        }
    }
}
```

Create `TwinLogger.cs`:

```csharp
using System;
using System.IO;

namespace UnderwaterGliderTwin.Logging
{
    public sealed class TwinLogger
    {
        private readonly string logDirectory;

        public TwinLogger(string logDirectory)
        {
            this.logDirectory = logDirectory;
            Directory.CreateDirectory(logDirectory);
        }

        public void AppendLoad(string message)
        {
            Append("load.log", message);
        }

        public void AppendAlarm(string message)
        {
            Append("alarm.log", message);
        }

        public void AppendPlayback(string message)
        {
            Append("playback.log", message);
        }

        private void Append(string fileName, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(logDirectory, fileName), line);
        }
    }
}
```

- [ ] **Step 4: Run alarm tests and verify pass**

Run EditMode tests.

Expected: PASS for all EditMode tests.

- [ ] **Step 5: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/Logging UnderwaterGliderTwin/Assets/Tests/EditMode/AlarmEvaluatorTests.cs
git commit -m "feat: add alarms and local logging"
```

---

### Task 6: Runtime Path Resolution And Bootstrap Integration

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Bootstrap\RuntimePathResolver.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Bootstrap\TwinBootstrap.cs`
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scenes\Main.unity`

**Interfaces:**
- Consumes: `CsvTelemetrySource`, `PlaybackModel`, `TwinLogger`
- Produces: One scene object that loads CSV, creates services, and starts playback.
- Produces: Command-line override `--csv "path\to\data.csv"`.

- [ ] **Step 1: Implement runtime path resolver**

Create `RuntimePathResolver.cs`:

```csharp
using System;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Bootstrap
{
    public static class RuntimePathResolver
    {
        public static string ResolveCsvPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--csv" && File.Exists(args[i + 1]))
                {
                    return args[i + 1];
                }
            }

            var candidates = new[]
            {
                Path.Combine(Application.streamingAssetsPath, "2.csv"),
                Path.Combine(Directory.GetCurrentDirectory(), "2.csv"),
                Path.Combine(ParentOf(Application.dataPath, 1), "2.csv"),
                Path.Combine(ParentOf(Application.dataPath, 2), "2.csv"),
                @"D:\Desktop\digital twin\2.csv"
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException("Could not find 2.csv. Pass --csv with a full path or place 2.csv beside the project/build.");
        }

        public static string ResolveLogDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "Logs");
        }

        private static string ParentOf(string path, int levels)
        {
            var current = new DirectoryInfo(path);
            for (var i = 0; i < levels && current.Parent != null; i++)
            {
                current = current.Parent;
            }
            return current.FullName;
        }
    }
}
```

- [ ] **Step 2: Implement bootstrap shell**

Create `TwinBootstrap.cs`:

```csharp
using UnityEngine;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class TwinBootstrap : MonoBehaviour
    {
        [SerializeField] private float rowsPerSecond = 120f;
        [SerializeField] private float horizontalScale = 0.0025f;
        [SerializeField] private float depthScale = 0.05f;

        public PlaybackController PlaybackController { get; private set; }
        public GeoCoordinateMapper Mapper { get; private set; }
        public TelemetryLoadResult LoadResult { get; private set; }
        public TwinLogger Logger { get; private set; }
        public AlarmEvaluator AlarmEvaluator { get; private set; }

        private void Awake()
        {
            Logger = new TwinLogger(RuntimePathResolver.ResolveLogDirectory());
            var csvPath = RuntimePathResolver.ResolveCsvPath();
            Logger.AppendLoad($"Loading CSV: {csvPath}");

            LoadResult = new CsvTelemetrySource(csvPath).Load();
            Logger.AppendLoad($"Loaded {LoadResult.Frames.Count} frames; skipped {LoadResult.SkippedRows} rows.");
            foreach (var error in LoadResult.Errors)
            {
                Logger.AppendLoad(error);
            }

            Mapper = new GeoCoordinateMapper(LoadResult.Frames[0], horizontalScale, depthScale);
            AlarmEvaluator = new AlarmEvaluator(maxDepthM: 1000f, minBatteryPercent: 20f, maxAbsAttitudeDeg: 20f);

            PlaybackController = gameObject.AddComponent<PlaybackController>();
            PlaybackController.Initialize(new PlaybackModel(LoadResult.Frames, rowsPerSecond));
        }
    }
}
```

- [ ] **Step 3: Attach bootstrap to `Main.unity`**

Open the Unity project and ensure `Assets/Scenes/Main.unity` has one GameObject named `TwinBootstrap` with the `TwinBootstrap` component.

Verification in Unity hierarchy:

```text
Main
└── TwinBootstrap (TwinBootstrap)
```

- [ ] **Step 4: Run scene in Editor with real CSV**

Press Play in Unity Editor.

Expected:

- Console prints no unhandled exception.
- `Application.persistentDataPath/Logs/load.log` contains a line with `Loaded 236163 frames`.
- Play mode remains responsive after load.

- [ ] **Step 5: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/Bootstrap UnderwaterGliderTwin/Assets/Scenes/Main.unity
git commit -m "feat: bootstrap CSV playback runtime"
```

---

### Task 7: Glider Model, Trajectory, Environment, And Cameras

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Visualization\GliderVisualBuilder.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Visualization\GliderTransformDriver.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Visualization\TrajectoryView.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Visualization\UnderwaterEnvironmentBuilder.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Visualization\TwinCameraController.cs`
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Bootstrap\TwinBootstrap.cs`

**Interfaces:**
- Consumes: `PlaybackController.FrameChanged`, `GeoCoordinateMapper`, `PoseMapper`
- Produces: Runtime glider GameObject named `Glider`
- Produces: trajectory LineRenderer named `ActualTrajectory`
- Produces: camera modes `Follow`, `Global`, `Free`

- [ ] **Step 1: Implement `GliderVisualBuilder`**

Create a root object `Glider` with:

- cylinder or capsule-like body scaled long on Z
- left and right wings
- tail stabilizers
- small red nose marker
- neutral material for body and cyan material for wing highlights

Public method:

```csharp
public static GameObject Build()
```

The returned object must have its local forward direction along `Vector3.forward`.

- [ ] **Step 2: Implement `GliderTransformDriver`**

Create a MonoBehaviour with:

```csharp
public void Initialize(PlaybackController playback, GeoCoordinateMapper mapper)
```

On each `FrameChanged`, set:

```csharp
transform.position = mapper.Map(frame);
transform.rotation = PoseMapper.ToRotation(frame);
```

- [ ] **Step 3: Implement `TrajectoryView`**

Create two `LineRenderer` objects:

- `FullTrajectory`: muted blue, sampled max 8000 points
- `TravelledTrajectory`: bright cyan, updated from sampled points up to current progress

Public method:

```csharp
public void Initialize(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, PlaybackController playback)
```

Use `TrajectorySampler.Sample(frames, mapper, 8000)` once during initialization. During playback, compute travelled sample count from `progress01`.

- [ ] **Step 4: Implement underwater environment**

`UnderwaterEnvironmentBuilder.Build()` must create:

- one seabed plane at `y = -70`
- fog enabled with `RenderSettings.fogMode = FogMode.ExponentialSquared`
- fog color near blue-green
- ambient color near dark blue
- one directional light
- one particle system parented near the camera for marine snow
- three transparent cone or quad light-beam meshes above the route

Public switches:

```csharp
public void SetFogEnabled(bool enabled)
public void SetParticlesEnabled(bool enabled)
```

- [ ] **Step 5: Implement camera controller**

`TwinCameraController` must support:

```csharp
public enum CameraMode { Follow, Global, Free }
public void SetMode(CameraMode mode)
public void Initialize(Transform target, Vector3[] trajectoryPoints)
```

Behavior:

- Follow: smooth damp behind and above target.
- Global: frame the trajectory bounds.
- Free: right mouse drag rotates, mouse wheel zooms, WASD/QE pans.

- [ ] **Step 6: Wire visualization in `TwinBootstrap`**

After `PlaybackController.Initialize(...)`:

```csharp
var glider = GliderVisualBuilder.Build();
var driver = glider.AddComponent<GliderTransformDriver>();
driver.Initialize(PlaybackController, Mapper);

var trajectory = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();
trajectory.Initialize(LoadResult.Frames, Mapper, PlaybackController);

var environment = new GameObject("UnderwaterEnvironment").AddComponent<UnderwaterEnvironmentBuilder>();
environment.Build();

var cameraController = Camera.main.gameObject.AddComponent<TwinCameraController>();
cameraController.Initialize(glider.transform, trajectory.FullTrajectoryPoints);
```

If no `Camera.main` exists, create one Camera tagged `MainCamera` first.

- [ ] **Step 7: Editor play verification**

Press Play.

Expected:

- A visible glider appears.
- The glider moves when playback is toggled on by later UI or by temporarily setting `SetPlaying(true)` for this test.
- A full trajectory line is visible.
- Fog, seabed, light, and marine snow are visible.
- Camera follow mode frames the glider.

- [ ] **Step 8: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/Visualization UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs
git commit -m "feat: render glider trajectory and underwater scene"
```

---

### Task 8: Runtime UI And Playback Controls

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\UI\DashboardView.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\UI\PlaybackControlsView.cs`
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\UI\StatusPanelView.cs`
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Bootstrap\TwinBootstrap.cs`

**Interfaces:**
- Consumes: `PlaybackController`, `AlarmEvaluator`, `TwinLogger`, `TelemetryFrame`
- Produces: runtime Canvas with left telemetry panel, right status panel, bottom controls, and alarm display.

- [ ] **Step 1: Implement reusable UI creation helpers**

Each UI class should create UGUI objects programmatically using:

- `Canvas`
- `CanvasScaler`
- `GraphicRaycaster`
- `Text`
- `Button`
- `Slider`
- `Toggle`

Use built-in Arial font from `Resources.GetBuiltinResource<Font>("Arial.ttf")`.

- [ ] **Step 2: Implement `DashboardView`**

Public method:

```csharp
public void Initialize(PlaybackController playback)
```

On `FrameChanged`, update labels:

```text
Depth
Heading
Pitch
Roll
Battery
24V Voltage
24V Current
Propeller RPM
Piston
```

Use fixed numeric formats:

- depth: `0.0 m`
- heading/pitch/roll: `0.0 deg`
- battery: `0%`
- voltage/current: `0.0`

- [ ] **Step 3: Implement `StatusPanelView`**

Public method:

```csharp
public void Initialize(PlaybackController playback, AlarmEvaluator alarmEvaluator, TwinLogger logger)
```

On `FrameChanged`, update:

```text
Mode
State
Target Segment
Target Heading
Target Depth
Target Altitude
Alarm
Row
Raw Time
```

When `AlarmState.HasAny` is true, display the alarm text with red/orange background and call `logger.AppendAlarm(...)`. Avoid duplicate log spam by only writing when the alarm message changes.

- [ ] **Step 4: Implement `PlaybackControlsView`**

Public method:

```csharp
public void Initialize(PlaybackController playback, TwinCameraController cameraController, UnderwaterEnvironmentBuilder environmentBuilder, TrajectoryView trajectoryView)
```

Controls:

- Play/Pause button calls `playback.TogglePlaying()`
- Speed buttons call `playback.SetSpeed(0.5f)`, `1f`, `2f`, `5f`, `10f`
- Slider calls `playback.Seek(slider.value)` while dragged
- Camera buttons call `cameraController.SetMode(CameraMode.Follow)`, `.Global`, `.Free`
- Toggles call environment and trajectory switches

- [ ] **Step 5: Wire UI in `TwinBootstrap`**

After visualization setup:

```csharp
var canvasRoot = new GameObject("RuntimeUI");
canvasRoot.AddComponent<DashboardView>().Initialize(PlaybackController);
canvasRoot.AddComponent<StatusPanelView>().Initialize(PlaybackController, AlarmEvaluator, Logger);
canvasRoot.AddComponent<PlaybackControlsView>().Initialize(PlaybackController, cameraController, environment, trajectory);
```

- [ ] **Step 6: Editor play verification**

Press Play.

Expected:

- UI does not cover the glider center.
- Play/Pause starts and stops replay.
- Speed buttons visibly change playback speed.
- Slider jump changes glider position.
- Camera buttons switch views.
- Fog and particles toggles change the scene.
- Alarm panel highlights during frames that exceed configured thresholds.

- [ ] **Step 7: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs
git commit -m "feat: add telemetry dashboard and replay controls"
```

---

### Task 9: Scene Polish, Performance Guards, And Real Data Smoke Test

**Files:**
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Telemetry\CsvTelemetrySource.cs`
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Playback\PlaybackController.cs`
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\Visualization\TrajectoryView.cs`
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\UI\DashboardView.cs`
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Scripts\UI\StatusPanelView.cs`

**Interfaces:**
- Consumes: completed prototype.
- Produces: smoother first-run behavior with large CSV.

- [ ] **Step 1: Add CSV load timing log**

In `TwinBootstrap`, wrap load with `System.Diagnostics.Stopwatch` and log:

```text
CSV load completed in X.XX seconds.
```

- [ ] **Step 2: Cap UI update frequency**

In `DashboardView` and `StatusPanelView`, update text at most 15 times per second while playback is running. Always update immediately after a slider seek.

Implementation approach:

```csharp
private float nextAllowedUiTime;
private bool forceNextUpdate;
```

- [ ] **Step 3: Avoid duplicate trajectory allocations**

Ensure `TrajectoryView` stores sampled points in a single `Vector3[]` and updates travelled line positions with a reusable buffer or by setting `positionCount` only when the sampled index changes.

- [ ] **Step 4: Run real data smoke test**

Press Play with `D:\Desktop\digital twin\2.csv`.

Expected:

- CSV loads once.
- No unhandled exception in Console.
- Playback reaches first visible movement.
- Slider can jump to 50% and 95%.
- UI shows values close to the CSV ranges observed in the spec.

- [ ] **Step 5: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts
git commit -m "perf: smooth large CSV replay"
```

---

### Task 10: Windows Build Script And Executable

**Files:**
- Create: `D:\Desktop\digital twin\UnderwaterGliderTwin\Assets\Editor\BuildWindows.cs`
- Modify: `D:\Desktop\digital twin\UnderwaterGliderTwin\ProjectSettings\EditorBuildSettings.asset`
- Output: `D:\Desktop\digital twin\Builds\UnderwaterGliderTwin\UnderwaterGliderTwin.exe`

**Interfaces:**
- Produces: deterministic Windows build command.

- [ ] **Step 1: Implement build script**

Create `BuildWindows.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnderwaterGliderTwin.Editor
{
    public static class BuildWindows
    {
        public static void Build()
        {
            var outputDirectory = Path.GetFullPath(Path.Combine("..", "Builds", "UnderwaterGliderTwin"));
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "UnderwaterGliderTwin.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"Build failed: {report.summary.result}");
            }
        }
    }
}
```

- [ ] **Step 2: Add `Main.unity` to build settings**

Open Unity Build Settings and ensure `Assets/Scenes/Main.unity` is scene index `0`.

- [ ] **Step 3: Run Windows build**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' `
  -quit -batchmode `
  -projectPath 'D:\Desktop\digital twin\UnderwaterGliderTwin' `
  -executeMethod UnderwaterGliderTwin.Editor.BuildWindows.Build
```

Expected:

- exit code `0`
- `D:\Desktop\digital twin\Builds\UnderwaterGliderTwin\UnderwaterGliderTwin.exe` exists

- [ ] **Step 4: Run executable with explicit CSV path**

Run:

```powershell
& 'D:\Desktop\digital twin\Builds\UnderwaterGliderTwin\UnderwaterGliderTwin.exe' --csv 'D:\Desktop\digital twin\2.csv'
```

Expected:

- Window opens.
- CSV loads.
- UI and scene match Editor behavior.

- [ ] **Step 5: Commit build script and settings**

Do not commit `Builds/`.

```powershell
git add UnderwaterGliderTwin/Assets/Editor/BuildWindows.cs UnderwaterGliderTwin/ProjectSettings/EditorBuildSettings.asset
git commit -m "build: add Windows player build script"
```

---

### Task 11: Usage Notes And Final Verification

**Files:**
- Create: `D:\Desktop\digital twin\README.md`
- Modify: `D:\Desktop\digital twin\.gitignore`

**Interfaces:**
- Produces: user-facing run instructions.

- [ ] **Step 1: Update `.gitignore` for Unity project and build outputs**

Ensure `.gitignore` excludes:

```gitignore
*.csv
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
UnderwaterGliderTwin/[Ll]ibrary/
UnderwaterGliderTwin/[Tt]emp/
UnderwaterGliderTwin/[Oo]bj/
UnderwaterGliderTwin/[Ll]ogs/
Builds/
```

- [ ] **Step 2: Write `README.md`**

Create a README with:

```markdown
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
```

- [ ] **Step 3: Run full EditMode test suite**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' `
  -quit -batchmode `
  -projectPath 'D:\Desktop\digital twin\UnderwaterGliderTwin' `
  -runTests -testPlatform EditMode `
  -testResults 'D:\Desktop\digital twin\UnderwaterGliderTwin\EditModeResults.xml'
```

Expected: exit code `0`, all EditMode tests pass.

- [ ] **Step 4: Run build verification**

Run Task 10 Step 3 again.

Expected: exit code `0`, executable exists.

- [ ] **Step 5: Run app smoke verification**

Run:

```powershell
& 'D:\Desktop\digital twin\Builds\UnderwaterGliderTwin\UnderwaterGliderTwin.exe' --csv 'D:\Desktop\digital twin\2.csv'
```

Manual checks:

- scene opens to water environment
- glider visible
- Play/Pause works
- slider jump works
- UI fields update
- camera buttons switch views
- alarm area can show warnings

- [ ] **Step 6: Commit docs**

```powershell
git add README.md .gitignore
git commit -m "docs: add run instructions"
```

- [ ] **Step 7: Final repository check**

Run:

```powershell
git status --short --ignored
```

Expected:

- no tracked modifications
- `2.csv` appears as ignored
- `Builds/` appears as ignored after build

## Self-Review Checklist

- Spec coverage:
  - CSV GBK loading: Task 2 and Task 6.
  - 236k-row playback without row GameObjects: Task 3, Task 4, Task 7, Task 9.
  - Coordinate mapping and depth mapping: Task 3.
  - Glider movement and pose: Task 7.
  - Actual trajectory and current replay location: Task 7.
  - UI telemetry, status, controls: Task 8.
  - Camera modes: Task 7 and Task 8.
  - Underwater fog, particles, seabed, light beams: Task 7.
  - Alarms and logs: Task 5 and Task 8.
  - Windows build: Task 10.
  - Usage notes: Task 11.
- Placeholder scan:
  - No unfinished placeholder markers.
  - No unowned features left inside the first-phase scope.
- Type consistency:
  - `TelemetryFrame` fields are consumed by parser, mapper, UI, and alarms using the same names.
  - `PlaybackController.FrameChanged` uses `(TelemetryFrame frame, int index, float progress01)` consistently.
  - `GeoCoordinateMapper.Map` returns `Vector3` for transform and trajectory consumers.
