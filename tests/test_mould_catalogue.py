import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('mould_builder', ROOT / 'tools/build_mould_catalogue.py')
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)

class MouldCatalogueTests(unittest.TestCase):
    def test_reproduces_complete_pinned_catalogue(self):
        catalogue = json.loads((ROOT / 'data/Sanctuary/Balance/moulds.json').read_text())
        self.assertEqual(catalogue, builder.build())
        self.assertEqual(len(catalogue['tiers']) * len(catalogue['stats']), 120)
        self.assertEqual(sum(s['weight'] for s in catalogue['stats']), 430)
        self.assertEqual((catalogue['ordinaryTickets'], catalogue['dropDenominator']), (10, 10000))
        self.assertFalse(catalogue['magicFindAffects'])

    def test_equipment_minima_and_canonical_names_exclude_charm_jewel_shortcuts(self):
        stats = {s['id']: s for s in builder.build()['stats']}
        expected = {'cast-rate': (10, 'of the Apprentice'), 'run-walk': (10, 'of Pacing'),
                    'magic-find': (5, 'of Chance'), 'gold-find': (25, 'of Greed'),
                    'hit-recovery': (10, 'of Balance'), 'enhanced-damage': (10, 'Jagged')}
        for key, values in expected.items():
            self.assertEqual((stats[key]['baseline'], stats[key]['affixName']), values)
        self.assertEqual(stats['attack-rating']['code'], 'att%')
        self.assertEqual(stats['block-chance']['code'], 'block')
        self.assertEqual(len(stats), 15)

    def test_frequencies_count_each_source_row_once_not_each_uo_appearance(self):
        catalogue = builder.build()
        affixes = {r['id']: r for r in json.loads((ROOT / 'data/Sanctuary/Balance/affixes.json').read_text())}
        for row in catalogue['stats']:
            ids = row['weightAffixes']
            self.assertEqual(len(ids), len(set(ids)))
            self.assertEqual(row['weight'], sum(affixes[i]['frequency'] for i in ids))
        weights = {r['id']: r['weight'] for r in catalogue['stats']}
        self.assertEqual(weights['attack-rating'], 1)
        self.assertGreater(weights['enhanced-damage'], weights['cast-rate'])
        for tier in catalogue['tiers']:
            self.assertEqual(tier['minimumLevel'], 10 + tier['tier'] * 10)
            self.assertEqual(tier['maximumLevel'], 19 + tier['tier'] * 10)
