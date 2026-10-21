"""Keep every held named definition and its return requirements visible."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('named_readiness', ROOT / 'tools/build_named_readiness.py')
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class NamedReadinessTests(unittest.TestCase):
    def setUp(self):
        self.audit = json.loads(builder.AUDIT.read_text())

    def test_complete_current_export_and_document(self):
        rows = builder.validate(self.audit)
        self.assertEqual(len(rows), 573)
        self.assertEqual(builder.render(self.audit), builder.OUTPUT.read_text())
        self.assertEqual(len({r['Id'] for r in rows if r['Name'] == 'Rainbow Facet'}), 8)

    def test_charms_are_excluded_from_eligibility_and_return_tasks(self):
        rows = builder.validate(self.audit)
        charms = [r for r in rows if r['BaseCode'] in {'cm1', 'cm2', 'cm3'}]
        self.assertEqual(len(charms), 15)
        for row in charms:
            self.assertFalse(row['Available'])
            self.assertIn('Charms are excluded by project policy', row['Reasons'])
            self.assertNotIn('| ' + row['Id'] + ' |', builder.render(self.audit))

    def test_excluded_charm_cannot_be_silently_reenabled(self):
        row = next(r for r in self.audit['Definitions'] if r['BaseCode'] == 'cm1' and r['SourceAvailability'] == 'SpawnableDefinition')
        row.update(Available=True, Reasons=[])
        with self.assertRaisesRegex(ValueError, 'Excluded charm'):
            builder.validate(self.audit)

    def test_missing_variant_cannot_disappear(self):
        self.audit['Definitions'].pop()
        with self.assertRaisesRegex(ValueError, 'lost or duplicated'):
            builder.validate(self.audit)

    def test_stale_runtime_inputs_require_reexport(self):
        key = next(iter(self.audit['InputHashes']))
        self.audit['InputHashes'][key] = 'stale'
        with self.assertRaisesRegex(ValueError, 'Stale named runtime evidence'):
            builder.validate(self.audit)

    def test_missing_source_fingerprint_requires_reexport(self):
        self.audit['InputHashes'].pop(next(iter(self.audit['InputHashes'])))
        with self.assertRaisesRegex(ValueError, 'Incomplete runtime input inventory'):
            builder.validate(self.audit)

    def test_source_inactive_rows_cannot_be_enabled(self):
        row = next(r for r in self.audit['Definitions'] if r['SourceAvailability'] != 'SpawnableDefinition')
        row.update(Available=True, Reasons=[])
        with self.assertRaisesRegex(ValueError, 'Source-inactive'):
            builder.validate(self.audit)

    def test_partial_set_activation_is_rejected(self):
        sets = json.loads((ROOT / 'data/Sanctuary/Balance/equipment-sets.json').read_text())
        source = {r['id']: r for r in json.loads((ROOT / 'data/Sanctuary/Balance/set-items.json').read_text())}
        rows = {r['Id']: r for r in self.audit['Definitions']}
        item_set = next(s for s in sets if len(s['pieceIds']) > 1
                        and all(source[i]['sourceSpawnable'] for i in s['pieceIds']))
        for i in item_set['pieceIds']:
            rows[i].update(Available=False, Reasons=['Missing prerequisite'])
        rows[item_set['pieceIds'][0]].update(Available=True, Reasons=[])
        with self.assertRaisesRegex(ValueError, 'Partially enabled set'):
            builder.validate(self.audit)


if __name__ == '__main__':
    unittest.main()
