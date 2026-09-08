# Progress

## 2026-07-15

- Confirmed the user wants the remaining engineering improvements completed one at a time with detailed verification.
- Created the phased engineering plan.
- Phase 1 is in progress: current-relative velocity coupling.
- Previous baseline: 131/131 EditMode tests passed; Windows build succeeded after automatic mission-volume fitting changes.
- Phase 1 complete: added explicit earth-frame velocity and relative-water-velocity coupling in `GliderDynamicsState`, `SimulationTrajectoryGenerator`, and `GliderDynamicsIntegrator`.
- Added regression coverage for current-relative velocity and sideslip. EditMode tests: 132/132 passed.
- Windows build succeeded. Runtime verification screenshot: `D:\relative-water-speed-phase1.png`.
- Phase 2 is next: expose geometry, mass, inertia, and hydrodynamic coefficients in the UI with units and validation.
- Phase 2 complete: added the dynamics parameter drawer fields and profile persistence for mass, reference geometry, three-axis inertia, lift slope, and base drag.
- EditMode tests: 133/133 passed. Windows build succeeded. Runtime startup screenshot: `D:\dynamics-parameters-baseline.png`.
- Phase 3 complete: added rate-limited piston buoyancy, bounded roll/pitch/yaw control-surface response, and actuator power consumption.
- Added diagnostics fields and detail-panel UI for piston position, control-surface deflections, and actuator power.
- EditMode tests: 134/134 passed. Windows build succeeded.
- Runtime verification screenshot: `D:\actuator-phase3.png` using a 1200 m simulated mission and non-zero current.
- Phase 4 complete: added `SeaTrialCalibrator` with current-corrected water-speed estimation, robust pitch/roll amplitude recommendations, and before/after speed RMSE.
- Added `CSV 海试标定` to the ocean-current/dynamics drawer; it updates the simulation template and shows coverage and calibration diagnostics.
- EditMode tests: 135/135 passed. Windows build succeeded.
- Runtime verification screenshot: `D:\sea-trial-calibration-phase4.png` using a 1200 m simulated mission and non-zero current.
- Phase 5 complete: added ocean-current quality evaluation for missing/partial/gapped/stale/ready data and surfaced it in the current configuration drawer.
- Phase 6 complete: deep-mission alarm limits now derive from task depth, and the mission status panel shows an engineering-validation result.
- EditMode tests: 141/141 passed. Windows build succeeded.
- Runtime verification screenshot: `D:\mission-validation-phase6.png`; the 1200 m mission displays `校核通过 | 上限 1265 m`.
