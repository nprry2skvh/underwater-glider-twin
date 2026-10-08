import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

import pandas as pd

TRAINING = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TRAINING))


class ForecastReplayTests(unittest.TestCase):
    def api(self):
        self.assertIsNotNone(importlib.util.find_spec('validate_forecasts'), 'replay validator is missing')
        import validate_forecasts
        return validate_forecasts

    @staticmethod
    def frame():
        rows = []
        for index in range(50):
            rows.append({
                'elapsed_seconds': float(index * 10),
                'received_seconds': float(index * 10),
                'longitude_deg': 120.0 + index * .00001,
                'latitude_deg': 25.0 + index * .00002,
                'depth_m': float(index), 'heading_deg': 359.0,
                'pitch_deg': 2.0, 'roll_deg': -1.0,
            })
        return pd.DataFrame(rows)

    def test_future_or_late_received_rows_cannot_change_frozen_constant_velocity(self):
        source = self.frame()
        source.loc[19, 'received_seconds'] = 1000.
        before = self.api().constant_velocity_forecast(source, 200., 60)
        changed = source.copy()
        changed.loc[21:, ['longitude_deg', 'latitude_deg', 'depth_m']] = [99., -88., 9999.]
        changed.loc[19, 'longitude_deg'] = -99.
        after = self.api().constant_velocity_forecast(changed, 200., 60)
        self.assertEqual(before, after)
        self.assertEqual([point['target_elapsed_seconds'] for point in before], [210., 220., 230., 240., 250., 260.])

    def test_manifest_hash_mismatch_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / 'source.csv'
            source.write_text('one', encoding='utf-8')
            manifest = {'source_sha256': '0' * 64}
            with self.assertRaisesRegex(ValueError, 'SHA-256'):
                self.api().validate_manifest_source(manifest, source)

    def test_fit_statistics_accepts_only_train_partition(self):
        rows = pd.DataFrame({'partition': ['train', 'development', 'final_test'], 'value': [1., 100., 10000.]})
        result = self.api().fit_training_statistics(rows, 'value')
        self.assertEqual(result, {'count': 1, 'mean': 1.0, 'standard_deviation': 0.0})
        with self.assertRaisesRegex(ValueError, 'train'):
            self.api().fit_training_statistics(rows[rows.partition != 'train'], 'value')

    def test_report_counts_failures_and_missing_in_denominator_without_probability_claim(self):
        attempts = [
            {'method': 'constant_velocity', 'status': 'scored', 'horizontal_error_m': 3., 'position_error_m': 5.},
            {'method': 'constant_velocity', 'status': 'missing'},
            {'method': 'constant_velocity', 'status': 'failed'},
        ]
        report = self.api().build_comparison_report(
            attempts, truth_grade='navigation_record_unverified', mission_count=None,
            segment_count=1, split_status='insufficient_independent_segments')
        method = report['methods']['constant_velocity']
        self.assertEqual(method['attempted_count'], 3)
        self.assertEqual(method['scored_count'], 1)
        self.assertEqual(method['failure_count'], 1)
        self.assertEqual(method['unscored_count'], 1)
        self.assertAlmostEqual(method['success_rate'], 1 / 3)
        self.assertIsNone(method['prediction_region_90_coverage'])
        self.assertEqual(report['interpretation'], 'consistency_with_navigation_record_only')
        self.assertFalse(report['independent_final_test'])

    def test_artifact_evaluator_matches_flat_tree_branching(self):
        model = {'base_score': .5, 'trees': [{'nodes': [
            {'feature_index': 0, 'threshold': 2., 'yes_index': 1, 'no_index': 2, 'missing_index': 1},
            {'feature_index': -1, 'leaf_value': .25}, {'feature_index': -1, 'leaf_value': 2.},
        ]}]}
        self.assertEqual(self.api().evaluate_flat_tree(model, [1.]), .75)
        self.assertEqual(self.api().evaluate_flat_tree(model, [3.]), 2.5)

    def test_exact_thirty_second_anchor_does_not_divide_by_zero(self):
        anchors = [{name: 7.0 for name in self.api().TARGETS}]
        actual = self.api()._interpolate_anchor(anchors, 30)
        self.assertEqual(actual['east_displacement_m'], 7.0)

    def test_audit_report_does_not_upgrade_synthetic_or_single_segment_data(self):
        report = self.api().build_quality_report(
            source_path='sample.csv', source_sha256='a' * 64, source_kind='synthetic',
            row_count=50, segment_count=1, has_received_time=False, has_current_provenance=False)
        self.assertEqual(report['replay_mode'], 'sample_time_replay')
        self.assertEqual(report['truth_grade'], 'synthetic_simulation')
        self.assertFalse(report['eligible_for_independent_operational_acceptance'])
        self.assertEqual(report['split_status'], 'insufficient_independent_segments')

    def test_replay_selects_only_requested_fixed_partition(self):
        segments = [self.frame() for _ in range(3)]
        import forecast_data
        manifest = forecast_data.build_split_manifest(segments, 'a' * 64)
        selected = self.api().select_replay_segments(segments, manifest, 'final_test')
        self.assertEqual([item[0] for item in selected], [2])
        changed = json.loads(json.dumps(manifest))
        changed['segments'][2]['end_seconds'] = 9999.
        with self.assertRaisesRegex(ValueError, 'boundary'):
            self.api().select_replay_segments(segments, changed, 'final_test')

    def test_frozen_input_digest_is_not_output_digest_and_future_does_not_change_it(self):
        frame = self.frame()
        frame.loc[19, 'received_seconds'] = 1000.
        first = self.api().freeze_forecast(frame, 200., 30, 'constant_velocity', 'run', 'branch')
        changed = frame.copy()
        changed.loc[21:, 'depth_m'] = 9999.
        changed.loc[19, 'longitude_deg'] = -99.
        second = self.api().freeze_forecast(changed, 200., 30, 'constant_velocity', 'run', 'branch')
        self.assertEqual(first, second)
        self.assertEqual(first['run_id'], 'run')
        self.assertEqual(first['branch_id'], 'branch')
        self.assertNotEqual(first['input_digest'], first['output_digest'])

    def test_delayed_score_obeys_receive_time_gap_and_deadline(self):
        frame = self.frame()
        forecast = self.api().freeze_forecast(frame, 200., 30, 'constant_velocity', 'run', 'branch')
        frame.loc[21:23, 'received_seconds'] = 1000.
        scores = self.api().score_frozen_forecast(forecast, frame, 290.)
        self.assertEqual([score['status'] for score in scores], ['missing', 'missing', 'missing'])
        self.assertTrue(all(score['position_error_m'] is None for score in scores))

    def test_deployed_features_are_recomputed_from_only_available_rows(self):
        frame = self.frame()
        for column, value in [('target_heading_deg', 359.), ('target_depth_m', 100.), ('turn_angle_deg', 1.), ('piston_mm', 0.)]:
            frame[column] = value
        frame['velocity_x'] = 9999.
        frame.loc[19, 'received_seconds'] = 1000.
        before = self.api().runtime_features(frame, 400., ['velocity_x', 'depth_mean', 'forecast_horizon_seconds'], 30)
        changed = frame.copy()
        changed.loc[19, ['longitude_deg', 'depth_m', 'velocity_x']] = [-99., 9999., -9999.]
        changed.loc[41:, 'depth_m'] = 9999.
        after = self.api().runtime_features(changed, 400., ['velocity_x', 'depth_mean', 'forecast_horizon_seconds'], 30)
        self.assertEqual(before, after)
        self.assertLess(before[0], 1.)

    def test_cli_pipeline_exports_requested_partition_with_input_lineage_and_horizons(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifact = root / 'artifact'
            artifact.mkdir()
            (artifact / 'feature_schema.json').write_text(json.dumps({
                'history_length': 30, 'sample_interval_seconds': 10,
                'feature_names': ['depth_m', 'forecast_horizon_seconds'], 'mean': [0., 0.], 'scale': [1., 1.]}), encoding='utf-8')
            (artifact / 'validation_report.json').write_text(json.dumps({
                'output_sources': {target: 'stable' for target in self.api().TARGETS}}), encoding='utf-8')
            schema_path = artifact / 'feature_schema.json'
            (artifact / 'manifest.json').write_text(json.dumps({
                'artifact_schema_version': 1, 'validation_status': 'accepted', 'training_run_id': 'fixture',
                'files': [{'path': 'feature_schema.json', 'sha256': self.api().file_sha256(schema_path),
                           'size_bytes': schema_path.stat().st_size}]}), encoding='utf-8')
            short = [0] * 51
            short[0] = '0d0h0m0s'
            short[21:28] = [120., 25., 10., 80., 359., 2., -1.]
            short[30:32] = [359., 100.]
            rows = [short]
            for segment in range(3):
                for index in range(150):
                    row = [0] * 51
                    row[0] = f'0d0h0m{(segment + 1) * 3000 + index * 10}s'
                    row[21:28] = [120 + index * .00001, 25 + index * .00001, 10., 80., 359., 2., -1.]
                    row[30:32] = [359., 100.]
                    rows.append(row)
            source = root / 'source.csv'
            pd.DataFrame(rows).to_csv(source, index=False)
            output = root / 'output'
            result = self.api().run_validation(source, artifact, output, 'synthetic', 1,
                                               partition='final_test', run_id='fixed-run')
            frozen = [json.loads(line) for line in (output / 'frozen_forecasts.jsonl').read_text().splitlines()]
            self.assertEqual({record['segment_index'] for record in frozen}, {2})
            self.assertEqual({record['partition'] for record in frozen}, {'final_test'})
            self.assertEqual({record['run_id'] for record in frozen}, {'fixed-run'})
            self.assertTrue(all(record['input_digest'] != record['output_digest'] for record in frozen))
            metrics = result['comparison_report']['methods']['constant_velocity']
            self.assertEqual(set(metrics['by_horizon']), {'30', '60', '300', '900'})
            self.assertIn('heading_mae_deg', metrics)
            self.assertIn('scored_point_count', metrics)
            self.assertGreater(metrics['scored_point_count'], 0)

    def test_csv_preserves_receive_metadata_without_filling_from_unreceived_rows(self):
        import train_models
        with tempfile.TemporaryDirectory() as directory:
            rows = []
            for index in range(3):
                row = [0] * 51
                row[0] = f'0d0h0m{index * 10}s'
                row[21:28] = [120., 25., 10., 80., 359., 2., -1.]
                row[30] = 359.
                row[31] = [10., 999., None][index]
                rows.append(row)
            data = pd.DataFrame(rows)
            data['received_seconds'] = [0., 1000., 20.]
            source = Path(directory) / 'received.csv'
            data.to_csv(source, index=False)
            loaded = train_models.load_frame_table(source)
            self.assertIn('received_seconds', loaded)
            self.assertTrue(pd.isna(loaded.iloc[2].target_depth_m))
            import forecast_data
            visible = forecast_data.causal_history(loaded, 20.)
            self.assertEqual(visible.iloc[-1].target_depth_m, 10.)


if __name__ == '__main__':
    unittest.main()
