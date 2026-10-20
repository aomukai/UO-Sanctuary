"""Pinned treasure weights, unsupported outcomes and reproducible UO mapping."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import build_gear_drops as builder

BALANCE = ROOT / 'data/Sanctuary/Balance'
SOURCE = ROOT / '.local/research/foundation-pinned'


class GearDropTests(unittest.TestCase):
    def test_complete_treasure_leaf_accounting_without_probability_redistribution(self):
        catalog = json.loads((BALANCE / 'gear-drops.json').read_text())
        treasure = json.loads((BALANCE / 'creature-gold.json').read_text())
        bases = {b['code']: b for b in catalog['bases']}
        pools = {p['name']: p for p in catalog['pools']}
        exclusions = {e['item'] for e in catalog['excludedLeaves']}
        self.assertEqual(catalog['setUniquePolicy'], 'Empty')
        self.assertEqual(catalog['whiteQuality'], 'Normal')
        for row in treasure['classes']:
            for entry in row['entries']:
                if not entry['nested'] and not entry['gold']:
                    self.assertTrue(entry['item'] in pools or entry['item'] in exclusions
                                    or bases.get(entry['item'], {}).get('variants'), entry)
        self.assertFalse(pools['bow15']['entries'])
        self.assertFalse(pools['bow21']['entries'])
        self.assertIn('isc', exclusions)  # Not a free Identify-scroll supply yet.
        self.assertIn('r01', exclusions)  # No legacy D2 runes bypassing moulds.
        self.assertTrue(any(not bases[e['code']]['variants'] for p in pools.values() for e in p['entries']))
        for pool in pools.values():
            self.assertEqual(len(pool['entries']), len({e['code'] for e in pool['entries']}))
            for entry in pool['entries']:
                self.assertGreater(entry['weight'], 0)
                level = int(''.join(c for c in pool['name'] if c.isdigit()))
                self.assertTrue(level - 3 < bases[entry['code']]['qualityLevel'] <= level)

    @unittest.skipUnless((SOURCE / 'ItemRatio.txt').is_file(), 'Pinned source cache unavailable; use fetch_balance_sources.py')
    def test_exact_pinned_catalogue_reproduction(self):
        self.assertEqual(builder.build(SOURCE), json.loads((BALANCE / 'gear-drops.json').read_text()))

    def test_source_drift_is_rejected_before_building(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary)
            (source / 'Weapons.txt').write_text('changed source')
            with self.assertRaisesRegex(AssertionError, 'Weapons'):
                builder.build(source)


if __name__ == '__main__':
    unittest.main()
