# Underwater Glider Engineering Improvements

**Goal:** Incrementally improve the underwater glider simulation from a visual engineering approximation toward a traceable, testable mission model.

**Current phase:** Complete

## Phases

- [completed] Phase 1: Use local current and vehicle earth velocity to compute body-frame relative water velocity.
- [completed] Phase 2: Expose geometry, mass, inertia, and hydrodynamic coefficients in the UI with units and validation.
- [completed] Phase 3: Add actuator dynamics for buoyancy, piston/ballast, control surfaces, delays, and energy use.
- [completed] Phase 4: Add sea-trial calibration from CSV telemetry and parameter-fit diagnostics.
- [completed] Phase 5: Add ocean-data quality, configured water-column checks, coverage flags, timestamps, and offline fallback states.
- [completed] Phase 6: Expand validation, reporting, and mission-panel presentation.

## Phase 1 Acceptance Criteria

1. Current is sampled at the vehicle's current depth and represented in the earth frame.
2. Vehicle earth-frame velocity is converted to body-frame relative water velocity before calculating angle of attack, lift, drag, and side force.
3. Ground position continues to integrate using vehicle earth velocity; current is not double-counted.
4. A zero-current case preserves the existing trajectory within tolerance.
5. A cross-current case produces measurable sideslip/drift and finite bounded loads.
6. EditMode tests pass and a Windows build starts with a valid 3D trajectory.
