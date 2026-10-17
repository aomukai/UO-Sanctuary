import argparse
import importlib.machinery
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]
LOADER = importlib.machinery.SourceFileLoader("sanctuary_dev", str(ROOT / "dev"))
SPEC = importlib.util.spec_from_loader(LOADER.name, LOADER)
dev = importlib.util.module_from_spec(SPEC)
LOADER.exec_module(dev)


class LocalConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.server = self.root / "server"
        self.client = self.root / "client"
        self.data = self.root / "UO data with spaces"
        self.data.mkdir()
        # Presence fixtures exercise setup validation, not a working UO installation.
        for name in ("tiledata.mul", "map0LegacyMUL.uop", "artLegacyMUL.uop"):
            (self.data / name).touch()
        self.args = argparse.Namespace(data=str(self.data), client_version="7.0.0.0", expansion="SA")
        self.server_patch = patch.object(dev, "SERVER", self.server)
        self.client_patch = patch.object(dev, "CLIENT", self.client)
        self.server_patch.start()
        self.client_patch.start()
        self.addCleanup(self.server_patch.stop)
        self.addCleanup(self.client_patch.stop)

    def test_configuration_is_local_and_existing_files_survive_retry(self):
        dev.configure(self.args)
        server_file = self.server / "Configuration/modernuo.json"
        settings = json.loads(server_file.read_text())
        self.assertEqual(settings["listeners"], ["127.0.0.1:2593"])
        self.assertEqual(settings["dataDirectories"], [str(self.data)])
        self.assertEqual(settings["settings"]["serverListing.autoDetect"], "False")
        client = json.loads((self.client / "settings.json").read_text())
        self.assertEqual(client["plugins"], [])
        self.assertFalse(client["saveaccount"])
        self.assertNotIn("password", client)
        expansion = json.loads((self.server / "Configuration/expansion.json").read_text())
        self.assertEqual(expansion["Id"], 8)
        self.assertEqual({name for name, enabled in expansion["MapSelectionFlags"].items() if enabled},
                         {"Felucca", "Trammel"})
        files = [server_file, self.server / "Configuration/expansion.json", self.client / "settings.json"]
        before = [path.read_bytes() for path in files]
        with self.assertRaisesRegex(ValueError, "already exists"):
            dev.configure(self.args)
        self.assertEqual(before, [path.read_bytes() for path in files])

    def test_missing_assets_create_no_configuration(self):
        (self.data / "tiledata.mul").unlink()
        with self.assertRaisesRegex(ValueError, "Missing Classic Client data"):
            dev.configure(self.args)
        self.assertFalse(self.server.exists())
        self.assertFalse(self.client.exists())

    def test_partial_existing_configuration_is_preserved(self):
        existing = self.client / "settings.json"
        existing.parent.mkdir()
        existing.write_text('{"custom": true}')
        with self.assertRaisesRegex(ValueError, "already exists"):
            dev.configure(self.args)
        self.assertEqual(existing.read_text(), '{"custom": true}')
        self.assertFalse(self.server.exists())


if __name__ == "__main__":
    unittest.main()
