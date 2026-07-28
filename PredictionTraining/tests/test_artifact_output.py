import json
import sys
import tempfile
import unittest
from pathlib import Path


TRAINING_DIRECTORY = Path(__file__).resolve().parents[1]
if str(TRAINING_DIRECTORY) not in sys.path:
    sys.path.insert(0, str(TRAINING_DIRECTORY))

import train_models


class ArtifactOutputTests(unittest.TestCase):
    def test_manifest_records_hashes_and_excludes_itself(self):
        with tempfile.TemporaryDirectory() as directory:
            root = write_fixture_artifact_tree(Path(directory) / "XGBoost")

            train_models.write_artifact_manifest(root, validation_status="accepted", training_run_id="test-run")

            payload = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(payload["artifact_schema_version"], 1)
            self.assertEqual(payload["validation_status"], "accepted")
            self.assertEqual(payload["training_run_id"], "test-run")
            self.assertTrue(payload["files"])
            self.assertTrue(all(item["path"] != "manifest.json" for item in payload["files"]))
            self.assertTrue(all(not item["path"].startswith("XGBoost/") for item in payload["files"]))
            self.assertTrue(all(len(item["sha256"]) == 64 for item in payload["files"]))

    def test_recoverable_publish_preserves_previous_tree_on_validation_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            models_root = Path(directory) / "Models"
            previous = write_fixture_artifact_tree(models_root / "XGBoost")
            train_models.write_artifact_manifest(previous, validation_status="accepted", training_run_id="previous")
            staging = write_fixture_artifact_tree(Path(directory) / "staging")
            train_models.write_artifact_manifest(staging, validation_status="rejected", training_run_id="bad")

            with self.assertRaises(train_models.ArtifactValidationError):
                train_models.publish_artifact_tree_with_rollback(staging, models_root)

            self.assertEqual(
                json.loads((previous / "manifest.json").read_text(encoding="utf-8"))["training_run_id"],
                "previous",
            )


def write_fixture_artifact_tree(root: Path) -> Path:
    (root / "models").mkdir(parents=True, exist_ok=True)
    (root / "feature_schema.json").write_text('{"feature_names":["depth_m"],"mean":[0],"scale":[1]}', encoding="utf-8")
    (root / "models" / "east_displacement_m.json").write_text('{"tree_count":1,"trees":[]}', encoding="utf-8")
    return root


if __name__ == "__main__":
    unittest.main()
