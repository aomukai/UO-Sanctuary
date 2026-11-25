"""Protect source provenance and stale/unsupported authoring evidence boundaries."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('first_loop_catalogue', ROOT / 'tools/build_first_loop_catalogue.py')
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class FirstLoopCatalogueTests(unittest.TestCase):
    def test_changed_source_is_rejected_before_extracting_rows(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary)
            (source / 'Weapons.txt').write_text('tampered source')
            with self.assertRaisesRegex(ValueError, 'Weapons: source differs'):
                builder.source_tables(source)

    def test_obsolete_manifest_is_rejected_before_reading_runtime_inputs(self):
        with tempfile.TemporaryDirectory() as temporary:
            audit = Path(temporary) / 'audit.json'
            audit.write_text(json.dumps({'ManifestSha256': 'obsolete'}))
            # Pin verification is tested independently; this case needs no local cache.
            from unittest.mock import patch
            with patch.object(builder, 'source_tables', return_value=({}, {})):
                with self.assertRaisesRegex(ValueError, 'Stale affix audit'):
                    builder.build(Path(temporary), audit)

    def test_unsupported_reference_affix_cannot_enter_exported_live_pool(self):
        rows = {r['id']: r for r in json.loads((ROOT / 'data/Sanctuary/Balance/affixes.json').read_text())}
        units = {e['code'] for e in json.loads((ROOT / 'tools/reference/first-loop-cohort.json').read_text())['effects']}
        supported = next(r for r in rows.values()
                         if any(m['code'] == 'str' for m in r['modifiers'])
                         and all(m['mode'] == 'Numeric' and m['code'] in units for m in r['modifiers']))
        sockets = next(r['id'] for r in rows.values() if any(m['code'] == 'sock' for m in r['modifiers']))
        query = dict(BaseCode='hax', ItemLevel=supported['level'], AffixLevel=supported['level'],
                     Rarity='Magic', AffixIds=[supported['id']])
        self.assertIn('str', builder.project_affix_pool(query, rows, units)['effectCodes'])
        query['AffixIds'].append(sockets)
        with self.assertRaisesRegex(ValueError, 'Unsupported compound effect'):
            builder.project_affix_pool(query, rows, units)


if __name__ == '__main__':
    unittest.main()
