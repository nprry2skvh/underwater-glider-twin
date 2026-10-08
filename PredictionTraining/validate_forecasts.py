"""Causal replay, frozen forecast export and honest delayed-score reports."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import uuid
from pathlib import Path
from typing import Iterable

import numpy as np
import pandas as pd

import forecast_data
import train_models

TARGETS = (
    'east_displacement_m', 'north_displacement_m', 'depth_delta_m',
    'heading_delta_deg', 'pitch_delta_deg', 'roll_delta_deg',
)
SCORING_CONFIG = {'time_tolerance_seconds': .5, 'maximum_interpolation_gap_seconds': 20.,
                  'wait_deadline_seconds': 60., 'metric_version': 'physical-local-end-v1'}


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def validate_manifest_source(manifest: dict, source_path: Path) -> None:
    if manifest.get('source_sha256') != file_sha256(source_path):
        raise ValueError('source SHA-256 does not match frozen split manifest')


def fit_training_statistics(rows: pd.DataFrame, value_column: str) -> dict:
    if 'partition' not in rows or rows.empty or not (rows.partition == 'train').any():
        raise ValueError('fitting input must contain at least one train partition row')
    values = pd.to_numeric(rows.loc[rows.partition == 'train', value_column], errors='coerce').dropna().to_numpy(dtype=float)
    if not len(values):
        raise ValueError('train partition has no finite values')
    return {'count': int(len(values)), 'mean': float(values.mean()),
            'standard_deviation': float(values.std())}


def prepare_forecast_input(frame: pd.DataFrame, issued_seconds: float) -> pd.DataFrame:
    if frame.attrs.get('causally_cleaned_at') == issued_seconds:
        return frame
    sampled = frame.elapsed_seconds.to_numpy(dtype=float)
    visible = np.isfinite(sampled) & (sampled <= issued_seconds)
    if 'received_seconds' in frame:
        received = frame.received_seconds.to_numpy(dtype=float)
        visible &= np.isfinite(received) & (received >= sampled) & (received <= issued_seconds)
    selected = frame.loc[visible].sort_values('elapsed_seconds', kind='stable')
    result = train_models.clean_navigation_rows(selected)
    result.attrs['causally_cleaned_at'] = issued_seconds
    return result


def _require_interval(seconds: float) -> None:
    if seconds != 10:
        raise ValueError('unsupported output interval; this version supports 10 seconds only')


def validate_artifact(root: Path) -> tuple[dict, dict]:
    root = Path(root).resolve()
    try:
        manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
        if (manifest.get('artifact_schema_version') != 1 or manifest.get('validation_status') != 'accepted'
                or not manifest.get('training_run_id') or not manifest.get('files')):
            raise ValueError('artifact manifest must declare accepted version and files')
        declared = set()
        for item in manifest['files']:
            path = (root / item['path']).resolve()
            relative = path.relative_to(root).as_posix()
            if relative in declared or file_sha256(path) != item['sha256'] or path.stat().st_size != item['size_bytes']:
                raise ValueError('artifact SHA-256/size mismatch or duplicate manifest file')
            declared.add(relative)
        schema = json.loads((root / 'feature_schema.json').read_text(encoding='utf-8'))
        sources = json.loads((root / 'validation_report.json').read_text(encoding='utf-8')).get('output_sources')
        if not sources or any(sources.get(target) not in ('xgboost', 'stable') for target in TARGETS):
            raise ValueError('artifact output sources are missing or unsupported')
        if 'feature_schema.json' not in declared or any(
                f'models/{target}.json' not in declared for target in TARGETS if sources[target] == 'xgboost'):
            raise ValueError('artifact manifest omits an active model or feature schema')
        if schema.get('history_length') != 30 or schema.get('sample_interval_seconds') != 10:
            raise ValueError('artifact history contract must be 30 past-held 10-second samples')
        names = schema.get('feature_names') or []
        if not names or len(schema.get('mean', [])) != len(names) or len(schema.get('scale', [])) != len(names):
            raise ValueError('artifact feature schema is incomplete')
        return schema, sources
    except (OSError, KeyError, TypeError, json.JSONDecodeError) as exception:
        raise ValueError('artifact manifest/schema is invalid: ' + str(exception)) from exception


def constant_velocity_forecast(frame: pd.DataFrame, issued_seconds: float, horizon_seconds: int,
                               sample_interval_seconds: int = 10) -> list[dict]:
    _require_horizon(horizon_seconds)
    _require_interval(sample_interval_seconds)
    frame = prepare_forecast_input(frame, issued_seconds)
    history = forecast_data.causal_history(frame, issued_seconds, window_size=30,
                                           sample_interval_seconds=sample_interval_seconds)
    required = ('longitude_deg', 'latitude_deg', 'depth_m', 'heading_deg', 'pitch_deg', 'roll_deg')
    if len(history) < 2 or any(name not in history for name in required):
        raise ValueError('constant velocity needs at least two causal position samples')
    previous, origin = history.iloc[-2], history.iloc[-1]
    previous_sampled = float(previous.get('_sampled_at_seconds', previous.elapsed_seconds))
    origin_sampled = float(origin.get('_sampled_at_seconds', origin.elapsed_seconds))
    dt = origin_sampled - previous_sampled
    if dt <= 0:
        raise ValueError('causal history times must increase')
    east, north = train_models.local_east_north_meters(
        float(previous.longitude_deg), float(previous.latitude_deg),
        float(origin.longitude_deg), float(origin.latitude_deg))
    east_rate, north_rate = east / dt, north / dt
    depth_rate = (float(origin.depth_m) - float(previous.depth_m)) / dt
    heading_rate = train_models.wrap_degrees(float(origin.heading_deg) - float(previous.heading_deg)) / dt
    pitch_rate = (float(origin.pitch_deg) - float(previous.pitch_deg)) / dt
    roll_rate = (float(origin.roll_deg) - float(previous.roll_deg)) / dt
    return [_forecast_point(
        float(origin.elapsed_seconds) + seconds, east_rate * seconds, north_rate * seconds,
        float(origin.depth_m) + depth_rate * seconds,
        float(origin.heading_deg) + heading_rate * seconds,
        float(origin.pitch_deg) + pitch_rate * seconds,
        float(origin.roll_deg) + roll_rate * seconds)
        for seconds in range(sample_interval_seconds, horizon_seconds + 1, sample_interval_seconds)]


def evaluate_flat_tree(model: dict, features: Iterable[float]) -> float:
    feature_values = list(features)
    total = np.float32(model.get('base_score', 0.0))
    for tree in model.get('trees', []):
        nodes = tree.get('nodes') or []
        index = 0
        for _ in range(len(nodes)):
            node = nodes[index]
            feature_index = int(node.get('feature_index', -1))
            if feature_index < 0:
                total = np.float32(total + np.float32(node.get('leaf_value', 0.0)))
                break
            value = np.float32(feature_values[feature_index])
            if not math.isfinite(value):
                index = int(node['missing_index'])
            elif value < np.float32(node['threshold']):
                index = int(node['yes_index'])
            else:
                index = int(node['no_index'])
        else:
            raise ValueError('flat tree did not reach a leaf')
    return float(total)


def deployed_forecast(frame: pd.DataFrame, issued_seconds: float, horizon_seconds: int,
                      artifact_root: Path, sample_interval_seconds: int = 10) -> list[dict]:
    _require_horizon(horizon_seconds)
    _require_interval(sample_interval_seconds)
    frame = prepare_forecast_input(frame, issued_seconds)
    history = forecast_data.causal_history(frame, issued_seconds, window_size=30,
                                           sample_interval_seconds=sample_interval_seconds)
    schema, sources = validate_artifact(artifact_root)
    if len(history) < int(schema['history_length']):
        raise ValueError('deployed model requires 30 causal history samples')
    models = {}
    for target in TARGETS:
        if sources.get(target) == 'xgboost':
            models[target] = json.loads((Path(artifact_root) / 'models' / f'{target}.json').read_text(encoding='utf-8'))
    anchors = []
    for seconds in range(30, horizon_seconds + 1, 30):
        features = runtime_features(frame, issued_seconds, schema['feature_names'], seconds)
        anchors.append({target: (evaluate_flat_tree(models[target], features)
                                 if sources.get(target) == 'xgboost' else 0.0)
                        for target in TARGETS})
    origin = history.iloc[-1]
    points = []
    for seconds in range(sample_interval_seconds, horizon_seconds + 1, sample_interval_seconds):
        anchor = _interpolate_anchor(anchors, seconds)
        points.append(_forecast_point(
            float(origin.elapsed_seconds) + seconds, anchor['east_displacement_m'],
            anchor['north_displacement_m'], float(origin.depth_m) + anchor['depth_delta_m'],
            float(origin.heading_deg) + anchor['heading_delta_deg'],
            float(origin.pitch_deg) + anchor['pitch_delta_deg'],
            float(origin.roll_deg) + anchor['roll_delta_deg']))
    return points


def runtime_features(frame: pd.DataFrame, issued_seconds: float, names: list[str], horizon: float) -> list[float]:
    frame = prepare_forecast_input(frame, issued_seconds)
    history = forecast_data.causal_history(frame, issued_seconds, window_size=30)
    if len(history) < 2:
        raise ValueError('insufficient causal history')
    current, first = history.iloc[-1], history.iloc[0]
    velocities = []
    previous = None
    for _, row in history.iterrows():
        if previous is None:
            velocities.append((0., 0., 0.))
        else:
            dt = max(.001, float(row.elapsed_seconds - previous.elapsed_seconds))
            east, north = train_models.local_east_north_meters(
                float(previous.longitude_deg), float(previous.latitude_deg), float(row.longitude_deg), float(row.latitude_deg))
            velocities.append((east / dt, -(float(row.depth_m) - float(previous.depth_m)) / dt, north / dt))
        previous = row
    velocity = np.asarray(velocities, dtype=np.float32)
    speeds = np.linalg.norm(velocity, axis=1)
    depth = history.depth_m.to_numpy(dtype=np.float32)
    pitch = history.pitch_deg.to_numpy(dtype=np.float32)
    duration = max(.001, float(current.elapsed_seconds - first.elapsed_seconds))
    values = {name: float(current.get(name, float('nan'))) for name in (
        'depth_m', 'heading_deg', 'pitch_deg', 'roll_deg', 'piston_mm', 'turn_angle_deg')}
    values.update({
        'heading_sin': math.sin(math.radians(float(current.heading_deg))),
        'heading_cos': math.cos(math.radians(float(current.heading_deg))),
        'velocity_x': float(velocity[-1, 0]), 'velocity_y': float(velocity[-1, 1]), 'velocity_z': float(velocity[-1, 2]),
        'current_speed': float(speeds[-1]),  # Legacy schema name: ground speed, never ocean current.
        'target_heading_residual_deg': train_models.wrap_degrees(float(current.get('target_heading_deg', float('nan'))) - float(current.heading_deg)),
        'target_depth_residual_m': float(current.get('target_depth_m', float('nan'))) - float(current.depth_m),
        'depth_mean': float(depth.mean()), 'depth_std': float(depth.std()),
        'pitch_mean': float(pitch.mean()), 'pitch_std': float(pitch.std()),
        'velocity_mean': float(speeds.mean()), 'velocity_std': float(speeds.std()),
        'depth_trend_mps': (float(current.depth_m) - float(first.depth_m)) / duration,
        'heading_trend_degps': train_models.wrap_degrees(float(current.heading_deg) - float(first.heading_deg)) / duration,
        'descending_flag': float(current.depth_m >= first.depth_m), 'forecast_horizon_seconds': float(horizon)})
    result = [float(np.float32(values[name])) for name in names]
    if not all(math.isfinite(value) for value in result):
        raise ValueError('non-finite causal model features')
    return result


def select_replay_segments(segments: list[pd.DataFrame], manifest: dict, partition: str) -> list[tuple[int, pd.DataFrame]]:
    if partition not in {'train', 'development', 'final_test', 'exploratory'}:
        raise ValueError('invalid replay partition')
    selected = []
    seen = set()
    for row in manifest['segments']:
        index = int(row['segment_index'])
        if index in seen or index < 0 or index >= len(segments):
            raise ValueError('invalid or duplicate manifest segment boundary')
        seen.add(index)
        segment = segments[index]
        if (len(segment) != row['row_count'] or float(segment.elapsed_seconds.iloc[0]) != row['start_seconds']
                or float(segment.elapsed_seconds.iloc[-1]) != row['end_seconds']):
            raise ValueError('manifest segment boundary mismatch')
        row_partition = row.get('partition', 'exploratory')
        if row_partition == partition:
            selected.append((index, segment))
    if not selected:
        raise ValueError('requested partition has no eligible segments')
    return selected


def freeze_forecast(frame: pd.DataFrame, issued: float, horizon: int, method: str,
                    run_id: str, branch_id: str, artifact_root: Path | None = None) -> dict:
    frame = prepare_forecast_input(frame, issued)
    history = forecast_data.causal_history(frame, issued)
    raw_columns = [column for column in history if not column.startswith('velocity_')
                   and column not in {'current_speed', 'x_m', 'y_m', 'z_m'}]
    input_json = history[raw_columns].to_json(orient='records', double_precision=15)
    input_digest = hashlib.sha256(input_json.encode()).hexdigest()
    if method == 'constant_velocity':
        points = constant_velocity_forecast(frame, issued, horizon)
        model_hash = 'constant_velocity_v1'
    elif method == 'current_deployed_xgboost_with_hold_fallback' and artifact_root is not None:
        points = deployed_forecast(frame, issued, horizon, artifact_root)
        model_hash = hashlib.sha256(''.join(
            path.relative_to(artifact_root).as_posix() + ':' + file_sha256(path)
            for path in sorted(Path(artifact_root).rglob('*.json'))).encode()).hexdigest()
    else:
        raise ValueError('unsupported or unavailable forecast method')
    output_digest = hashlib.sha256(json.dumps(points, sort_keys=True, allow_nan=False).encode()).hexdigest()
    identity = f'{run_id}:{branch_id}:{issued}:{horizon}:{method}:{model_hash}:{input_digest}'
    origin = history.iloc[-1]
    return {'forecast_id': hashlib.sha256(identity.encode()).hexdigest()[:24], 'run_id': run_id,
            'branch_id': branch_id, 'method': method, 'model_hash': model_hash,
            'code_hash': file_sha256(Path(__file__)), 'input_digest': input_digest, 'output_digest': output_digest,
            'input_mode': history.attrs['replay_mode'], 'profile_sequence': int(origin.get('profile_sequence', 0)),
            'current_version': 'not_provided', 'origin_elapsed_seconds': issued,
            'origin': {'longitude_deg': float(origin.longitude_deg), 'latitude_deg': float(origin.latitude_deg)},
            'horizon_seconds': horizon, 'target_elapsed_seconds': [p['target_elapsed_seconds'] for p in points],
            'scoring_config': dict(SCORING_CONFIG), 'points': points}


def score_frozen_forecast(record: dict, frame: pd.DataFrame, received_seconds: float,
                          truth_grade: str = 'navigation_record_unverified') -> list[dict]:
    train_models.validate_simulation_branch_metadata(frame)
    config = record['scoring_config']
    sampled = frame.elapsed_seconds.to_numpy(dtype=float)
    received = frame.received_seconds.to_numpy(dtype=float) if 'received_seconds' in frame else sampled
    result = []
    for point in record['points']:
        target = point['target_elapsed_seconds']
        deadline = target + config['wait_deadline_seconds']
        radius = max(config['maximum_interpolation_gap_seconds'], config['time_tolerance_seconds'])
        visible = (np.isfinite(received) & (received >= sampled) & (received <= received_seconds)
                   & (received < deadline) & (np.abs(sampled - target) <= radius))
        if 'branch_id' in frame:
            simulation_rows = (frame.truth_grade.astype(str).str.contains('simulation', case=False).to_numpy()
                               if 'truth_grade' in frame else
                               np.full(len(frame), 'simulation' in truth_grade.casefold(), dtype=bool))
            visible &= ~simulation_rows | (frame.branch_id.to_numpy() == record['branch_id'])
        events = frame.loc[visible].copy()
        events['_received_at'] = received[visible]
        events = events.sort_values('_received_at', kind='stable').drop_duplicates('elapsed_seconds', keep='first')
        actual, availability = None, None
        # Finalize at the first arrival event that permits alignment, not the
        # best observation available by the end of the entire waiting period.
        for arrival in events['_received_at'].drop_duplicates():
            event_time = max(target, float(arrival))
            if event_time > received_seconds or event_time >= deadline:
                continue
            candidates = events[events['_received_at'] <= arrival].sort_values('elapsed_seconds')
            actual = _align_truth(candidates, target, config)
            if actual is not None:
                availability = event_time
                break
        score = {'score_key': f"{record['run_id']}:{record['forecast_id']}:{target}:{config['metric_version']}",
                 'forecast_id': record['forecast_id'], 'target_elapsed_seconds': target,
                 'truth_grade': truth_grade, 'status': 'missing' if received_seconds >= deadline else 'awaiting',
                 'horizontal_error_m': None, 'position_error_m': None, 'available_at_seconds': availability}
        if actual is not None:
            valid_position = all(math.isfinite(float(actual[name])) for name in ('longitude_deg', 'latitude_deg', 'depth_m'))
            score['truth_grade'] = actual.get('truth_grade', truth_grade)
            if not valid_position or not train_models.parse_position_reference(actual.get('has_position_reference', True)):
                score.update(status='no_position_reference')
                result.append(score)
                continue
            origin = record['origin']
            east = (float(actual.longitude_deg) - origin['longitude_deg']) * 111320. * math.cos(math.radians(origin['latitude_deg']))
            north = (float(actual.latitude_deg) - origin['latitude_deg']) * 111320.
            horizontal = math.hypot(point['east_m'] - east, point['north_m'] - north)
            depth_error = abs(point['depth_m'] - float(actual.depth_m))
            score.update(status='scored', horizontal_error_m=horizontal,
                         position_error_m=math.hypot(horizontal, depth_error), depth_error_m=depth_error,
                         heading_error_deg=abs(train_models.wrap_degrees(point['heading_deg'] - float(actual.heading_deg))),
                         pitch_error_deg=abs(point['pitch_deg'] - float(actual.pitch_deg)),
                         roll_error_deg=abs(point['roll_deg'] - float(actual.roll_deg)))
        result.append(score)
    return result


def _align_truth(candidates: pd.DataFrame, target: float, config: dict):
    exact = candidates[(candidates.elapsed_seconds - target).abs() <= config['time_tolerance_seconds']]
    if not exact.empty:
        return exact.iloc[(exact.elapsed_seconds - target).abs().argmin()]
    before, after = candidates[candidates.elapsed_seconds < target], candidates[candidates.elapsed_seconds > target]
    if before.empty or after.empty:
        return None
    lower, upper = before.iloc[-1], after.iloc[0]
    gap = float(upper.elapsed_seconds - lower.elapsed_seconds)
    if gap <= 0 or gap > config['maximum_interpolation_gap_seconds']:
        return None
    if 'branch_id' in candidates and lower.branch_id != upper.branch_id:
        return None
    if 'truth_grade' in candidates and lower.truth_grade != upper.truth_grade:
        return None
    for endpoint in (lower, upper):
        if (not train_models.parse_position_reference(endpoint.get('has_position_reference', True))
                or not all(math.isfinite(float(endpoint[name])) for name in ('longitude_deg', 'latitude_deg', 'depth_m'))):
            return None
    alpha = (target - float(lower.elapsed_seconds)) / gap
    actual = lower.copy()
    for column in ('longitude_deg', 'latitude_deg', 'depth_m', 'pitch_deg', 'roll_deg'):
        actual[column] = float(lower[column]) + (float(upper[column]) - float(lower[column])) * alpha
    actual['heading_deg'] = float(lower.heading_deg) + train_models.wrap_degrees(float(upper.heading_deg) - float(lower.heading_deg)) * alpha
    return actual


def build_quality_report(*, source_path: str, source_sha256: str, source_kind: str,
                         row_count: int, segment_count: int, has_received_time: bool,
                         has_current_provenance: bool) -> dict:
    synthetic = source_kind == 'synthetic'
    truth_grade = 'synthetic_simulation' if synthetic else 'navigation_record_unverified'
    split_status = 'fixed_three_way_manifest' if segment_count >= 3 else 'insufficient_independent_segments'
    limitations = []
    if not has_received_time:
        limitations.append('receive timestamps absent; replay is ordered by sample time only')
    if not has_current_provenance:
        limitations.append('ocean-current product issue/version/valid-time metadata absent')
    if segment_count < 3:
        limitations.append('fewer than three independent continuous segments; no final-test claim')
    if not synthetic:
        limitations.append('position provenance is unverified; errors mean consistency with navigation record only')
    return {
        'schema_version': 1, 'source_path': source_path, 'source_sha256': source_sha256,
        'source_kind': source_kind, 'row_count': int(row_count),
        'continuous_segment_count': int(segment_count), 'verified_mission_count': None,
        'replay_mode': 'received_time_replay' if has_received_time else 'sample_time_replay',
        'truth_grade': truth_grade, 'current_provenance': 'present' if has_current_provenance else 'absent',
        'split_status': split_status,
        'eligible_for_independent_operational_acceptance': False,
        'limitations': limitations,
    }


def build_comparison_report(attempts: list[dict], *, truth_grade: str,
                            mission_count: int | None, segment_count: int,
                            split_status: str, score_rows: list[dict] | None = None) -> dict:
    methods = {}
    method_names = sorted({item['method'] for item in attempts})
    for name in method_names:
        rows = [item for item in attempts if item['method'] == name]
        scored = [item for item in rows if item.get('status') == 'scored']
        failures = [item for item in rows if item.get('status') == 'failed']
        unscored = [item for item in rows if item.get('status') not in {'scored', 'failed'}]
        horizontal = [float(item['horizontal_error_m']) for item in scored]
        position = [float(item['position_error_m']) for item in scored]
        count = len(rows)
        methods[name] = {
            'attempted_count': count, 'scored_count': len(scored),
            'failure_count': len(failures), 'unscored_count': len(unscored),
            'success_rate': len(scored) / count if count else 0.0,
            'prediction_success_rate': (count - len(failures)) / count if count else 0.0,
            'scoring_success_rate': len(scored) / count if count else 0.0,
            'failure_rate': len(failures) / count if count else 0.0,
            'unscored_fraction': len(unscored) / count if count else 0.0,
            'horizontal_mae_m': float(np.mean(horizontal)) if horizontal else None,
            'position_rmse_m': float(np.sqrt(np.mean(np.square(position)))) if position else None,
            'prediction_region_90_coverage': None,
            'probability_note': 'not_applicable_model_does_not_emit_independently_calibrated_region',
        }
        point_rows = [row for row in (score_rows or []) if row['method'] == name]
        valid_points = [row for row in point_rows if row['status'] == 'scored']
        methods[name].update(eligible_point_count=len(point_rows), scored_point_count=len(valid_points),
                             missing_point_count=sum(row['status'] == 'missing' for row in point_rows),
                             awaiting_point_count=sum(row['status'] == 'awaiting' for row in point_rows))
        for metric, field in [('depth_mae_m', 'depth_error_m'), ('heading_mae_deg', 'heading_error_deg'),
                              ('pitch_mae_deg', 'pitch_error_deg'), ('roll_mae_deg', 'roll_error_deg')]:
            values = [row[field] for row in valid_points if field in row and row[field] is not None]
            methods[name][metric] = float(np.mean(values)) if values else None
        methods[name]['by_horizon'] = {}
        for horizon in forecast_data.SUPPORTED_HORIZONS:
            group = [row for row in valid_points if row.get('horizon_seconds') == horizon]
            endpoint = [row for row in group if row.get('is_endpoint')]
            methods[name]['by_horizon'][str(horizon)] = {
                'scored_point_count': len(group),
                'horizontal_mae_m': float(np.mean([row['horizontal_error_m'] for row in group])) if group else None,
                'endpoint_horizontal_mae_m': float(np.mean([row['horizontal_error_m'] for row in endpoint])) if endpoint else None}
    # Preserve the requested four method names even when inputs are unavailable.
    methods.setdefault('event_driven_physics_with_current', {'status': 'unavailable',
        'reason': 'requires paired SimulationStateSnapshot/profile or equivalent observed state'})
    methods.setdefault('physics_plus_residual_candidate', {'status': 'not_trained',
        'reason': 'requires paired frozen physical baseline residuals and three-way data manifest'})
    return {
        'schema_version': 1, 'truth_grade': truth_grade,
        'verified_mission_count': mission_count, 'continuous_segment_count': segment_count,
        'split_status': split_status,
        'independent_final_test': False,
        'model_training_overlap': 'unknown_for_previously_deployed_artifact',
        'interpretation': ('synthetic_functional_validation_only' if truth_grade == 'synthetic_simulation'
                           else 'consistency_with_navigation_record_only'),
        'methods': methods,
    }


def run_validation(csv_path: Path, artifact_root: Path, output_root: Path,
                   source_kind: str, max_origins: int = 8, partition: str = 'auto',
                   run_id: str | None = None, manifest_path: Path | None = None) -> dict:
    csv_path, artifact_root, output_root = Path(csv_path), Path(artifact_root), Path(output_root)
    output_root.mkdir(parents=True, exist_ok=True)
    source_hash = file_sha256(csv_path)
    run_id = run_id or uuid.uuid4().hex
    frame = train_models.load_frame_table(csv_path)
    cleaned = train_models.clean_navigation_rows(frame)
    raw_segments = train_models.split_continuous_segments(frame)
    raw_segments = [segment for segment in raw_segments if len(segment) > 120]
    # Preserve raw availability for receipt-time replay; never globally fill
    # late values before the per-issue visibility filter.
    prepared = [segment.copy() for segment in raw_segments]
    quality = build_quality_report(
        source_path=str(csv_path), source_sha256=source_hash, source_kind=source_kind,
        row_count=len(frame), segment_count=len(prepared), has_received_time='received_seconds' in frame,
        has_current_provenance=False)
    quality.update(cleaned_row_count=len(cleaned), rejected_navigation_row_count=len(frame) - len(cleaned),
                   scoring_config=dict(SCORING_CONFIG), cleaning_max_horizontal_speed_mps=3.0,
                   cleaning_max_gap_seconds=60.0, model_training_overlap='unknown_for_previously_deployed_artifact')
    (output_root / 'quality_report.json').write_text(json.dumps(quality, ensure_ascii=False, indent=2), encoding='utf-8')
    if manifest_path is not None:
        manifest = json.loads(Path(manifest_path).read_text(encoding='utf-8'))
        validate_manifest_source(manifest, csv_path)
    elif len(prepared) >= 3:
        manifest = forecast_data.build_split_manifest(prepared, source_hash)
    else:
        manifest = {'schema_version': 1, 'source_sha256': source_hash,
                    'status': 'insufficient_independent_segments',
                    'segments': [{'segment_index': index, 'row_count': len(segment),
                                  'start_seconds': float(segment.elapsed_seconds.iloc[0]),
                                  'end_seconds': float(segment.elapsed_seconds.iloc[-1])}
                                 for index, segment in enumerate(prepared)]}
    (output_root / 'split_manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    if partition == 'auto':
        partition = 'final_test' if len(prepared) >= 3 else 'exploratory'
    selected_segments = select_replay_segments(prepared, manifest, partition)
    attempts, frozen, score_rows = [], [], []
    for segment_index, segment in selected_segments:
        times = segment.elapsed_seconds.to_numpy(dtype=float)
        candidates = np.flatnonzero((times >= times[0] + 290.) & (times <= times[-1] - 900.))
        if not len(candidates):
            continue
        selected = candidates[np.linspace(0, len(candidates) - 1,
                                          min(max_origins, len(candidates)), dtype=int)]
        for origin_index in selected:
            issued = float(segment.elapsed_seconds.iloc[origin_index])
            input_frame = prepare_forecast_input(segment, issued)
            truth_frame = (raw_segments[segment_index] if 'received_seconds' in frame else
                           train_models.clean_navigation_rows(raw_segments[segment_index]))
            for horizon in forecast_data.SUPPORTED_HORIZONS:
                for method in ('constant_velocity', 'current_deployed_xgboost_with_hold_fallback'):
                    try:
                        # Segment index is a split boundary, not a simulation
                        # branch. Only already-visible origin metadata may bind
                        # this forecast; never use a future row's identity.
                        branch_id = (str(input_frame.iloc[-1].branch_id) if 'branch_id' in input_frame
                                     else f'segment-{segment_index}')
                        record = freeze_forecast(input_frame, issued, horizon, method, run_id,
                                                 branch_id, artifact_root)
                        record.update(segment_index=segment_index, partition=partition, source_sha256=source_hash)
                        frozen.append(record)
                        scored = score_frozen_forecast(record, truth_frame,
                                                      issued + horizon + SCORING_CONFIG['wait_deadline_seconds'], quality['truth_grade'])
                        score_rows.extend({'method': method, 'horizon_seconds': horizon,
                                           'is_endpoint': score['target_elapsed_seconds'] == issued + horizon, **score} for score in scored)
                        valid = [row for row in scored if row['status'] == 'scored']
                        attempts.append({'method': method, 'horizon_seconds': horizon,
                                         'forecast_id': record['forecast_id'], 'partition': partition,
                                         'status': 'scored' if valid else 'missing',
                                         'eligible_point_count': len(scored), 'scored_point_count': len(valid),
                                         'horizontal_error_m': float(np.mean([r['horizontal_error_m'] for r in valid])) if valid else None,
                                         'position_error_m': float(np.sqrt(np.mean([r['position_error_m'] ** 2 for r in valid]))) if valid else None})
                    except Exception as exc:
                        attempts.append({'method': method, 'horizon_seconds': horizon, 'partition': partition,
                                         'status': 'failed', 'reason': str(exc), 'origin_elapsed_seconds': issued})
    _write_jsonl(output_root / 'frozen_forecasts.jsonl', frozen)
    _write_jsonl(output_root / 'scores.jsonl', score_rows)
    _write_jsonl(output_root / 'attempts.jsonl', attempts)
    comparison = build_comparison_report(
        attempts, truth_grade=quality['truth_grade'], mission_count=None,
        segment_count=len(prepared), split_status=quality['split_status'], score_rows=score_rows)
    comparison.update(run_id=run_id, evaluated_partition=partition, evaluated_segment_count=len(selected_segments),
                      scoring_config=dict(SCORING_CONFIG))
    (output_root / 'comparison_report.json').write_text(json.dumps(comparison, ensure_ascii=False, indent=2), encoding='utf-8')
    (output_root / 'comparison_report.md').write_text(_comparison_markdown(comparison, quality), encoding='utf-8')
    return {'quality_report': quality, 'comparison_report': comparison,
            'forecast_count': len(frozen), 'score_count': len(score_rows)}


def _require_horizon(seconds: int) -> None:
    if seconds not in forecast_data.SUPPORTED_HORIZONS:
        raise ValueError('unsupported horizon; use 30, 60, 300 or 900 seconds')


def _forecast_point(target: float, east: float, north: float, depth: float,
                    heading: float, pitch: float, roll: float) -> dict:
    if not all(math.isfinite(value) for value in (target, east, north, depth, heading, pitch, roll)):
        raise ValueError('forecast contains non-finite physical output')
    return {'target_elapsed_seconds': float(target), 'east_m': float(east), 'north_m': float(north),
            'depth_m': float(depth), 'heading_deg': float(heading % 360.0),
            'pitch_deg': float(pitch), 'roll_deg': float(roll),
            'enu': {'eastM': float(east), 'northM': float(north), 'upM': -float(depth)}}


def _interpolate_anchor(anchors: list[dict], seconds: int) -> dict:
    if seconds > 0 and seconds % 30 == 0:
        return dict(anchors[seconds // 30 - 1])
    upper_index = max(0, math.ceil(seconds / 30) - 1)
    lower_index = math.floor(seconds / 30) - 1
    lower = {target: 0.0 for target in TARGETS} if lower_index < 0 else anchors[lower_index]
    upper = anchors[upper_index]
    lower_seconds = 0 if lower_index < 0 else (lower_index + 1) * 30
    amount = (seconds - lower_seconds) / ((upper_index + 1) * 30 - lower_seconds)
    return {target: lower[target] + (upper[target] - lower[target]) * amount for target in TARGETS}


def _score_points(frame: pd.DataFrame, points: list[dict], truth_grade: str) -> list[dict]:
    times = frame.elapsed_seconds.to_numpy(dtype=float)
    result = []
    origin_time = points[0]['target_elapsed_seconds'] - 10.0
    origin_index = int(np.searchsorted(times, origin_time, side='left'))
    origin = frame.iloc[min(origin_index, len(frame) - 1)]
    for point in points:
        target = point['target_elapsed_seconds']
        index = int(np.searchsorted(times, target, side='left'))
        if index >= len(frame) or abs(float(times[index]) - target) > .5:
            result.append({'target_elapsed_seconds': target, 'status': 'missing', 'truth_grade': truth_grade})
            continue
        actual = frame.iloc[index]
        actual_east, actual_north = train_models.local_east_north_meters(
            float(origin.longitude_deg), float(origin.latitude_deg),
            float(actual.longitude_deg), float(actual.latitude_deg))
        east_error, north_error = point['east_m'] - actual_east, point['north_m'] - actual_north
        horizontal = math.hypot(east_error, north_error)
        depth_error = abs(point['depth_m'] - float(actual.depth_m))
        result.append({'target_elapsed_seconds': target, 'status': 'scored', 'truth_grade': truth_grade,
                       'horizontal_error_m': horizontal,
                       'position_error_m': math.hypot(horizontal, depth_error),
                       'depth_error_m': depth_error,
                       'heading_error_deg': abs(train_models.wrap_degrees(point['heading_deg'] - float(actual.heading_deg))),
                       'pitch_error_deg': abs(point['pitch_deg'] - float(actual.pitch_deg)),
                       'roll_error_deg': abs(point['roll_deg'] - float(actual.roll_deg))})
    return result


def _write_jsonl(path: Path, rows: list[dict]) -> None:
    with path.open('w', encoding='utf-8', newline='\n') as stream:
        for row in rows:
            stream.write(json.dumps(row, ensure_ascii=False, allow_nan=False, separators=(',', ':')) + '\n')


def _comparison_markdown(report: dict, quality: dict) -> str:
    lines = ['# 预测算法回放对照', '', f"- 真值等级：`{report['truth_grade']}`",
             f"- 解释边界：`{report['interpretation']}`", f"- 独立最终测试：`{report['independent_final_test']}`",
             f"- 连续段数：{report['continuous_segment_count']}；已验证航次数：未知", '',
             '| 方法 | 状态/尝试 | 已评分 | 失败 | 未评分 | 水平 MAE(m) | 三维 RMSE(m) |',
             '|---|---:|---:|---:|---:|---:|---:|']
    for name, values in report['methods'].items():
        if 'attempted_count' not in values:
            lines.append(f"| {name} | {values['status']} | — | — | — | — | — |")
            continue
        lines.append(f"| {name} | {values['attempted_count']} | {values['scored_count']} | {values['failure_count']} | {values['unscored_count']} | "
                     f"{_number(values['horizontal_mae_m'])} | {_number(values['position_rmse_m'])} |")
    lines += ['', '90% 概率区域覆盖率：不适用；当前方法没有输出并经独立数据校准的概率区域。', '', '限制：']
    lines += [f'- {item}' for item in quality['limitations']]
    lines += ['- 已部署模型的训练数据重叠未知；后建清单不能证明独立最终测试。',
              f"- 本次分区：{report['evaluated_partition']}；实际评估连续段：{report['evaluated_segment_count']}。",
              '- 上表为各次预测均权汇总；这是小样本功能验证，不用于选定算法优胜者。', '',
              '| 方法 | 时长(s) | 已评分点 | 水平 MAE(m) | 终点水平 MAE(m) |',
              '|---|---:|---:|---:|---:|']
    for name, values in report['methods'].items():
        for horizon, metrics in values.get('by_horizon', {}).items():
            lines.append(f"| {name} | {horizon} | {metrics['scored_point_count']} | "
                         f"{_number(metrics['horizontal_mae_m'])} | {_number(metrics['endpoint_horizontal_mae_m'])} |")
    return '\n'.join(lines) + '\n'


def _number(value) -> str:
    return '—' if value is None else f'{value:.3f}'


def main() -> None:
    parser = argparse.ArgumentParser(description='Causal frozen-forecast replay and delayed scoring')
    parser.add_argument('--csv', required=True)
    parser.add_argument('--artifact', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--source-kind', choices=('navigation', 'synthetic'), required=True)
    parser.add_argument('--max-origins', type=int, default=8)
    parser.add_argument('--partition', choices=('auto', 'train', 'development', 'final_test', 'exploratory'), default='auto')
    parser.add_argument('--manifest')
    parser.add_argument('--run-id')
    args = parser.parse_args()
    result = run_validation(Path(args.csv), Path(args.artifact), Path(args.output),
                            args.source_kind, max(1, args.max_origins), args.partition,
                            args.run_id, Path(args.manifest) if args.manifest else None)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
