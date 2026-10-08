from __future__ import annotations

import argparse
import hashlib
import json
import math
import pickle
import re
import time
import shutil
from datetime import datetime
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import numpy as np
import pandas as pd

try:
    from sklearn.multioutput import MultiOutputRegressor
    from sklearn.preprocessing import StandardScaler
except Exception:
    MultiOutputRegressor = None
    StandardScaler = None

try:
    import xgboost as xgb
except Exception:
    xgb = None

try:
    import torch
    from torch import nn
    from torch.utils.data import DataLoader, TensorDataset
except Exception:
    torch = None
    nn = None
    DataLoader = None
    TensorDataset = None


TIME_PATTERN = re.compile(r"-?\d+")
CSV_COLUMNS = {
    "raw_time": 0,
    "voltage_24v": 2,
    "current_24a": 3,
    "work_mode": 13,
    "run_state": 14,
    "turn_angle_deg": 17,
    "piston_mm": 18,
    "longitude_deg": 21,
    "latitude_deg": 22,
    "depth_m": 23,
    "altitude_m": 24,
    "heading_deg": 25,
    "pitch_deg": 26,
    "roll_deg": 27,
    "propeller_rpm": 28,
    "target_segment": 29,
    "target_heading_deg": 30,
    "target_depth_m": 31,
    "target_altitude_m": 32,
    "battery_percent": 41,
}
FEATURE_COLUMNS = [
    "elapsed_seconds",
    "latitude_deg",
    "longitude_deg",
    "depth_m",
    "heading_deg",
    "pitch_deg",
    "roll_deg",
    "velocity_x",
    "velocity_y",
    "velocity_z",
    "current_speed",
    "x_m",
    "y_m",
    "z_m",
]
TARGET_COLUMNS = [
    "x_m",
    "y_m",
    "z_m",
    "heading_deg",
    "depth_m",
    "pitch_deg",
    "roll_deg",
]


@dataclass
class DatasetBundle:
    train_features: np.ndarray
    train_targets: np.ndarray
    val_features: np.ndarray
    val_targets: np.ndarray
    val_target_frames: np.ndarray
    scaler_mean: np.ndarray
    scaler_scale: np.ndarray
    metadata: dict


@dataclass(frozen=True)
class FeatureSchema:
    names: tuple[str, ...]
    mean: np.ndarray
    scale: np.ndarray


@dataclass(frozen=True)
class PreparedForecastData:
    schema: FeatureSchema
    train_features: np.ndarray
    train_targets: dict[str, np.ndarray]
    validation_features: np.ndarray
    validation_targets: dict[str, np.ndarray]
    metadata: dict


class ArtifactValidationError(RuntimeError):
    pass


if torch is not None and nn is not None:
    class LstmForecaster(nn.Module):
        def __init__(self, feature_dim: int, hidden_dim: int, target_dim: int, horizon: int) -> None:
            super().__init__()
            self.horizon = horizon
            self.target_dim = target_dim
            self.lstm = nn.LSTM(feature_dim, hidden_dim, batch_first=True, num_layers=2, dropout=0.1)
            self.head = nn.Sequential(
                nn.Linear(hidden_dim, hidden_dim),
                nn.ReLU(),
                nn.Linear(hidden_dim, horizon * target_dim),
            )

        def forward(self, inputs: torch.Tensor) -> torch.Tensor:
            outputs, _ = self.lstm(inputs)
            return self.head(outputs[:, -1, :]).reshape(-1, self.horizon, self.target_dim)


    class CnnLstmForecaster(nn.Module):
        def __init__(self, feature_dim: int, hidden_dim: int, target_dim: int, horizon: int) -> None:
            super().__init__()
            self.horizon = horizon
            self.target_dim = target_dim
            self.conv = nn.Sequential(
                nn.Conv1d(feature_dim, 32, kernel_size=3, padding=1),
                nn.ReLU(),
                nn.Conv1d(32, 64, kernel_size=3, padding=1),
                nn.ReLU(),
            )
            self.lstm = nn.LSTM(64, hidden_dim, batch_first=True, num_layers=2, dropout=0.1)
            self.head = nn.Sequential(
                nn.Linear(hidden_dim, hidden_dim),
                nn.ReLU(),
                nn.Linear(hidden_dim, horizon * target_dim),
            )

        def forward(self, inputs: torch.Tensor) -> torch.Tensor:
            features = self.conv(inputs.transpose(1, 2)).transpose(1, 2)
            outputs, _ = self.lstm(features)
            return self.head(outputs[:, -1, :]).reshape(-1, self.horizon, self.target_dim)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train underwater glider prediction models from 1.csv.")
    parser.add_argument("--csv", default=str(Path("..") / "1.csv"))
    parser.add_argument("--output-root", default=str(Path("..") / "Models"))
    parser.add_argument("--train-ratio", type=float, default=0.7)
    parser.add_argument("--window-size", type=int, default=30)
    parser.add_argument("--horizon", type=int, default=30)
    parser.add_argument("--epochs", type=int, default=20)
    parser.add_argument("--batch-size", type=int, default=128)
    parser.add_argument("--max-origins-per-segment", type=int, default=300)
    parser.add_argument("--models", nargs="+", default=["xgboost"])
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    csv_path = Path(args.csv).resolve()
    output_root = Path(args.output_root).resolve()
    output_root.mkdir(parents=True, exist_ok=True)

    frame_table = load_frame_table(csv_path)
    dataset = None

    summary = {
        "csv_path": str(csv_path),
        "row_count": int(len(frame_table)),
        "train_ratio": args.train_ratio,
        "window_size": args.window_size,
        "horizon": args.horizon,
        "models": {},
    }

    for model_name in args.models:
        normalized_name = model_name.strip().lower()
        print(f"Training {normalized_name}...")
        if normalized_name == "physics":
            dataset = dataset or build_dataset(frame_table, args.window_size, args.horizon, args.train_ratio)
            summary["models"]["physics"] = run_physics_baseline(dataset, output_root / "Physics")
        elif normalized_name == "xgboost":
            cleaned = clean_navigation_rows(frame_table)
            segments = [add_derived_features(segment.copy()) for segment in split_continuous_segments(cleaned)]
            summary["models"]["xgboost"] = train_xgboost_artifact_from_segments(
                segments,
                output_root / "XGBoost",
                train_ratio=args.train_ratio,
                max_origins_per_segment=args.max_origins_per_segment,
            )
        elif normalized_name == "lstm":
            dataset = dataset or build_dataset(frame_table, args.window_size, args.horizon, args.train_ratio)
            summary["models"]["lstm"] = run_torch_model(dataset, output_root / "LSTM", "lstm", args.epochs, args.batch_size)
        elif normalized_name in {"cnnlstm", "cnn-lstm"}:
            dataset = dataset or build_dataset(frame_table, args.window_size, args.horizon, args.train_ratio)
            summary["models"]["cnnlstm"] = run_torch_model(dataset, output_root / "CNNLSTM", "cnnlstm", args.epochs, args.batch_size)
        else:
            summary["models"][normalized_name] = {"status": "skipped", "reason": "unsupported model name"}

    summary_path = output_root / "training_summary.json"
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"Summary written to {summary_path}")


