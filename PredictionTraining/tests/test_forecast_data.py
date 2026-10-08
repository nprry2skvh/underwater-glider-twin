import importlib.util
import sys
import unittest
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import train_models


class ForecastDataTests(unittest.TestCase):
    def api(self):
        self.assertIsNotNone(importlib.util.find_spec('forecast_data'), 'causal forecast data API is missing')
        import forecast_data
        return forecast_data

    def test_resampling_does_not_interpolate_using_future_observation(self):
        frame = pd.DataFrame({'elapsed_seconds': [0., 20.], 'depth_m': [0., 100.], 'heading_deg': [359., 1.]})
        actual = train_models.resample_telemetry(frame)
        self.assertEqual(actual.depth_m.tolist(), [0., 0., 100.])
        self.assertEqual(actual.heading_deg.tolist(), [359., 359., 1.])

    def test_causal_history_ignores_late_received_sample_and_future(self):
        frame = pd.DataFrame({'elapsed_seconds': [0., 10., 20., 30.],
                              'received_seconds': [0., 100., 20., 30.], 'depth_m': [1., 999., 3., 1000.]})
        actual = self.api().causal_history(frame, 20., window_size=3)
        self.assertEqual(actual.depth_m.tolist(), [1., 1., 3.])
        self.assertEqual(actual.attrs['replay_mode'], 'received_time_replay')
        changed = frame.copy()
        changed.loc[1, 'depth_m'] = -99999.
        pd.testing.assert_frame_equal(actual, self.api().causal_history(changed, 20., window_size=3))

    def test_causal_history_does_not_backfill_initial_missing_field(self):
        frame = pd.DataFrame({'elapsed_seconds': [0., 20.], 'depth_m': [np.nan, 10.]})
        actual = self.api().causal_history(frame, 10., window_size=2)
        self.assertTrue(actual.depth_m.isna().all())
        self.assertEqual(actual.attrs['replay_mode'], 'sample_time_replay')

    def test_split_manifest_is_fixed_disjoint_and_has_three_partitions(self):
        segments = [pd.DataFrame({'elapsed_seconds': [i * 2000., i * 2000. + 1800.]}) for i in range(5)]
        manifest = self.api().build_split_manifest(segments, 'a' * 64)
        self.assertEqual([row['partition'] for row in manifest['segments']],
                         ['train', 'train', 'train', 'development', 'final_test'])
        self.assertEqual(len({row['segment_id'] for row in manifest['segments']}), 5)
        self.assertEqual(manifest['source_sha256'], 'a' * 64)

    def test_insufficient_segments_cannot_be_called_final_test(self):
        with self.assertRaisesRegex(ValueError, 'three'):
            self.api().build_split_manifest([pd.DataFrame({'elapsed_seconds': [0., 100.]})], 'a' * 64)

    def test_origins_purge_targets_across_partition_boundary(self):
        actual = self.api().eligible_origins(0., 2000., history_seconds=290., horizon_seconds=900.)
        self.assertEqual(actual[0], 290.)
        self.assertEqual(actual[-1], 1100.)
        self.assertNotIn(1110., actual)

    def test_invalid_receive_timestamp_cannot_become_available(self):
        frame = pd.DataFrame({'elapsed_seconds': [0., 10., 20.],
                              'received_seconds': [0., np.nan, float('inf')], 'depth_m': [1., 2., 3.]})
        actual = self.api().causal_history(frame, 20., window_size=3)
        self.assertEqual(actual.depth_m.tolist(), [1., 1., 1.])


if __name__ == '__main__':
    unittest.main()
