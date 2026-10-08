import json
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_forecasts as replay
import test_forecast_replay as fixtures
import test_residual_candidate as residual_fixtures


class ReviewRegressionTests(unittest.TestCase):
    def test_python_feature_vector_matches_shared_physical_fixture(self):
        frame = fixtures.ForecastReplayTests.frame()
        frame = pd.concat([frame, frame, frame]).iloc[:101].copy().reset_index(drop=True)
        frame['elapsed_seconds'] = np.arange(101) * 5.
        frame['received_seconds'] = frame.elapsed_seconds
        frame['longitude_deg'], frame['latitude_deg'], frame['depth_m'] = 120., 25., frame.elapsed_seconds
        frame['pitch_deg'], frame['roll_deg'] = 8., 3.
        frame['target_heading_deg'], frame['target_depth_m'] = 35., 200.
        frame['turn_angle_deg'], frame['piston_mm'] = 0., 0.
        fixture = json.loads((Path(__file__).parent / 'fixtures/runtime_feature_parity.json').read_text())
        actual = replay.runtime_features(frame, 500., fixture['feature_names'], 30.)
        np.testing.assert_allclose(actual, fixture['expected_features'], atol=2e-4, rtol=0.)

    def test_modified_active_model_is_rejected_even_when_json_remains_valid(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'models').mkdir()
            schema = {'history_length': 30, 'sample_interval_seconds': 10,
                      'feature_names': ['depth_m'], 'mean': [0.], 'scale': [1.]}
            model = {'base_score': 0., 'trees': [{'nodes': [{'feature_index': -1, 'leaf_value': 5.}]}]}
            (root / 'feature_schema.json').write_text(json.dumps(schema))
            model_path = root / 'models/east_displacement_m.json'
            model_path.write_text(json.dumps(model))
            sources = {name: 'stable' for name in replay.TARGETS}
            sources['east_displacement_m'] = 'xgboost'
            (root / 'validation_report.json').write_text(json.dumps({'output_sources': sources}))
            paths = [root / 'feature_schema.json', model_path]
            manifest = {'artifact_schema_version': 1, 'validation_status': 'accepted', 'training_run_id': 'test',
                        'files': [{'path': path.relative_to(root).as_posix(), 'sha256': replay.file_sha256(path),
                                   'size_bytes': path.stat().st_size} for path in paths]}
            (root / 'manifest.json').write_text(json.dumps(manifest))
            self.assertEqual(len(replay.deployed_forecast(fixtures.ForecastReplayTests.frame(), 400., 30, root)), 3)
            model['base_score'] = 1000.
            model_path.write_text(json.dumps(model))
            with self.assertRaisesRegex(ValueError, 'SHA-256'):
                replay.deployed_forecast(fixtures.ForecastReplayTests.frame(), 400., 30, root)

    def test_late_rows_are_removed_before_navigation_cleaning(self):
        source = fixtures.ForecastReplayTests.frame()
        source['latitude_deg'] = 25.
        source.loc[19, ['received_seconds', 'longitude_deg']] = [1000., 120.00047]
        source.loc[20, 'longitude_deg'] = 120.00017
        changed = source.copy()
        changed.loc[19, 'longitude_deg'] = 99.
        self.assertTrue(hasattr(replay, 'prepare_forecast_input'), 'causal cleaning boundary missing')
        first = replay.prepare_forecast_input(source, 200.)
        second = replay.prepare_forecast_input(changed, 200.)
        pd.testing.assert_frame_equal(first, second)
        self.assertEqual(first.elapsed_seconds.iloc[-1], 200.)

    def test_first_interpolated_score_is_not_replaced_by_later_exact_observation(self):
        source = fixtures.ForecastReplayTests.frame()
        record = replay.freeze_forecast(source, 200., 30, 'constant_velocity', 'r', 'b')
        source.loc[21, ['received_seconds', 'depth_m']] = [250., 111.]
        first = replay.score_frozen_forecast(record, source, 220.)[0]
        final = replay.score_frozen_forecast(record, source, 290.)[0]
        self.assertEqual(first['depth_error_m'], 0.)
        self.assertEqual(first, final)

    def test_threshold_branch_uses_unity_float32_rounding(self):
        model = {'base_score': 0., 'trees': [{'nodes': [
            {'feature_index': 0, 'threshold': .1000000015, 'yes_index': 1, 'no_index': 2, 'missing_index': 1},
            {'feature_index': -1, 'leaf_value': 100.}, {'feature_index': -1, 'leaf_value': 200.}]}]}
        self.assertEqual(replay.evaluate_flat_tree(model, [np.float32(.1)]), 200.)

    def test_output_spacing_does_not_silently_omit_the_endpoint(self):
        for spacing in (0, 7, 40, float('nan')):
            with self.subTest(spacing=spacing), self.assertRaisesRegex(ValueError, 'interval'):
                replay.constant_velocity_forecast(fixtures.ForecastReplayTests.frame(), 200., 30, spacing)

    def test_artifact_requires_manifest_and_complete_sources(self):
        source = fixtures.ForecastReplayTests.frame()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'feature_schema.json').write_text(json.dumps({'history_length': 30, 'feature_names': ['depth_m']}))
            (root / 'validation_report.json').write_text('{}')
            with self.assertRaisesRegex(ValueError, 'artifact|manifest|sources'):
                replay.deployed_forecast(source, 400., 30, root)

    def test_residual_rejects_unavailable_hash_placeholders(self):
        import residual_candidate
        rows, manifest = residual_fixtures.ResidualCandidateTests.fixtures()
        rows['profile_hash'] = 'unavailable'
        rows['current_hash'] = 'not_provided'
        with tempfile.TemporaryDirectory() as directory, self.assertRaisesRegex(ValueError, 'lineage|hash'):
            residual_candidate.train_candidate(rows, manifest, Path(directory), ['speed_mps'])

    def test_residual_rejects_unavailable_physics_even_with_wellformed_hashes(self):
        import residual_candidate
        rows, manifest = residual_fixtures.ResidualCandidateTests.fixtures()
        rows['physics_available'] = False
        with tempfile.TemporaryDirectory() as directory, self.assertRaisesRegex(ValueError, 'available|lineage'):
            residual_candidate.train_candidate(rows, manifest, Path(directory), ['speed_mps'])

    def test_observed_current_product_cannot_be_issued_after_forecast_origin(self):
        import residual_candidate
        rows, manifest = residual_fixtures.ResidualCandidateTests.fixtures()
        rows['current_kind'] = 'observed_product'
        rows['current_issued_seconds'] = rows.origin_elapsed_seconds + 1.
        rows['current_valid_start_seconds'] = rows.origin_elapsed_seconds - 100.
        rows['current_valid_end_seconds'] = rows.target_elapsed_seconds + 100.
        rows['current_version'] = 'v1'
        with tempfile.TemporaryDirectory() as directory, self.assertRaisesRegex(ValueError, 'current|issued'):
            residual_candidate.train_candidate(rows, manifest, Path(directory), ['speed_mps'])


if __name__ == '__main__':
    unittest.main()