def load_frame_table(csv_path: Path) -> pd.DataFrame:
    try:
        raw = pd.read_csv(csv_path, encoding="gbk", header=0, dtype=str, low_memory=False)
    except UnicodeDecodeError:
        raw = pd.read_csv(csv_path, encoding="utf-8-sig", header=0, dtype=str, low_memory=False)
    raw = raw[raw.iloc[:, 0].astype(str).str.contains(r"\d", regex=True)].copy()
    frame = pd.DataFrame()
    for name, index in CSV_COLUMNS.items():
        frame[name] = raw.iloc[:, index]

    for name in frame.columns:
        if name not in {"raw_time", "work_mode", "run_state"}:
            frame[name] = pd.to_numeric(frame[name], errors="coerce")

    frame["elapsed_seconds"] = frame["raw_time"].apply(parse_elapsed_seconds).astype(float)
    frame = frame.replace([np.inf, -np.inf], np.nan)
    frame = frame.dropna(subset=["longitude_deg", "latitude_deg", "depth_m", "heading_deg", "pitch_deg", "roll_deg", "elapsed_seconds"])
    frame = frame[(frame["longitude_deg"].abs() > 0.01) & (frame["latitude_deg"].abs() > 0.01)].reset_index(drop=True)
    frame = add_derived_features(frame)
    frame = frame.ffill()
    return frame


def add_derived_features(frame: pd.DataFrame) -> pd.DataFrame:
    meters_per_degree_latitude = 111320.0
    average_latitude_rad = np.deg2rad(frame["latitude_deg"].rolling(2).mean().fillna(frame["latitude_deg"]))
    meters_per_degree_longitude = meters_per_degree_latitude * np.cos(average_latitude_rad)

    delta_seconds = frame["elapsed_seconds"].diff().where(lambda values: values > 0).fillna(1.0).clip(lower=1e-3)
    east_meters = frame["longitude_deg"].diff().fillna(0.0) * meters_per_degree_longitude
    north_meters = frame["latitude_deg"].diff().fillna(0.0) * meters_per_degree_latitude
    vertical_meters = -(frame["depth_m"].diff().fillna(0.0))

    frame["velocity_x"] = east_meters / delta_seconds
    frame["velocity_y"] = vertical_meters / delta_seconds
    frame["velocity_z"] = north_meters / delta_seconds
    frame["current_speed"] = np.sqrt(frame["velocity_x"] ** 2 + frame["velocity_y"] ** 2 + frame["velocity_z"] ** 2)
    frame["x_m"] = east_meters.cumsum()
    frame["z_m"] = north_meters.cumsum()
    frame["y_m"] = -frame["depth_m"]
    return frame


def clean_navigation_rows(frame: pd.DataFrame, max_horizontal_speed_mps: float = 3.0) -> pd.DataFrame:
    """Keep only chronologically continuous, physically plausible navigation samples."""
    valid_rows = []
    previous = None
    for _, row in frame.iterrows():
        longitude = float(row["longitude_deg"])
        latitude = float(row["latitude_deg"])
        elapsed = float(row["elapsed_seconds"])
        if not np.isfinite(longitude) or not np.isfinite(latitude) or abs(longitude) <= 0.01 or abs(latitude) <= 0.01:
            continue

        if previous is not None:
            delta_seconds = elapsed - float(previous["elapsed_seconds"])
            if delta_seconds <= 0.0:
                continue
            if delta_seconds > 60.0:
                valid_rows.append(row)
                previous = row
                continue
            east_meters, north_meters = local_east_north_meters(
                float(previous["longitude_deg"]),
                float(previous["latitude_deg"]),
                longitude,
                latitude,
            )
            if math.hypot(east_meters, north_meters) / delta_seconds > max_horizontal_speed_mps:
                continue

        valid_rows.append(row)
        previous = row

    return pd.DataFrame(valid_rows).reset_index(drop=True)


def split_continuous_segments(frame: pd.DataFrame, max_gap_seconds: float = 60.0) -> list[pd.DataFrame]:
    if frame.empty:
        return []
    segment_ids = (frame["elapsed_seconds"].diff().fillna(0.0) > max_gap_seconds).cumsum()
    return [segment.reset_index(drop=True) for _, segment in frame.groupby(segment_ids)]


def resample_telemetry(frame: pd.DataFrame, sample_interval_seconds: int = 10) -> pd.DataFrame:
    """Hold already observed telemetry on a fixed grid; never interpolate future inputs."""
    if frame.empty:
        return frame.copy()
    start_seconds = float(frame["elapsed_seconds"].iloc[0])
    end_seconds = float(frame["elapsed_seconds"].iloc[-1])
    target_seconds = np.arange(start_seconds, end_seconds + 1e-7, sample_interval_seconds, dtype=np.float64)
    indexed = frame.set_index("elapsed_seconds")
    numeric_columns = indexed.select_dtypes(include=[np.number]).columns.tolist()
    resampled = indexed[numeric_columns].reindex(indexed.index.union(target_seconds)).sort_index().ffill()
    resampled = resampled.reindex(target_seconds)
    resampled.index.name = "elapsed_seconds"
    return resampled.reset_index()


