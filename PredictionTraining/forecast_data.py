"""Causal input preparation and immutable experiment split contracts.

Input times are elapsed seconds in the same run clock. No receive timestamp is
invented for legacy telemetry. Truth interpolation belongs to scoring, not here.
"""
from __future__ import annotations

import hashlib
import math
import re

import numpy as np
import pandas as pd

SUPPORTED_HORIZONS = (30, 60, 300, 900)


def causal_history(frame: pd.DataFrame, issued_seconds: float, window_size: int = 30,
                   sample_interval_seconds: float = 10) -> pd.DataFrame:
    if not math.isfinite(issued_seconds) or window_size < 1 or sample_interval_seconds <= 0:
        raise ValueError('invalid issue time, window or sample interval')
    sampled = pd.to_numeric(frame['elapsed_seconds'], errors='coerce')
    visible = np.isfinite(sampled) & (sampled <= issued_seconds)
    mode = 'sample_time_replay'
    if 'received_seconds' in frame:
        received = pd.to_numeric(frame['received_seconds'], errors='coerce')
        visible &= np.isfinite(received) & (received <= issued_seconds)
        mode = 'received_time_replay'
    history = frame.loc[visible].copy()
    if history.empty:
        history.attrs['replay_mode'] = mode
        return history
    sort = ['elapsed_seconds'] + (['received_seconds'] if 'received_seconds' in history else [])
    history = history.sort_values(sort, kind='stable').drop_duplicates('elapsed_seconds', keep='last')
    grid = issued_seconds - np.arange(window_size - 1, -1, -1) * sample_interval_seconds
    grid = grid[grid >= float(history.elapsed_seconds.iloc[0])]
    indexed = history.set_index('elapsed_seconds').replace([np.inf, -np.inf], np.nan)
    # ffill on the union includes only samples already received at issue time.
    result = indexed.reindex(indexed.index.union(grid)).sort_index().ffill().reindex(grid)
    result.index.name = 'elapsed_seconds'
    result = result.reset_index()
    result.attrs['replay_mode'] = mode
    return result


def eligible_origins(start: float, end: float, history_seconds: float,
                     horizon_seconds: float, sample_interval_seconds: float = 10) -> np.ndarray:
    if (not all(math.isfinite(v) for v in (start, end, history_seconds, horizon_seconds, sample_interval_seconds))
            or end < start or history_seconds < 0 or horizon_seconds <= 0 or sample_interval_seconds <= 0):
        raise ValueError('invalid partition boundaries or durations')
    # Both the earliest input and the furthest target stay inside the partition.
    return np.arange(start + history_seconds, end - horizon_seconds + 1e-7, sample_interval_seconds)


def build_split_manifest(segments: list[pd.DataFrame], source_sha256: str) -> dict:
    if len(segments) < 3:
        raise ValueError('at least three whole segments are required for train/development/final_test')
    if not re.fullmatch('[0-9a-fA-F]{64}', source_sha256):
        raise ValueError('source SHA-256 must be 64 hex characters')
    train_count = min(len(segments) - 2, max(1, int(len(segments) * .6)))
    development_count = min(len(segments) - train_count - 1, max(1, int(len(segments) * .2)))
    rows = []
    for index, segment in enumerate(segments):
        times = segment.elapsed_seconds.to_numpy(dtype=float)
        if len(times) == 0 or not np.isfinite(times).all() or np.any(np.diff(times) <= 0):
            raise ValueError('segments require finite strictly increasing sample times')
        start, end = float(times[0]), float(times[-1])
        identity = f'{source_sha256}:{index}:{start:.9f}:{end:.9f}'
        partition = ('train' if index < train_count else
                     'development' if index < train_count + development_count else 'final_test')
        rows.append({'segment_id': hashlib.sha256(identity.encode()).hexdigest()[:20],
                     'segment_index': index, 'start_seconds': start, 'end_seconds': end,
                     'row_count': len(times), 'partition': partition})
    return {'schema_version': 1, 'source_sha256': source_sha256.lower(),
            'split_unit': 'continuous_segment_not_verified_mission',
            'method': 'chronological_whole_segments_60_20_20', 'segments': rows}
