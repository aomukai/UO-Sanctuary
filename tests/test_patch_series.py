import importlib.machinery
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]
LOADER = importlib.machinery.SourceFileLoader("sanctuary_dev_patches", str(ROOT / "dev"))
SPEC = importlib.util.spec_from_loader(LOADER.name, LOADER)
dev = importlib.util.module_from_spec(SPEC)
LOADER.exec_module(dev)


class PatchSeriesTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.source = self.root / "source"
        self.source.mkdir()
        self.file = self.source / "example.txt"
        self.file.write_text("start\nold\ntail\n")
        patches = self.root / "patches/Example"
        patches.mkdir(parents=True)
        (patches / "01.patch").write_text(
            "--- a/example.txt\n+++ b/example.txt\n@@ -1,3 +1,3 @@\n start\n-old\n+new\n tail\n")
        (patches / "02.patch").write_text(
            "--- a/example.txt\n+++ b/example.txt\n@@ -1,3 +1,3 @@\n start\n new\n-tail\n+end\n")
        root_patch = patch.object(dev, "ROOT", self.root)
        root_patch.start()
        self.addCleanup(root_patch.stop)

    def test_overlapping_patches_apply_once_and_can_be_checked_again(self):
        dev.apply_patches("Example", self.source)
        self.assertEqual(self.file.read_text(), "start\nnew\nend\n")
        dev.apply_patches("Example", self.source)
        self.assertEqual(self.file.read_text(), "start\nnew\nend\n")

    def test_existing_prefix_can_receive_the_next_patch(self):
        self.file.write_text("start\nnew\ntail\n")
        dev.apply_patches("Example", self.source)
        self.assertEqual(self.file.read_text(), "start\nnew\nend\n")

    def test_conflict_preserves_local_edits(self):
        self.file.write_text("start\nmy local edit\ntail\n")
        before = self.file.read_bytes()
        with self.assertRaisesRegex(ValueError, "no files were changed"):
            dev.apply_patches("Example", self.source)
        self.assertEqual(self.file.read_bytes(), before)
