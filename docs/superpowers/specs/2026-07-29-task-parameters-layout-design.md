# Task Parameters Drawer Redesign

## Goal

Make the runtime `任务参数` drawer readable and predictable at 1920×1080 and 1280×720 while preserving existing behavior, leaf object names, and event handlers.

## Design

The drawer remains anchored immediately below the command-center header. Its collapsed state is a 48 px header. Its expanded state is capped at 35% of the canvas height and contains one vertical `ScrollRect`; the header and collapse toggle are outside the viewport and always receive pointer input.

The content is divided into three vertical section cards:

1. **任务与预测** — CSV path/load, model selection, prediction horizon, apply, and start/stop prediction.
2. **仿真参数** — cycle count, duration, target depth, water-column depth, heading, heading delta, pitch, roll, and run simulation.
3. **海流与航段** — longitude/latitude, layer depth and velocity values, layer navigation/edit actions, online fetch, and flight-leg settings.

Every parameter is a `FieldCard` wrapper with a label above an input. Units are included in labels. Long text inputs (CSV path) use a full-width row. Buttons have their own row or action column and never share a cell with an unrelated label.

At wide layouts the section grid uses three columns. At 1280 px it uses two columns. If the calculated card width would be below the minimum readable width, it falls back to one column. Labels may wrap; the CSV path and other long inputs always span the available width.

The existing leaf controls keep their names (`CsvPathInput`, `SimulationCyclesInput`, `OceanCurrentLookupButton`, etc.). New section/card wrappers may be added without changing leaf names or their listeners.

The ocean-current editor is a separate modal canvas with a sorting order above the main command canvas and a full-screen raycast blocker. It is opened above both the task drawer and the 3D viewport; closing it restores the prior drawer state.

## Acceptance criteria

- Each label and its input are under the same field-card transform.
- The collapse toggle remains clickable when the drawer is expanded and toggles text/state on every click.
- The expanded drawer never covers the command-center header or playback controls.
- The ocean-current modal receives all pointer input while visible and renders above the main drawer.
- 1920×1080 uses three columns; 1280×720 uses two columns unless the minimum card width forces one column.
- EditMode tests pass and a packaged Player screenshot confirms the layout at both resolutions.