def build_feature_schema(training_frame: pd.DataFrame) -> FeatureSchema:
    names = (
        "depth_m", "heading_deg", "heading_sin", "heading_cos", "pitch_deg", "roll_deg",
        "velocity_x", "velocity_y", "velocity_z", "current_speed",
        "target_heading_residual_deg", "target_depth_residual_m", "turn_angle_deg", "piston_mm",
        "depth_mean", "depth_std", "pitch_mean", "pitch_std",
        "velocity_mean", "velocity_std", "depth_trend_mps", "heading_trend_degps", "descending_flag",
    )
    values = np.asarray([summarize_history(training_frame, len(training_frame) - 1, names)], dtype=np.float32)
    mean = values.mean(axis=0)
    scale = values.std(axis=0)
    scale[scale == 0.0] = 1.0
    return FeatureSchema(names, mean.astype(np.float32), scale.astype(np.float32))


def summarize_history(frame: pd.DataFrame, end_index: int, feature_names: tuple[str, ...]) -> np.ndarray:
    start_index = max(0, end_index - 29)
    history = frame.iloc[start_index:end_index + 1]
    current = history.iloc[-1]
    depth_values = history["depth_m"].to_numpy(dtype=np.float32)
    pitch_values = history["pitch_deg"].to_numpy(dtype=np.float32)
    velocity_values = history[["velocity_x", "velocity_y", "velocity_z"]].to_numpy(dtype=np.float32)
    elapsed_values = history["elapsed_seconds"].to_numpy(dtype=np.float32)
    duration = max(float(elapsed_values[-1] - elapsed_values[0]), 0.001)
    heading_trend = wrap_degrees(float(history["heading_deg"].iloc[-1]) - float(history["heading_deg"].iloc[0])) / duration
    values = {
        "depth_m": float(current["depth_m"]),
        "heading_deg": float(current["heading_deg"]),
        "heading_sin": math.sin(math.radians(float(current["heading_deg"]))),
        "heading_cos": math.cos(math.radians(float(current["heading_deg"]))),
        "pitch_deg": float(current["pitch_deg"]),
        "roll_deg": float(current["roll_deg"]),
        "velocity_x": float(current["velocity_x"]),
        "velocity_y": float(current["velocity_y"]),
        "velocity_z": float(current["velocity_z"]),
        "current_speed": float(current["current_speed"]),
        "target_heading_residual_deg": wrap_degrees(float(current["target_heading_deg"]) - float(current["heading_deg"])),
        "target_depth_residual_m": float(current["target_depth_m"] - current["depth_m"]),
        "turn_angle_deg": float(current["turn_angle_deg"]),
        "piston_mm": float(current["piston_mm"]),
        "depth_mean": float(np.mean(depth_values)),
        "depth_std": float(np.std(depth_values)),
        "pitch_mean": float(np.mean(pitch_values)),
        "pitch_std": float(np.std(pitch_values)),
        "velocity_mean": float(np.mean(np.linalg.norm(velocity_values, axis=1))),
        "velocity_std": float(np.std(np.linalg.norm(velocity_values, axis=1))),
        "depth_trend_mps": float((depth_values[-1] - depth_values[0]) / duration),
        "heading_trend_degps": heading_trend,
        "descending_flag": 1.0 if depth_values[-1] >= depth_values[0] else 0.0,
    }
    return np.asarray([values[name] for name in feature_names], dtype=np.float32)


def build_forecast_rows(
    frame: pd.DataFrame,
    origin_indices: np.ndarray,
    horizons_seconds: tuple[int, ...],
    schema: FeatureSchema,
) -> tuple[np.ndarray, dict[str, np.ndarray]]:
    features = []
    targets = {
        "east_displacement_m": [], "north_displacement_m": [], "depth_delta_m": [],
        "heading_delta_deg": [], "pitch_delta_deg": [], "roll_delta_deg": [],
    }
    elapsed_values = frame["elapsed_seconds"].to_numpy(dtype=np.float64)
    for origin_index in origin_indices:
        origin = frame.iloc[int(origin_index)]
        base_features = summarize_history(frame, int(origin_index), schema.names)
        for horizon_seconds in horizons_seconds:
            target_time = float(origin["elapsed_seconds"]) + float(horizon_seconds)
            target_index = int(np.searchsorted(elapsed_values, target_time, side="left"))
            if target_index >= len(frame) or not math.isclose(float(elapsed_values[target_index]), target_time, abs_tol=0.001):
                continue
            target = frame.iloc[target_index]
            east_meters, north_meters = local_east_north_meters(
                float(origin["longitude_deg"]), float(origin["latitude_deg"]),
                float(target["longitude_deg"]), float(target["latitude_deg"]),
            )
            features.append(np.append(base_features, float(horizon_seconds)))
            targets["east_displacement_m"].append(east_meters)
            targets["north_displacement_m"].append(north_meters)
            targets["depth_delta_m"].append(float(target["depth_m"] - origin["depth_m"]))
            targets["heading_delta_deg"].append(wrap_degrees(float(target["heading_deg"] - origin["heading_deg"])))
            targets["pitch_delta_deg"].append(float(target["pitch_deg"] - origin["pitch_deg"]))
            targets["roll_delta_deg"].append(float(target["roll_deg"] - origin["roll_deg"]))

    return np.asarray(features, dtype=np.float32), {name: np.asarray(values, dtype=np.float32) for name, values in targets.items()}


