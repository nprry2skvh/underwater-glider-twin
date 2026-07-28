import json
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
import pandas as pd


TRAINING_DIRECTORY = Path(__file__).resolve().parents[1]
if str(TRAINING_DIRECTORY) not in sys.path:
    sys.path.insert(0, str(TRAINING_DIRECTORY))

import train_models


class XGBoostExportTests(unittest.TestCase):
    def test_exported_booster_uses_contiguous_node_indexes(self):
        exporter = getattr(train_models, "export_booster_as_flat_tree_json", None)
        self.assertIsNotNone(exporter, "XGBoost booster export must be available")
        if train_models.xgb is None:
            self.skipTest("xgboost is unavailable")
        model = train_models.xgb.XGBRegressor(n_estimators=2, max_depth=1, random_state=42)
        model.fit(np.array([[0.0], [1.0], [2.0], [3.0]]), np.array([0.0, 0.0, 1.0, 1.0]))
        with tempfile.TemporaryDirectory() as directory:
            path = exporter(model.get_booster(), Path(directory) / "model.json")
            payload = json.loads(path.read_text(encoding="utf-8"))

        self.assertEqual(payload["tree_count"], 2)
        self.assertEqual(payload["trees"][0]["nodes"][0]["node_index"], 0)
        self.assertIn("base_score", payload)

    def test_validation_report_rejects_worse_900_second_candidate(self):
        builder = getattr(train_models, "build_validation_report", None)
        self.assertIsNotNone(builder, "validation reporting must be available")
        xgboost_metrics = {"by_horizon": {"900": {"endpoint_error_m": 12.0, "depth_mae_m": 4.0}}, "aggregate": {"trajectory_rmse_m": 3.0, "heading_mae_deg": 2.0}}
        physics_metrics = {"by_horizon": {"900": {"endpoint_error_m": 10.0, "depth_mae_m": 3.0}}, "aggregate": {"trajectory_rmse_m": 3.0, "heading_mae_deg": 2.0}}

        report = builder(xgboost_metrics, physics_metrics, {"artifact_version": 1})

        self.assertFalse(report["accepted"])
        self.assertTrue(any("900-second endpoint" in reason for reason in report["rejection_reasons"]))

    def test_validation_report_labels_zero_change_baseline_as_stable_reference(self):
        builder = getattr(train_models, "build_validation_report", None)
        metrics = {"by_horizon": {"900": {"endpoint_error_m": 10.0, "depth_mae_m": 3.0}}, "aggregate": {"trajectory_rmse_m": 3.0, "heading_mae_deg": 2.0}}

        report = builder(metrics, metrics, {"artifact_version": 1})

        self.assertIn("stable_reference", report)
        self.assertNotIn("physics", report)
        self.assertNotIn("Physics", " ".join(report["rejection_reasons"]))

    def test_validation_report_accepts_hybrid_outputs_that_improve_or_preserve_each_metric(self):
        builder = getattr(train_models, "build_validation_report", None)
        raw_xgboost = {"by_horizon": {"900": {"endpoint_error_m": 9.0, "depth_mae_m": 5.0}}, "aggregate": {"trajectory_rmse_m": 2.0, "heading_mae_deg": 6.0}}
        stable_reference = {"by_horizon": {"900": {"endpoint_error_m": 10.0, "depth_mae_m": 3.0}}, "aggregate": {"trajectory_rmse_m": 3.0, "heading_mae_deg": 2.0}}
        deployed = {"by_horizon": {"900": {"endpoint_error_m": 9.0, "depth_mae_m": 3.0}}, "aggregate": {"trajectory_rmse_m": 2.0, "heading_mae_deg": 2.0}}

        report = builder(raw_xgboost, stable_reference, {"artifact_version": 1}, deployed)

        self.assertTrue(report["accepted"])
        self.assertEqual(report["deployment"], deployed)

    def test_train_xgboost_artifact_writes_six_runtime_models(self):
        trainer = getattr(train_models, "train_xgboost_artifact", None)
        self.assertIsNotNone(trainer, "train_xgboost_artifact must train the runtime artifact")
        if train_models.xgb is None:
            self.skipTest("xgboost is unavailable")
        frame = self.make_training_frame(180)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "XGBoost"
            report = trainer(frame, output, train_ratio=0.7, estimator_count=4)
            manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))

        self.assertIn("accepted", report)
        model_paths = [item["path"] for item in manifest["files"] if item["path"].startswith("models/")]
        self.assertEqual(len(model_paths), 6)
        self.assertNotIn("manifest.json", [item["path"] for item in manifest["files"]])
        self.assertIn("output_sources", report)

    def test_train_xgboost_artifact_from_segments_records_held_out_segments(self):
        trainer = getattr(train_models, "train_xgboost_artifact_from_segments", None)
        self.assertIsNotNone(trainer, "XGBoost artifact training must use whole held-out telemetry segments")
        if train_models.xgb is None:
            self.skipTest("xgboost is unavailable")
        segments = []
        for segment_index in range(4):
            segment = self.make_training_frame(140)
            segment.loc[:, "elapsed_seconds"] += segment_index * 4000.0
            segments.append(segment)
        with tempfile.TemporaryDirectory() as directory:
            report = trainer(segments, Path(directory), train_ratio=0.5, max_origins_per_segment=5, estimator_count=2)

        self.assertEqual(report["segment_split"]["train_segment_count"], 2)
        self.assertEqual(report["segment_split"]["validation_segment_count"], 2)

    def test_cli_defaults_to_xgboost_artifact_training(self):
        original_argv = sys.argv
        sys.argv = ["train_models.py", "--csv", "telemetry.csv"]
        try:
            args = train_models.parse_args()
        finally:
            sys.argv = original_argv

        self.assertEqual(args.models, ["xgboost"])
        self.assertGreater(args.max_origins_per_segment, 0)

    def test_select_best_xgboost_candidate_prioritizes_900_second_error(self):
        selector = getattr(train_models, "select_best_xgboost_candidate", None)
        self.assertIsNotNone(selector, "candidate selection must rank held-out long-horizon accuracy")
        candidates = [
            {"name": "short-horizon", "metrics": {"by_horizon": {"30": {"endpoint_error_m": 1.0}, "300": {"endpoint_error_m": 3.0}, "900": {"endpoint_error_m": 100.0}}, "aggregate": {"trajectory_rmse_m": 10.0, "heading_mae_deg": 5.0}}},
            {"name": "long-horizon", "metrics": {"by_horizon": {"30": {"endpoint_error_m": 2.0}, "300": {"endpoint_error_m": 5.0}, "900": {"endpoint_error_m": 20.0}}, "aggregate": {"trajectory_rmse_m": 11.0, "heading_mae_deg": 6.0}}},
        ]

        selected = selector(candidates)

        self.assertEqual(selected["name"], "long-horizon")

    def test_select_output_sources_keeps_xgboost_only_when_validation_improves(self):
        selector = getattr(train_models, "select_output_sources", None)
        self.assertIsNotNone(selector, "output source selection must be explicit in the artifact")
        selected = selector(
            {"east_displacement_m": 1.0, "north_displacement_m": 1.0, "depth_delta_m": 3.0, "heading_delta_deg": 12.0, "pitch_delta_deg": 1.0, "roll_delta_deg": 5.0},
            {"east_displacement_m": 2.0, "north_displacement_m": 2.0, "depth_delta_m": 2.0, "heading_delta_deg": 8.0, "pitch_delta_deg": 2.0, "roll_delta_deg": 4.0},
        )
        self.assertEqual(selected["east_displacement_m"], "xgboost")
        self.assertEqual(selected["depth_delta_m"], "stable")
        self.assertEqual(selected["pitch_delta_deg"], "xgboost")
        self.assertEqual(selected["roll_delta_deg"], "stable")

    @staticmethod
    def make_training_frame(count):
        indexes = np.arange(count)
        return pd.DataFrame({
            "elapsed_seconds": indexes.astype(np.float64) * 10.0,
            "longitude_deg": 120.0 + indexes * 0.00001,
            "latitude_deg": 25.0 + indexes * 0.000005,
            "depth_m": 30.0 + np.sin(indexes / 8.0) * 10.0,
            "heading_deg": (45.0 + indexes * 0.5) % 360.0,
            "pitch_deg": np.sin(indexes / 8.0) * 12.0,
            "roll_deg": np.cos(indexes / 7.0) * 3.0,
            "velocity_x": np.full(count, 0.1), "velocity_y": np.full(count, -0.05),
            "velocity_z": np.full(count, 0.08), "current_speed": np.full(count, 0.14),
            "target_heading_deg": np.full(count, 60.0), "target_depth_m": np.full(count, 100.0),
            "turn_angle_deg": np.full(count, 1.0), "piston_mm": np.full(count, 16.0),
        })


if __name__ == "__main__":
    unittest.main()
