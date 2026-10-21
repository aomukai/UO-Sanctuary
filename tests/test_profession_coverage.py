import importlib.util
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('profession_coverage', ROOT / 'tools/build_profession_coverage.py')
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class ProfessionCoverageTests(unittest.TestCase):
    def test_missing_production_route_is_rejected(self):
        import json
        with tempfile.TemporaryDirectory() as name:
            root = Path(name)
            balance = root / 'data/Sanctuary/Balance'
            shutil.copytree(ROOT / 'data/Sanctuary/Balance', balance)
            path = balance / 'equipment-recipes.json'
            rows = json.loads(path.read_text())
            path.write_text(json.dumps([r for r in rows if r['skill'] != 'Blacksmith']))
            with patch.object(builder, 'ROOT', root), patch.object(builder, 'BALANCE', balance):
                with self.assertRaisesRegex(AssertionError, 'Missing production gain paths.*Blacksmith'):
                    builder.build()