def prepare_segmented_forecast_data(
    segments: list[pd.DataFrame],
    train_ratio: float = 0.7,
    max_origins_per_segment: int = 300,
    history_length: int = 30,
    sample_interval_seconds: int = 10,
) -> PreparedForecastData:
    """Create a bounded, chronological holdout dataset without crossing telemetry segment boundaries."""
    horizons = tuple(range(30, 901, 30))
    max_horizon_steps = max(horizons) // sample_interval_seconds
    minimum_rows = history_length + max_horizon_steps + 1
    prepared_segments = [
        resample_telemetry(segment, sample_interval_seconds)
        for segment in segments
        if len(segment) >= minimum_rows
    ]
    prepared_segments = [segment for segment in prepared_segments if len(segment) >= minimum_rows]
    if len(prepared_segments) < 2:
        raise ValueError("At least two telemetry segments with 900-second coverage are required.")
    if not 0.0 < train_ratio < 1.0:
        raise ValueError("train_ratio must be between 0 and 1 for held-out segment validation.")
    if max_origins_per_segment < 1:
        raise ValueError("max_origins_per_segment must be positive.")

    split_index = int(len(prepared_segments) * train_ratio)
    split_index = min(max(split_index, 1), len(prepared_segments) - 1)
    train_segments = prepared_segments[:split_index]
    validation_segments = prepared_segments[split_index:]
    schema = build_feature_schema(pd.concat(train_segments, ignore_index=True))

    def capped_origins(segment: pd.DataFrame) -> np.ndarray:
        candidates = np.arange(history_length - 1, len(segment) - max_horizon_steps, dtype=np.int32)
        if len(candidates) <= max_origins_per_segment:
            return candidates
        selection = np.linspace(0, len(candidates) - 1, max_origins_per_segment, dtype=np.int32)
        return candidates[selection]

    def build_rows_for_segments(source_segments: list[pd.DataFrame]) -> tuple[np.ndarray, dict[str, np.ndarray], int]:
        feature_chunks = []
        target_chunks: dict[str, list[np.ndarray]] = {}
        origin_count = 0
        for segment in source_segments:
            origins = capped_origins(segment)
            features, targets = build_forecast_rows(segment, origins, horizons, schema)
            feature_chunks.append(features)
            origin_count += len(origins)
            for target_name, values in targets.items():
                target_chunks.setdefault(target_name, []).append(values)
        if not feature_chunks:
            raise ValueError("No forecast rows could be built from telemetry segments.")
        return (
            np.concatenate(feature_chunks, axis=0),
            {target_name: np.concatenate(values, axis=0) for target_name, values in target_chunks.items()},
            origin_count,
        )

    train_features, train_targets, train_origin_count = build_rows_for_segments(train_segments)
    validation_features, validation_targets, validation_origin_count = build_rows_for_segments(validation_segments)
    return PreparedForecastData(
        schema=schema,
        train_features=train_features,
        train_targets=train_targets,
        validation_features=validation_features,
        validation_targets=validation_targets,
        metadata={
            "eligible_segment_count": len(prepared_segments),
            "train_segment_count": len(train_segments),
            "validation_segment_count": len(validation_segments),
            "train_origin_count": train_origin_count,
            "validation_origin_count": validation_origin_count,
            "max_origins_per_segment": max_origins_per_segment,
            "history_length": history_length,
            "sample_interval_seconds": sample_interval_seconds,
        },
    )


def local_east_north_meters(origin_longitude: float, origin_latitude: float, longitude: float, latitude: float) -> tuple[float, float]:
    north = (latitude - origin_latitude) * 111320.0
    east = (longitude - origin_longitude) * 111320.0 * math.cos(math.radians((latitude + origin_latitude) * 0.5))
    return east, north


def wrap_degrees(value: float) -> float:
    return (value + 180.0) % 360.0 - 180.0


def export_booster_as_flat_tree_json(booster, destination: Path) -> Path:
    """Export XGBoost's tree dump into JsonUtility-friendly contiguous node arrays."""
    destination.parent.mkdir(parents=True, exist_ok=True)
    trees = []
    for raw_tree in booster.get_dump(dump_format="json"):
        source_tree = json.loads(raw_tree)
        source_nodes = []

        def visit(node: dict) -> None:
            source_nodes.append(node)
            for child in node.get("children", []):
                visit(child)

        visit(source_tree)
        node_indexes = {int(node["nodeid"]): index for index, node in enumerate(source_nodes)}
        nodes = []
        for index, node in enumerate(source_nodes):
            if "leaf" in node:
                nodes.append({
                    "node_index": index,
                    "feature_index": -1,
                    "threshold": 0.0,
                    "yes_index": -1,
                    "no_index": -1,
                    "missing_index": -1,
                    "leaf_value": float(node["leaf"]),
                })
                continue
            nodes.append({
                "node_index": index,
                "feature_index": int(str(node["split"]).lstrip("f")),
                "threshold": float(node["split_condition"]),
                "yes_index": node_indexes[int(node["yes"])],
                "no_index": node_indexes[int(node["no"])],
                "missing_index": node_indexes[int(node["missing"])],
                "leaf_value": 0.0,
            })
        trees.append({"nodes": nodes})

    config = json.loads(booster.save_config())
    raw_base_score = config["learner"]["learner_model_param"]["base_score"]
    base_score_match = re.search(r"[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?", str(raw_base_score))
    base_score = float(base_score_match.group(0)) if base_score_match else 0.0
    destination.write_text(json.dumps({"base_score": base_score, "tree_count": len(trees), "trees": trees}, separators=(",", ":")), encoding="utf-8")
    return destination


def build_validation_report(
    xgboost_metrics: dict,
    stable_reference_metrics: dict,
    artifact_metadata: dict,
    deployment_metrics: dict | None = None,
) -> dict:
    deployment_metrics = deployment_metrics or xgboost_metrics
    deployment_900 = deployment_metrics["by_horizon"]["900"]
    stable_reference_900 = stable_reference_metrics["by_horizon"]["900"]
    rejection_reasons = []
    if deployment_900["endpoint_error_m"] >= stable_reference_900["endpoint_error_m"]:
        rejection_reasons.append("900-second endpoint error did not improve over stable reference")
    if deployment_900["depth_mae_m"] > stable_reference_900["depth_mae_m"]:
        rejection_reasons.append("900-second depth MAE regressed versus stable reference")
    if deployment_metrics["aggregate"]["trajectory_rmse_m"] > stable_reference_metrics["aggregate"]["trajectory_rmse_m"]:
        rejection_reasons.append("aggregate trajectory RMSE regressed versus stable reference")
    if deployment_metrics["aggregate"]["heading_mae_deg"] > stable_reference_metrics["aggregate"]["heading_mae_deg"]:
        rejection_reasons.append("aggregate heading MAE regressed versus stable reference")
    return {
        "artifact": artifact_metadata,
        "xgboost": xgboost_metrics,
        "deployment": deployment_metrics,
        "stable_reference": stable_reference_metrics,
        "accepted": not rejection_reasons,
        "rejection_reasons": rejection_reasons,
    }


