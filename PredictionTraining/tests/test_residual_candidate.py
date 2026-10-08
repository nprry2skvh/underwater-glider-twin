import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


class ResidualCandidateTests(unittest.TestCase):
    def api(self):
        self.assertIsNotNone(importlib.util.find_spec('residual_candidate'), 'residual training interface is missing')
        import residual_candidate
        return residual_candidate

    @staticmethod
    def fixtures():
        rows, segments = [], []
        for segment, partition in enumerate(('train', 'development', 'final_test')):
            segments.append({'segment_index': segment, 'partition': partition,
                             'start_seconds': segment * 1000., 'end_seconds': segment * 1000. + 600.})
            for index in range(8):
                row = {'segment_index': segment, 'partition': partition,
                       'origin_elapsed_seconds': segment * 1000. + 290. + index * 10,
                       'target_elapsed_seconds': segment * 1000. + 320. + index * 10,
                       'horizon_seconds': 30., 'speed_mps': .5 + index * .1,
                       'physics_forecast_id': f'{segment}-{index}', 'profile_hash': 'a' * 64,
                       'current_hash': 'b' * 64}
                for target in ('east_m', 'north_m', 'depth_m', 'heading_deg', 'pitch_deg', 'roll_deg'):
                    row['physics_' + target] = 359. if target == 'heading_deg' else 0.
                    row['actual_' + target] = 1. if target == 'heading_deg' else 2. + index * .1
                rows.append(row)
        return pd.DataFrame(rows), {'source_sha256': 'c' * 64, 'segments': segments}

    def test_final_test_changes_cannot_change_models_limits_or_development_fallback(self):
        rows, manifest = self.fixtures()
        changed = rows.copy()
        changed.loc[changed.partition == 'final_test', 'actual_east_m'] = 99999.
        with tempfile.TemporaryDirectory() as directory:
            one, two = Path(directory) / 'one', Path(directory) / 'two'
            first = self.api().train_candidate(rows, manifest, one, ['speed_mps', 'horizon_seconds'])
            second = self.api().train_candidate(changed, manifest, two, ['speed_mps', 'horizon_seconds'])
            self.assertEqual(first, second)
            self.assertEqual(first['fit_count'], 8)
            self.assertEqual(first['development_count'], 8)
            self.assertEqual(len(first['outputs']), 6)
            self.assertEqual(first['outputs']['heading_deg']['limit'], 2.)
            self.assertEqual(json.loads((one / 'candidate.json').read_text()), first)
            prediction = self.api().predict_residual(one, {'speed_mps': .6, 'horizon_seconds': 30.})
            self.assertEqual(len(prediction), 6)
            self.assertLessEqual(abs(prediction['heading_deg']), 2.)
            for path in one.glob('*.model.json'):
                self.assertEqual(path.read_bytes(), (two / path.name).read_bytes())

    def test_rejects_cross_boundary_missing_lineage_and_truth_as_features(self):
        rows, manifest = self.fixtures()
        with tempfile.TemporaryDirectory() as directory:
            changed = rows.copy()
            changed.loc[0, 'target_elapsed_seconds'] = 1001.
            with self.assertRaisesRegex(ValueError, 'boundary'):
                self.api().train_candidate(changed, manifest, Path(directory) / 'boundary', ['speed_mps'])
            with self.assertRaisesRegex(ValueError, 'lineage'):
                self.api().train_candidate(rows.drop(columns='physics_forecast_id'), manifest,
                                           Path(directory) / 'lineage', ['speed_mps'])
            with self.assertRaisesRegex(ValueError, 'feature'):
                self.api().train_candidate(rows, manifest, Path(directory) / 'leak', ['actual_east_m'])

    def test_missing_paired_rows_reports_not_trained(self):
        with tempfile.TemporaryDirectory() as directory:
            report = self.api().train_candidate(pd.DataFrame(), {}, Path(directory), ['speed_mps'])
            self.assertEqual(report['status'], 'not_trained')


if __name__ == '__main__':
    unittest.main()
