# XGBoost Engineering Forecast Design

## Goal

Replace the current physics-only runtime prediction option with one XGBoost model family that prioritizes forecast accuracy while remaining practical for a Unity desktop application. The predictor must generate a 900-second future trajectory and key glider state forecasts from telemetry recorded at approximately 10-second intervals.

The model is accepted only when its independent validation results are better than the physics baseline at the agreed forecast horizons and it can execute locally inside Unity without Python.

## Scope

This work includes XGBoost training, export, Unity runtime inference, prediction display integration, and repeatable validation reporting. It does not add neural-network models, remote inference, online retraining, or automated model deployment.

## Data Contract

The source telemetry is `1.csv` or another CSV with the same column layout. Valid samples are spaced at approximately 10 seconds. The training script shall preserve chronological ordering and must never shuffle future samples into training data.

Each prediction consumes the latest 30 valid telemetry frames, representing roughly five minutes of history. Required raw signals are:

- Latitude, longitude, depth, heading, pitch, and roll.
- Derived east, north, vertical, and total speed.
- Current speed or the best available current estimate.
- Target heading, target depth, turn angle, and pitch-control amount.
- Mission/run-state signals when they can be encoded consistently from the source data.

Feature generation must be identical in Python and Unity. The exported artifact is the source of truth for feature order, normalization mean, normalization scale, angle handling, and required-value defaults.

## Forecast Targets

The predictor evaluates 30 forecast anchors: 30, 60, ..., 900 seconds after the latest input frame. At each anchor it predicts six changes relative to that latest frame:

- East displacement in meters.
- North displacement in meters.
- Depth change in meters.
- Wrapped heading change in degrees.
- Pitch change in degrees.
- Roll change in degrees.

Unity reconstructs absolute positions and states from the latest telemetry frame. Vertical position is derived from forecast depth rather than trained separately, avoiding the previous duplicate `y_m` and depth targets. The viewer receives a 10-second trajectory by interpolating between the 30 anchor points; heading interpolation must follow the shortest wrapped angular path.

## Training Design

Training uses six direct multi-horizon XGBoost regressors, one per target. A model input is the normalized 30-frame feature window plus the requested horizon in seconds. This lets each output model share statistical structure across all 30 horizons, rather than writing a separate model for every target and horizon.

The initial training configuration is intentionally bounded:

- Histogram tree method, squared-error objective, deterministic random seed.
- Hyperparameter search limited to depth, learning rate, estimator count, row subsampling, and column subsampling.
- Candidate selection based on chronological validation performance at 30, 300, and 900 seconds, with 900-second endpoint error weighted highest.
- The final model is retrained only with parameters already shown to improve validation performance; there is no unbounded parameter sweep.

The physics baseline remains a validation comparator, not a runtime fallback. Its output must be generated from exactly the same held-out windows and forecast horizons.

## Runtime Artifact

`Models/XGBoost` shall contain a versioned, self-sufficient artifact set:

- `manifest.json`: artifact version, model type, sample interval, history length, horizons, and hashes or sizes of companion files.
- `feature_schema.json`: ordered feature definitions, normalization parameters, categorical mappings, and default values.
- `models/*.json`: the six XGBoost trees exported in a Unity-readable JSON representation.
- `validation_report.json`: data split definition, baseline and XGBoost metrics, latency measurements, and acceptance decision.
- `validation_predictions.npz`: offline diagnostic data, retained for Python analysis only.

Python `pickle` files may be retained as optional training diagnostics but Unity must not depend on them. The Unity evaluator loads the exported trees, evaluates threshold branches, and returns model outputs using only C# and the artifact files.

## Runtime Behavior and Failure Handling

At prediction time, Unity validates artifact version, model count, feature-schema compatibility, input history length, and finite feature values before inference. A valid XGBoost artifact is the only selectable prediction model in the final UI.

If the artifact is missing, malformed, incompatible, or inference returns non-finite values, the prediction request fails visibly with a concrete diagnostic message and does not silently label physics output as XGBoost. The most recently successful prediction remains displayed until a later valid request replaces it.

The runtime response includes the reconstructed trajectory, the six state sequences, model name, latency, and confidence intervals. Confidence intervals are lookup values derived from held-out validation residuals bucketed by forecast horizon, depth band, and navigation phase. They are empirical error bounds, not a claim that XGBoost supplies native probabilities.

## Validation Protocol

Validation is repeatable and chronological. The default split uses the earliest 70 percent of valid telemetry for training and the final 30 percent for validation. A second scenario split must hold out complete depth/mission segments when enough data exists; its purpose is to reveal whether the model only memorizes a continuous voyage.

Every candidate and the physics baseline report the following at 30, 300, and 900 seconds, plus aggregate values over all anchors:

- Three-dimensional trajectory RMSE and MAE.
- Endpoint position error.
- Depth MAE.
- Absolute wrapped heading error.
- Pitch and roll MAE.
- P50 and P90 inference latency in the Unity-compatible evaluator.
- Validation coverage and the empirical P90 error bounds used as confidence intervals.

The report must show the number of validation windows, source CSV identity, artifact version, training configuration, and model file sizes so an engineering reviewer can reproduce the result.

## Acceptance Criteria

The XGBoost artifact is eligible to become the sole prediction model only when all of the following are true:

- It produces the full 900-second trajectory and all six forecast state sequences from 30 valid input frames.
- It improves the held-out 900-second endpoint error and 900-second depth MAE versus the physics baseline.
- It does not regress the aggregate trajectory RMSE or heading MAE versus the physics baseline.
- The Unity-compatible evaluator completes a forecast within the UI real-time budget, initially P90 at or below 50 ms on the target development machine.
- Artifact validation rejects corrupt or incompatible files with a user-visible diagnostic.
- Automated tests cover feature-schema compatibility, tree evaluation on known fixtures, trajectory reconstruction, angular wrapping, artifact rejection, and validation-report generation.

If a candidate fails an accuracy criterion, it remains an experiment and is not exposed as the application prediction model. If it fails the latency criterion, the training/export design must be reduced before any UI integration proceeds.

## Implementation Boundaries

The existing `PredictionTraining/train_models.py` and Unity prediction classes are the intended integration points. Changes must be limited to XGBoost training/export, prediction runtime, artifact loading, XGBoost-only UI selection, and associated tests. Existing physics simulation behavior outside the prediction selection flow is out of scope.