def select_output_sources(xgboost_mae: dict[str, float], stable_mae: dict[str, float]) -> dict[str, str]:
    return {
        target: "xgboost" if xgboost_mae.get(target, float("inf")) < stable_mae.get(target, float("inf")) else "stable"
        for target in (
            "east_displacement_m", "north_displacement_m", "depth_delta_m",
            "heading_delta_deg", "pitch_delta_deg", "roll_delta_deg",
        )
    }


def xgboost_candidate_configs(estimator_count: int) -> tuple[dict, ...]:
    return (
        {
            "name": "balanced",
            "parameters": {
                "n_estimators": estimator_count, "max_depth": 5, "learning_rate": 0.05,
                "subsample": 0.85, "colsample_bytree": 0.85,
            },
        },
        {
            "name": "regularized_long_horizon",
            "parameters": {
                "n_estimators": max(2, int(round(estimator_count * 1.5))), "max_depth": 4, "learning_rate": 0.04,
                "subsample": 0.9, "colsample_bytree": 0.9, "min_child_weight": 3, "reg_lambda": 2.0,
            },
        },
        {
            "name": "deep_long_horizon",
            "parameters": {
                "n_estimators": max(2, int(round(estimator_count * 1.25))), "max_depth": 6, "learning_rate": 0.035,
                "subsample": 0.8, "colsample_bytree": 0.9, "min_child_weight": 2,
            },
        },
    )


def select_best_xgboost_candidate(candidates: list[dict]) -> dict:
    if not candidates:
        raise ValueError("At least one XGBoost candidate is required.")

    def validation_score(candidate: dict) -> float:
        metrics = candidate["metrics"]
        horizons = metrics["by_horizon"]
        return (
            0.05 * horizons["30"]["endpoint_error_m"]
            + 0.2 * horizons["300"]["endpoint_error_m"]
            + 0.75 * horizons["900"]["endpoint_error_m"]
            + 0.1 * metrics["aggregate"]["trajectory_rmse_m"]
            + 0.1 * metrics["aggregate"]["heading_mae_deg"]
        )

    return min(candidates, key=validation_score)


def train_xgboost_artifact(
    frame: pd.DataFrame,
    output_dir: Path,
    train_ratio: float = 0.7,
    estimator_count: int = 120,
) -> dict:
    max_horizon_steps = 90
    origins = np.arange(29, len(frame) - max_horizon_steps, dtype=np.int32)
    if len(origins) < 10:
        raise ValueError("Telemetry does not contain enough fixed-grid samples for 900-second training.")
    split = max(1, int(len(origins) * train_ratio))
    schema = build_feature_schema(frame.iloc[:origins[split - 1] + 1])
    train_x, train_targets = build_forecast_rows(frame, origins[:split], tuple(range(30, 901, 30)), schema)
    val_x, val_targets = build_forecast_rows(frame, origins[split:], tuple(range(30, 901, 30)), schema)
    output_dir = Path(output_dir)
    staging_dir = _create_artifact_staging_dir(output_dir)
    try:
        report = _train_xgboost_artifact_rows(
            train_x,
            train_targets,
            val_x,
            val_targets,
            schema,
            staging_dir,
            estimator_count,
            {"source_row_count": int(len(frame))},
        )
        validate_artifact_tree(staging_dir)
        publish_artifact_tree_with_rollback(staging_dir, output_dir.parent, output_dir.name)
        return report
    except Exception:
        _remove_tree_if_exists(staging_dir)
        raise


