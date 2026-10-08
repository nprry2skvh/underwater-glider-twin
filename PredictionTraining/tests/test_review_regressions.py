import json
import subprocess
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

    @staticmethod
    def _write_quality_csv(path, reference_flags, branch='branch-a', grade='navigation_reference'):
        rows = []
        for index, flag in enumerate(reference_flags):
            row = [0] * 51
            row[0] = f'0d0h0m{index * 10}s'
            row[21:28] = [120. + index * .00001, 25. + index * .00002, index, 80., 359., 2., -1.]
            row[30:32] = [359., 100.]
            rows.append(row)
        source = pd.DataFrame(rows)
        source['has_position_reference'] = reference_flags
        source['branch_id'], source['truth_grade'] = branch, grade
        source.to_csv(path, index=False)

    def test_csv_quality_false_is_preserved_and_cannot_score(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'truth.csv'
            self._write_quality_csv(path, ['False'] * 50)
            loaded = replay.train_models.load_frame_table(path)
            record = replay.freeze_forecast(loaded, 200., 30, 'constant_velocity', 'run', 'branch-a')
            scores = replay.score_frozen_forecast(record, loaded, 290.)
            self.assertEqual([score['status'] for score in scores], ['no_position_reference'] * 3)
            self.assertTrue(all(score['position_error_m'] is None for score in scores))
            self.assertEqual(loaded.branch_id.tolist(), ['branch-a'] * 50)
            self.assertEqual(loaded.truth_grade.tolist(), ['navigation_reference'] * 50)

    def test_csv_quality_parser_handles_boolean_values_without_truthy_strings(self):
        flags = ['false', '0', 'true', '1', ' FALSE ', ' TRUE ']
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'quality.csv'
            self._write_quality_csv(path, flags)
            loaded = replay.train_models.load_frame_table(path)
            self.assertIn('has_position_reference', loaded.columns)
            self.assertEqual(loaded.has_position_reference.tolist(), [False, False, True, True, False, True])

    def test_csv_explicit_quality_missing_or_invalid_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            for bad in ('unknown', '', None, '2'):
                with self.subTest(bad=bad):
                    path = Path(directory) / 'quality.csv'
                    self._write_quality_csv(path, ['true', bad])
                    with self.assertRaisesRegex(ValueError, 'has_position_reference'):
                        replay.train_models.load_frame_table(path)

    def test_invalid_interpolation_endpoint_waits_for_a_valid_exact_observation(self):
        for endpoint in (20, 22):
            with self.subTest(endpoint=endpoint):
                source = fixtures.ForecastReplayTests.frame()
                source['has_position_reference'] = True
                record = replay.freeze_forecast(source, 200., 30, 'constant_velocity', 'run', 'branch-a')
                source.loc[21, 'received_seconds'] = 250.
                source.loc[endpoint, 'has_position_reference'] = False
                before = replay.score_frozen_forecast(record, source, 220.)[0]
                self.assertEqual(before['status'], 'awaiting')
                self.assertIsNone(before['position_error_m'])
                after = replay.score_frozen_forecast(record, source, 250.)[0]
                self.assertEqual(after['status'], 'scored')
                self.assertEqual(after['available_at_seconds'], 250.)

    def test_truth_interpolation_cannot_cross_quality_grade(self):
        source = fixtures.ForecastReplayTests.frame()
        record = replay.freeze_forecast(source, 200., 30, 'constant_velocity', 'run', 'branch-a')
        source['truth_grade'] = 'grade-a'
        source.loc[21, 'received_seconds'] = 250.
        source.loc[22, 'truth_grade'] = 'grade-b'
        self.assertEqual(replay.score_frozen_forecast(record, source, 220.)[0]['status'], 'awaiting')

    def test_direct_truth_false_string_cannot_become_a_successful_score(self):
        source = fixtures.ForecastReplayTests.frame()
        source['has_position_reference'] = 'False'
        record = replay.freeze_forecast(source, 200., 30, 'constant_velocity', 'run', 'branch-a')
        self.assertEqual(replay.score_frozen_forecast(record, source, 240.)[0]['status'], 'no_position_reference')

    def test_imported_simulation_truth_cannot_score_foreign_branch_under_default_grade(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'truth.csv'
            self._write_quality_csv(path, ['True'] * 50, branch='foreign', grade='simulation_branch')
            loaded = replay.train_models.load_frame_table(path)
            record = replay.freeze_forecast(loaded, 200., 30, 'constant_velocity', 'run', 'branch-a')
            scores = replay.score_frozen_forecast(record, loaded, 290.)
            self.assertEqual([score['status'] for score in scores], ['missing'] * 3)

    def test_imported_branch_and_grade_metadata_cannot_be_missing(self):
        with tempfile.TemporaryDirectory() as directory:
            for column in ('branch_id', 'truth_grade'):
                with self.subTest(column=column):
                    path = Path(directory) / 'truth.csv'
                    self._write_quality_csv(path, ['True', 'True'])
                    raw = pd.read_csv(path)
                    raw.loc[1, column] = ''
                    raw.to_csv(path, index=False)
                    with self.assertRaisesRegex(ValueError, column):
                        replay.train_models.load_frame_table(path)

    def test_cli_freezes_visible_simulation_branch_and_rejects_foreign_truth(self):
        training = Path(replay.__file__).resolve().parent
        with tempfile.TemporaryDirectory() as directory:
            for future_branch in ('branch-a', 'branch-b'):
                with self.subTest(future_branch=future_branch):
                    path = Path(directory) / 'truth.csv'
                    output = Path(directory) / future_branch
                    self._write_quality_csv(path, ['True'] * 130, grade='simulation_branch')
                    raw = pd.read_csv(path)
                    raw.loc[30:, 'branch_id'] = future_branch
                    raw.to_csv(path, index=False)
                    result = subprocess.run([
                        sys.executable, str(training / 'validate_forecasts.py'),
                        '--csv', str(path), '--artifact', str(training.parent / 'Models/XGBoost'),
                        '--output', str(output), '--source-kind', 'synthetic',
                        '--max-origins', '1', '--run-id', 'review-cli-branch',
                    ], capture_output=True, text=True, timeout=60)
                    self.assertEqual(result.returncode, 0, result.stderr)
                    forecasts = [json.loads(line) for line in (output / 'frozen_forecasts.jsonl').read_text().splitlines()]
                    constant = [row for row in forecasts if row['method'] == 'constant_velocity']
                    self.assertEqual(len(constant), 4)
                    self.assertEqual({row['branch_id'] for row in constant}, {'branch-a'})
                    self.assertEqual({row['segment_index'] for row in constant}, {0})
                    self.assertEqual({row['origin_elapsed_seconds'] for row in constant}, {290.})
                    scores = [json.loads(line) for line in (output / 'scores.jsonl').read_text().splitlines()]
                    constant_scores = [row for row in scores if row['method'] == 'constant_velocity']
                    self.assertEqual(len(constant_scores), 129)
                    expected = 'scored' if future_branch == 'branch-a' else 'missing'
                    self.assertEqual({row['status'] for row in constant_scores}, {expected})

    def test_csv_explicit_simulation_grade_requires_branch_column(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'truth.csv'
            self._write_quality_csv(path, ['True'] * 50, grade='simulation_branch')
            raw = pd.read_csv(path).drop(columns='branch_id')
            raw.to_csv(path, index=False)
            with self.assertRaisesRegex(ValueError, 'branch_id'):
                replay.train_models.load_frame_table(path)

    def test_direct_simulation_truth_without_branch_identity_cannot_score(self):
        source = fixtures.ForecastReplayTests.frame()
        record = replay.freeze_forecast(source, 200., 30, 'constant_velocity', 'run', 'branch-a')
        source['truth_grade'] = 'simulation_branch'
        with self.assertRaisesRegex(ValueError, 'branch_id'):
            replay.score_frozen_forecast(record, source, 290.)

    def test_explicit_navigation_truth_can_score_foreign_branch_under_simulation_default(self):
        source = fixtures.ForecastReplayTests.frame()
        record = replay.freeze_forecast(source, 200., 30, 'constant_velocity', 'run', 'branch-a')
        source['branch_id'] = 'navigation'
        source['truth_grade'] = 'navigation_reference'
        source['has_position_reference'] = True
        for default_grade in ('navigation_record_unverified', 'synthetic_simulation'):
            with self.subTest(default_grade=default_grade):
                scores = replay.score_frozen_forecast(record, source, 290., default_grade)
                self.assertEqual([row['status'] for row in scores], ['scored'] * 3)
                self.assertEqual([row['truth_grade'] for row in scores], ['navigation_reference'] * 3)


if __name__ == '__main__':
    unittest.main()
