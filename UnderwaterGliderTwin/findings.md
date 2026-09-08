# Findings

## Current model

- `SimulationTrajectoryGenerator` samples `OceanCurrentProfile.GetVelocity(depth)` and passes an east/north current vector to `GliderDynamicsIntegrator`.
- `GliderDynamicsIntegrator` currently treats `WaterVelocityEndMps` as the velocity used for hydrodynamic loads and adds `currentEndMps` only when integrating position.
- The integrator applies a lateral-current heuristic to desired roll and heading, but the full earth-to-body relative-water-velocity transform is not yet explicit.
- Current layers are selected by containment, falling back to the nearest layer when a depth is outside coverage.
- The current profile can come from Copernicus Marine data, local cache, or manual layers.

## Engineering decision

Phase 1 will make the velocity frames explicit. The state will carry earth-frame vehicle velocity, the current will be sampled in earth coordinates, and the integrator will derive relative water velocity before force and moment calculations. Position will use earth-frame vehicle velocity directly, preventing double-counting.

## Phase 1 result

- `GliderDynamicsState.EarthVelocityEndMps` now stores the earth-frame vehicle velocity.
- `GliderDynamicsState.WaterVelocityEndMps` is refreshed as earth velocity minus local current before hydrodynamic loads are reported.
- Position integrates earth velocity once; current is no longer added a second time in the position update.
- Hydrodynamic side force uses relative water velocity, while the navigation controller keeps a separate lateral-current ground-track correction.

## Phase 2 result

- The dedicated ocean-current drawer now contains editable core dynamics parameters with units.
- The UI validates mass, geometry, inertia, lift slope, and base drag before applying a simulation profile.
- Sea-trial and calm-water presets refresh the visible values instead of silently changing hidden settings.

## Phase 3 result

- Buoyancy command now maps to a rate-limited piston position instead of an instantaneous force jump.
- Roll, pitch, and yaw control surfaces have bounded deflection and response time constants.
- Actuator power is calculated from piston travel rate and control-surface rate, then included in battery power.
- Simulation diagnostics now carry piston position, three-axis control-surface deflection, and actuator power for UI and reporting.
- The telemetry details panel exposes these values with `mm`, `deg`, and `W` units.

## Phase 4 result

- `SeaTrialCalibrator` derives consecutive ground velocity from longitude/latitude and elapsed time.
- When current layers exist, the calibrator subtracts the depth-local current before estimating median water speed.
- The recommended cruise speed minimizes the constant-speed RMSE for the loaded samples; pitch and roll amplitudes use robust 90th-percentile values.
- The data-input drawer now has a `CSV 海试标定` action that writes recommendations back into the simulation template and reports sample count, current coverage, and RMSE before/after.

## Phase 5 result

- `OceanCurrentQualityEvaluator` distinguishes missing data, partial depth coverage, depth gaps, stale retrievals, and ready data.
- The ocean-current drawer now reports source class, requested mission depth, coverage, and quality state.
- Editing a fetched layer converts the state to manual data so the UI does not falsely claim it is still an untouched online result.

## Phase 6 result

- `MissionValidationEvaluator` derives a depth alarm limit from the mission water column and target depth, using a 5% plus 5 m engineering margin.
- Deep missions no longer inherit the previous fixed 1000 m alarm threshold.
- The mission status panel now shows an engineering-validation row and promotes incomplete current coverage into the mission health state.
- The alarm limit is derived from the configured mission depth, never from an already over-depth telemetry sample.