def train_xgboost_artifact_from_segments(
    segments: list[pd.DataFrame],
    output_dir: Path,
    train_ratio: float = 0.7,
    max_origins_per_segment: int = 300,
    estimator_count: int = 120,
) -> dict:
    prepared = prepare_segmented_forecast_data(
        segments,
        train_ratio=train_ratio,
        max_origins_per_segment=max_origins_per_segment,
    )
    output_dir = Path(output_dir)
    staging_dir = _create_artifact_staging_dir(output_dir)
    try:
        report = _train_xgboost_artifact_rows(
            prepared.train_features,
            prepared.train_targets,
            prepared.validation_features,
            prepared.validation_targets,
            prepared.schema,
            staging_dir,
            estimator_count,
            {"source_row_count": int(sum(len(segment) for segment in segments))},
        )
        report["segment_split"] = prepared.metadata
        (staging_dir / "validation_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        validate_artifact_tree(staging_dir)
        publish_artifact_tree_with_rollback(staging_dir, output_dir.parent, output_dir.name)
        return report
    except Exception:
        _remove_tree_if_exists(staging_dir)
        raise


def _train_xgboost_artifact_rows(
    train_x: np.ndarray,
    train_targets: dict[str, np.ndarray],
    val_x: np.ndarray,
    val_targets: dict[str, np.ndarray],
    schema: FeatureSchema,
    output_dir: Path,
    estimator_count: int,
    source_metadata: dict,
) -> dict:
    if xgb is None:
        raise RuntimeError("xgboost is required to train the XGBoost artifact")
    output_dir.mkdir(parents=True, exist_ok=True)
    model_dir = output_dir / "models"
    model_dir.mkdir(exist_ok=True)
    candidates = []
    for configuration in xgboost_candidate_configs(estimator_count):
        predictions = {}
        for target_name, target_values in train_targets.items():
            model = xgb.XGBRegressor(
                **configuration["parameters"],
                objective="reg:squarederror",
                tree_method="hist",
                random_state=42,
            )
            model.fit(train_x, target_values)
            predictions[target_name] = model.predict(val_x).astype(np.float32)
        candidates.append({
            **configuration,
            "metrics": evaluate_forecast_metrics(val_x, val_targets, predictions),
        })

    selected_candidate = select_best_xgboost_candidate(candidates)
    predictions = {}
    model_files = []
    for target_name, target_values in train_targets.items():
        model = xgb.XGBRegressor(
            **selected_candidate["parameters"],
            objective="reg:squarederror",
            tree_method="hist",
            random_state=42,
        )
        model.fit(train_x, target_values)
        predictions[target_name] = model.predict(val_x).astype(np.float32)
        relative_path = Path("models") / f"{target_name}.json"
        export_booster_as_flat_tree_json(model.get_booster(), output_dir / relative_path)
        model_files.append(str(relative_path).replace("\\", "/"))

    xgboost_metrics = evaluate_forecast_metrics(val_x, val_targets, predictions)
    zero_predictions = {name: np.zeros_like(values) for name, values in val_targets.items()}
    stable_reference_metrics = evaluate_forecast_metrics(val_x, val_targets, zero_predictions)
    xgboost_target_mae = {name: float(np.mean(np.abs(predictions[name] - values))) for name, values in val_targets.items()}
    stable_target_mae = {name: float(np.mean(np.abs(values))) for name, values in val_targets.items()}
    output_sources = select_output_sources(xgboost_target_mae, stable_target_mae)
    deployment_predictions = {
        name: predictions[name] if output_sources[name] == "xgboost" else np.zeros_like(values)
        for name, values in val_targets.items()
    }
    deployment_metrics = evaluate_forecast_metrics(val_x, val_targets, deployment_predictions)
    report = build_validation_report(
        xgboost_metrics,
        stable_reference_metrics,
        {"artifact_version": 1, "source": "candidate"},
        deployment_metrics,
    )
    report.update(source_metadata)
    report["output_sources"] = output_sources
    report["selected_candidate"] = selected_candidate["name"]
    report["candidate_search"] = [
        {
            "name": candidate["name"],
            "parameters": candidate["parameters"],
            "validation_score": (
                0.05 * candidate["metrics"]["by_horizon"]["30"]["endpoint_error_m"]
                + 0.2 * candidate["metrics"]["by_horizon"]["300"]["endpoint_error_m"]
                + 0.75 * candidate["metrics"]["by_horizon"]["900"]["endpoint_error_m"]
                + 0.1 * candidate["metrics"]["aggregate"]["trajectory_rmse_m"]
                + 0.1 * candidate["metrics"]["aggregate"]["heading_mae_deg"]
            ),
            "metrics": candidate["metrics"],
        }
        for candidate in candidates
    ]
    report["artifact_schema_version"] = 1
    report["artifact_manifest_sha256"] = None
    (output_dir / "validation_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    (output_dir / "feature_schema.json").write_text(json.dumps({
        "history_length": 30,
        "sample_interval_seconds": 10,
        "horizons_seconds": list(range(30, 901, 30)),
        "feature_names": list(schema.names) + ["forecast_horizon_seconds"],
        "mean": schema.mean.tolist() + [0.0],
        "scale": schema.scale.tolist() + [1.0],
    }, indent=2), encoding="utf-8")
    manifest_path = write_artifact_manifest(output_dir, "accepted")
    report["artifact_manifest_sha256"] = compute_sha256(manifest_path)
    (output_dir / "validation_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    return report


def evaluate_forecast_metrics(features: np.ndarray, targets: dict[str, np.ndarray], predictions: dict[str, np.ndarray]) -> dict:
    horizons = features[:, -1].astype(np.int32)
    position_error = np.hypot(
        predictions["east_displacement_m"] - targets["east_displacement_m"],
        predictions["north_displacement_m"] - targets["north_displacement_m"],
    )
    heading_error = np.abs(np.asarray([wrap_degrees(value) for value in predictions["heading_delta_deg"] - targets["heading_delta_deg"]]))
    by_horizon = {}
    for horizon in (30, 300, 900):
        mask = horizons == horizon
        by_horizon[str(horizon)] = {
            "endpoint_error_m": float(np.mean(position_error[mask])) if np.any(mask) else float("inf"),
            "depth_mae_m": float(np.mean(np.abs(predictions["depth_delta_m"][mask] - targets["depth_delta_m"][mask]))) if np.any(mask) else float("inf"),
        }
    return {
        "by_horizon": by_horizon,
        "aggregate": {
            "trajectory_rmse_m": float(np.sqrt(np.mean(position_error ** 2))),
            "heading_mae_deg": float(np.mean(heading_error)),
        },
    }


def build_dataset(frame_table: pd.DataFrame, window_size: int, horizon: int, train_ratio: float) -> DatasetBundle:
    split_index = int(len(frame_table) * train_ratio)
    train_frame = frame_table.iloc[:split_index].reset_index(drop=True)
    val_frame = frame_table.iloc[split_index:].reset_index(drop=True)

    train_feature_frame = train_frame[FEATURE_COLUMNS]
    scaler_mean = train_feature_frame.mean().to_numpy(dtype=np.float32)
    scaler_scale = train_feature_frame.std(ddof=0).replace(0, 1).to_numpy(dtype=np.float32)

    full_feature_values = frame_table[FEATURE_COLUMNS].to_numpy(dtype=np.float32)
    normalized_features = (full_feature_values - scaler_mean) / scaler_scale
    target_values = frame_table[TARGET_COLUMNS].to_numpy(dtype=np.float32)

    train_features, train_targets = build_windows(normalized_features[:split_index], target_values[:split_index], window_size, horizon)
    val_source_start = max(0, split_index - window_size)
    val_features, val_targets = build_windows(normalized_features[val_source_start:], target_values[val_source_start:], window_size, horizon)

    validation_frame_indices = []
    for start in range(len(val_features)):
        absolute_future_end = val_source_start + start + window_size + horizon - 1
        validation_frame_indices.append(absolute_future_end)

    metadata = {
        "train_rows": int(len(train_frame)),
        "validation_rows": int(len(val_frame)),
        "train_windows": int(len(train_features)),
        "validation_windows": int(len(val_features)),
        "feature_columns": FEATURE_COLUMNS,
        "target_columns": TARGET_COLUMNS,
    }
    return DatasetBundle(
        train_features=train_features,
        train_targets=train_targets,
        val_features=val_features,
        val_targets=val_targets,
        val_target_frames=np.array(validation_frame_indices, dtype=np.int32),
        scaler_mean=scaler_mean,
        scaler_scale=scaler_scale,
        metadata=metadata,
    )


def build_windows(features: np.ndarray, targets: np.ndarray, window_size: int, horizon: int) -> tuple[np.ndarray, np.ndarray]:
    windowed_features = []
    windowed_targets = []
    last_start = len(features) - window_size - horizon + 1
    for start in range(max(0, last_start)):
        windowed_features.append(features[start:start + window_size])
        windowed_targets.append(targets[start + window_size:start + window_size + horizon])
    return np.asarray(windowed_features, dtype=np.float32), np.asarray(windowed_targets, dtype=np.float32)


def run_physics_baseline(dataset: DatasetBundle, output_dir: Path) -> dict:
    output_dir.mkdir(parents=True, exist_ok=True)
    predictions = []
    for feature_window in dataset.val_features:
        denormalized = feature_window * dataset.scaler_scale + dataset.scaler_mean
        last_step = denormalized[-1]
        current_speed = float(last_step[FEATURE_COLUMNS.index("current_speed")])
        heading = math.radians(float(last_step[FEATURE_COLUMNS.index("heading_deg")]))
        pitch = math.radians(float(last_step[FEATURE_COLUMNS.index("pitch_deg")]))
        depth = float(last_step[FEATURE_COLUMNS.index("depth_m")])
        x = float(last_step[FEATURE_COLUMNS.index("x_m")])
        y = float(last_step[FEATURE_COLUMNS.index("y_m")])
        z = float(last_step[FEATURE_COLUMNS.index("z_m")])
        step_predictions = []
        for _ in range(dataset.val_targets.shape[1]):
            dx = math.sin(heading) * math.cos(pitch) * current_speed
            dz = math.cos(heading) * math.cos(pitch) * current_speed
            dy = -math.sin(pitch) * current_speed
            x += dx
            y += dy
            z += dz
            depth = -y
            step_predictions.append([x, y, z, math.degrees(heading), depth, math.degrees(pitch), 0.0])
        predictions.append(step_predictions)

    predictions_array = np.asarray(predictions, dtype=np.float32)
    metrics = evaluate(predictions_array, dataset.val_targets)
    np.savez_compressed(output_dir / "validation_predictions.npz", predictions=predictions_array, targets=dataset.val_targets)
    write_metadata(output_dir, "Physics", metrics, dataset)
    return {"status": "trained", **metrics}


def run_xgboost(dataset: DatasetBundle, output_dir: Path) -> dict:
    if xgb is None or MultiOutputRegressor is None:
        return {"status": "missing_dependency", "reason": "Install xgboost and scikit-learn to train this model."}

    output_dir.mkdir(parents=True, exist_ok=True)
    train_x = dataset.train_features.reshape(len(dataset.train_features), -1)
    train_y = dataset.train_targets.reshape(len(dataset.train_targets), -1)
    val_x = dataset.val_features.reshape(len(dataset.val_features), -1)

    model = MultiOutputRegressor(
        xgb.XGBRegressor(
            n_estimators=180,
            max_depth=6,
            learning_rate=0.05,
            subsample=0.85,
            colsample_bytree=0.85,
            objective="reg:squarederror",
            tree_method="hist",
            random_state=42,
        )
    )
    started_at = time.perf_counter()
    model.fit(train_x, train_y)
    elapsed = (time.perf_counter() - started_at) * 1000.0
    predictions = model.predict(val_x).reshape(dataset.val_targets.shape)
    metrics = evaluate(predictions, dataset.val_targets)
    metrics["train_time_ms"] = elapsed
    with open(output_dir / "xgboost_model.pkl", "wb") as file:
        pickle.dump(model, file)
    np.savez_compressed(output_dir / "validation_predictions.npz", predictions=predictions, targets=dataset.val_targets)
    write_metadata(output_dir, "XGBoost", metrics, dataset)
    return {"status": "trained", **metrics}


def run_torch_model(dataset: DatasetBundle, output_dir: Path, model_type: str, epochs: int, batch_size: int) -> dict:
    if torch is None or nn is None or DataLoader is None or TensorDataset is None:
        return {"status": "missing_dependency", "reason": "Install torch to train this model."}

    output_dir.mkdir(parents=True, exist_ok=True)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    feature_dim = dataset.train_features.shape[-1]
    target_dim = dataset.train_targets.shape[-1]
    horizon = dataset.train_targets.shape[1]
    model = (
        LstmForecaster(feature_dim, hidden_dim=96, target_dim=target_dim, horizon=horizon)
        if model_type == "lstm"
        else CnnLstmForecaster(feature_dim, hidden_dim=96, target_dim=target_dim, horizon=horizon)
    ).to(device)

    train_loader = DataLoader(
        TensorDataset(torch.from_numpy(dataset.train_features), torch.from_numpy(dataset.train_targets)),
        batch_size=batch_size,
        shuffle=True,
    )
    optimizer = torch.optim.Adam(model.parameters(), lr=1e-3)
    criterion = nn.MSELoss()

    started_at = time.perf_counter()
    for _ in range(epochs):
        model.train()
        for batch_x, batch_y in train_loader:
            batch_x = batch_x.to(device)
            batch_y = batch_y.to(device)
            optimizer.zero_grad(set_to_none=True)
            loss = criterion(model(batch_x), batch_y)
            loss.backward()
            optimizer.step()

    model.eval()
    with torch.no_grad():
        val_tensor = torch.from_numpy(dataset.val_features).to(device)
        predictions = model(val_tensor).cpu().numpy()

    elapsed = (time.perf_counter() - started_at) * 1000.0
    metrics = evaluate(predictions, dataset.val_targets)
    metrics["train_time_ms"] = elapsed
    torch.save(model.state_dict(), output_dir / "model_state.pt")
    np.savez_compressed(output_dir / "validation_predictions.npz", predictions=predictions, targets=dataset.val_targets)
    write_metadata(output_dir, "LSTM" if model_type == "lstm" else "CNN-LSTM", metrics, dataset)
    return {"status": "trained", **metrics}


def evaluate(predictions: np.ndarray, targets: np.ndarray) -> dict:
    errors = np.linalg.norm(predictions[..., :3] - targets[..., :3], axis=-1)
    rmse = float(np.sqrt(np.mean(errors ** 2)))
    mae = float(np.mean(errors))
    current_error = float(np.mean(errors[:, 0])) if errors.size else 0.0
    max_error = float(np.max(errors)) if errors.size else 0.0
    confidence = float(1.0 / (1.0 + rmse / 6.0))
    return {
        "rmse": rmse,
        "mae": mae,
        "current_error": current_error,
        "max_error": max_error,
        "confidence": confidence,
    }


def write_metadata(output_dir: Path, model_name: str, metrics: dict, dataset: DatasetBundle) -> None:
    metadata = {
        "model_name": model_name,
        "metrics": metrics,
        "dataset": dataset.metadata,
        "generated_at": time.strftime("%Y-%m-%d %H:%M:%S"),
    }
    (output_dir / "metadata.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")


def write_artifact_manifest(root: Path, validation_status: str, training_run_id: str | None = None) -> Path:
    root = Path(root)
    files = []
    for file_path in sorted(root.rglob("*")):
        if not file_path.is_file():
            continue
        relative_path = file_path.relative_to(root).as_posix()
        if relative_path in {"manifest.json", "validation_report.json"}:
            continue
        files.append({
            "path": relative_path,
            "sha256": compute_sha256(file_path),
            "size_bytes": file_path.stat().st_size,
        })

    payload = {
        "artifact_schema_version": 1,
        "validation_status": validation_status,
        "training_run_id": training_run_id or datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ"),
        "files": files,
    }
    manifest_path = root / "manifest.json"
    manifest_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return manifest_path


def _create_artifact_staging_dir(output_dir: Path) -> Path:
    output_dir = Path(output_dir)
    staging_dir = output_dir.with_name(
        output_dir.name + ".staging-" + datetime.utcnow().strftime("%Y%m%d%H%M%S") + "-" + next(temp_suffix())
    )
    staging_dir.parent.mkdir(parents=True, exist_ok=True)
    _remove_tree_if_exists(staging_dir)
    staging_dir.mkdir(parents=True, exist_ok=False)
    return staging_dir


def _remove_tree_if_exists(root: Path) -> None:
    root = Path(root)
    if root.exists():
        shutil.rmtree(root)


def validate_artifact_tree(root: Path) -> dict:
    root = Path(root)
    manifest_path = root / "manifest.json"
    if not manifest_path.exists():
        raise ArtifactValidationError("manifest.json is missing")

    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    if payload.get("artifact_schema_version") != 1:
        raise ArtifactValidationError("unsupported artifact schema version")
    if payload.get("validation_status") != "accepted":
        raise ArtifactValidationError("artifact validation_status must be accepted")

    files = payload.get("files") or []
    if not files:
        raise ArtifactValidationError("artifact file set is missing")

    for item in files:
        relative = item.get("path", "")
        if not relative or relative == "manifest.json" or relative.startswith("XGBoost/") or Path(relative).is_absolute() or ".." in Path(relative).parts:
            raise ArtifactValidationError(f"invalid artifact path: {relative}")

        file_path = root / Path(relative)
        if not file_path.exists():
            raise ArtifactValidationError(f"missing artifact file: {relative}")
        if int(item.get("size_bytes", -1)) != file_path.stat().st_size:
            raise ArtifactValidationError(f"size mismatch for artifact file: {relative}")
        if compute_sha256(file_path) != item.get("sha256"):
            raise ArtifactValidationError(f"hash mismatch for artifact file: {relative}")

    return payload


def publish_artifact_tree_with_rollback(staging_root: Path, models_root: Path, artifact_name: str = "XGBoost") -> Path:
    staging_root = Path(staging_root)
    models_root = Path(models_root)
    final_root = models_root / artifact_name
    backup_root = None
    if not staging_root.exists():
        raise ArtifactValidationError("staging artifact tree is missing")

    payload = validate_artifact_tree(staging_root)
    models_root.mkdir(parents=True, exist_ok=True)

    try:
        if final_root.exists():
            backup_root = final_root.with_name(final_root.name + ".backup-" + datetime.utcnow().strftime("%Y%m%d%H%M%S") + "-" + next(temp_suffix()))
            shutil.move(str(final_root), str(backup_root))

        shutil.move(str(staging_root), str(final_root))
        validate_artifact_tree(final_root)
        if backup_root is not None and backup_root.exists():
            shutil.rmtree(backup_root)
    except Exception as exc:
        if final_root.exists():
            shutil.rmtree(final_root, ignore_errors=True)
        if backup_root is not None and backup_root.exists() and not final_root.exists():
            shutil.move(str(backup_root), str(final_root))
        raise ArtifactValidationError(str(exc)) from exc

    return final_root


def compute_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as file:
        for chunk in iter(lambda: file.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def temp_suffix():
    while True:
        yield time.strftime("%H%M%S") + "-" + str(int(time.time() * 1_000_000))


def parse_elapsed_seconds(raw_time: str) -> float:
    matches = TIME_PATTERN.findall(str(raw_time))
    if len(matches) == 6:
        year, month, day, hours, minutes, seconds = [int(value) for value in matches]
        current = datetime(year, month, day, hours, minutes, seconds)
        year_start = datetime(year, 1, 1)
        return float((current - year_start).total_seconds())
    if len(matches) != 4:
        raise ValueError(f"Could not parse telemetry time {raw_time!r}")
    days, hours, minutes, seconds = [int(value) for value in matches]
    return float(((days * 24 + hours) * 60 + minutes) * 60 + seconds)


if __name__ == "__main__":
    main()
