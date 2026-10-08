"""Train-only residual candidates from paired frozen physical forecasts.

This tool never replaces deployed Models. Pair generation must use causal seed
state/profile and physical ENU meters; observed targets belong only to labels.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path

import numpy as np
import pandas as pd
from xgboost import XGBRegressor

OUTPUTS = ('east_m', 'north_m', 'depth_m', 'heading_deg', 'pitch_deg', 'roll_deg')
INPUTS = {'speed_mps', 'origin_depth_m', 'origin_heading_deg', 'horizon_seconds',
          'current_east_mps', 'current_north_mps'} | {'physics_' + name for name in OUTPUTS}
LINEAGE = ('physics_forecast_id', 'profile_hash', 'current_hash')


def _digest(value) -> str:
    return hashlib.sha256(json.dumps(value, sort_keys=True, allow_nan=False,
                                    separators=(',', ':')).encode()).hexdigest()


def _validate(rows: pd.DataFrame, manifest: dict, features: list[str]) -> None:
    if not features or len(set(features)) != len(features) or any(name not in INPUTS for name in features):
        raise ValueError('feature contract forbids actual/future observations')
    if any(name not in rows for name in LINEAGE) or rows[list(LINEAGE)].isna().any().any():
        raise ValueError('paired physics lineage is required')
    for column in LINEAGE:
        if (rows[column].astype(str).str.len() == 0).any():
            raise ValueError('paired physics lineage cannot be empty')
    segments = manifest.get('segments', [])
    mapping = {row['segment_index']: row for row in segments}
    if len(mapping) != len(segments) or {row['partition'] for row in segments} != {'train', 'development', 'final_test'}:
        raise ValueError('three disjoint fixed manifest partitions are required')
    for _, row in rows.iterrows():
        segment = mapping.get(row.segment_index)
        if segment is None or segment['partition'] != row.partition:
            raise ValueError('partition does not match fixed manifest')
        if (row.origin_elapsed_seconds - 290. < segment['start_seconds'] or
                row.target_elapsed_seconds > segment['end_seconds'] or
                row.target_elapsed_seconds <= row.origin_elapsed_seconds or
                row.horizon_seconds != row.target_elapsed_seconds - row.origin_elapsed_seconds):
            raise ValueError('history/target crosses partition boundary or has inconsistent time axis')
    columns = features + ['physics_' + name for name in OUTPUTS] + ['actual_' + name for name in OUTPUTS]
    if not np.isfinite(rows[columns].to_numpy(dtype=float)).all():
        raise ValueError('paired features and labels must be finite')


def train_candidate(rows: pd.DataFrame, manifest: dict, output_root: Path,
                    feature_names: list[str]) -> dict:
    output_root = Path(output_root).resolve()
    if any(part.casefold() == 'models' for part in output_root.parts):
        raise ValueError('candidate output must not replace deployed Models')
    if (output_root / 'candidate.json').exists():
        raise FileExistsError('candidate already exists; choose a new output directory')
    if rows.empty:
        report = {'status': 'not_trained', 'reason': 'paired frozen physics/observation rows absent'}
        output_root.mkdir(parents=True, exist_ok=True)
        (output_root / 'candidate.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        return report
    _validate(rows, manifest, feature_names)
    train = rows[rows.partition == 'train'].copy()
    development = rows[rows.partition == 'development'].copy()
    if train.empty or development.empty:
        raise ValueError('train and development pairs are both required')
    # Final-test labels never enter fitting, clipping, fallback, or digests.
    report = {'schema_version': 1, 'status': 'candidate_only',
              'features': list(feature_names), 'fit_count': len(train),
              'development_count': len(development), 'manifest_hash': _digest(manifest),
              'fit_digest': _digest(train.to_dict('records')),
              'development_digest': _digest(development.to_dict('records')),
              'algorithm': 'xgboost_residual_12_trees_depth2_seed0',
              'limit_policy': 'train_absolute_residual_quantile_0.995',
              'fallback_policy': 'development_MAE_not_better_than_zero_residual',
              'final_test_status': 'not_evaluated_by_training', 'outputs': {}}
    models = {}
    for target in OUTPUTS:
        train_y = train['actual_' + target].to_numpy() - train['physics_' + target].to_numpy()
        dev_y = development['actual_' + target].to_numpy() - development['physics_' + target].to_numpy()
        if target == 'heading_deg':
            train_y, dev_y = (train_y + 180.) % 360. - 180., (dev_y + 180.) % 360. - 180.
        limit = max(1e-6, float(np.quantile(np.abs(train_y), .995)))
        model = XGBRegressor(n_estimators=12, max_depth=2, learning_rate=.1,
                             n_jobs=1, random_state=0, objective='reg:squarederror')
        model.fit(train[feature_names].to_numpy(dtype=float), train_y)
        predicted = np.clip(model.predict(development[feature_names].to_numpy(dtype=float)), -limit, limit)
        candidate_error = float(np.mean(np.abs(predicted - dev_y)))
        baseline_error = float(np.mean(np.abs(dev_y)))
        source = 'candidate' if candidate_error < baseline_error else 'zero_residual'
        report['outputs'][target] = {'source': source, 'limit': limit,
                                     'development_mae': candidate_error,
                                     'zero_residual_development_mae': baseline_error,
                                     'model_file': target + '.model.json'}
        models[target] = model
    output_root.mkdir(parents=True, exist_ok=True)
    for target, model in models.items():
        path = output_root / report['outputs'][target]['model_file']
        model.save_model(path)
        report['outputs'][target]['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
    (output_root / 'candidate.json').write_text(json.dumps(report, indent=2, allow_nan=False), encoding='utf-8')
    return report


def predict_residual(candidate_root: Path, features: dict) -> dict:
    root = Path(candidate_root)
    report = json.loads((root / 'candidate.json').read_text(encoding='utf-8'))
    if report['status'] != 'candidate_only':
        raise ValueError('residual candidate is not trained')
    vector = [float(features.get(name, math.nan)) for name in report['features']]
    if not all(math.isfinite(value) for value in vector):
        return {name: 0. for name in OUTPUTS}
    result = {}
    for target, metadata in report['outputs'].items():
        path = root / metadata['model_file']
        if hashlib.sha256(path.read_bytes()).hexdigest() != metadata['sha256']:
            raise ValueError('residual model SHA-256 mismatch')
        model = XGBRegressor()
        model.load_model(path)
        value = float(model.predict(np.asarray([vector], dtype=float))[0]) if metadata['source'] == 'candidate' else 0.
        result[target] = float(np.clip(value, -metadata['limit'], metadata['limit'])) if math.isfinite(value) else 0.
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pairs', required=True, help='paired CSV with physics lineage and manifest partitions')
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--features', nargs='+', required=True)
    args = parser.parse_args()
    source = Path(args.pairs)
    manifest = json.loads(Path(args.manifest).read_text(encoding='utf-8'))
    if hashlib.sha256(source.read_bytes()).hexdigest() != manifest.get('source_sha256'):
        raise ValueError('paired CSV does not match manifest SHA-256')
    print(json.dumps(train_candidate(pd.read_csv(source), manifest, Path(args.output), args.features), indent=2))


if __name__ == '__main__':
    main()
