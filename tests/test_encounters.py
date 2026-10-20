"""Pinned source and authored dungeon boundaries for T35."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('build_encounters', ROOT / 'tools/build_encounters.py')
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)

class EncounterCatalogueTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.config = json.loads((ROOT/'data/Sanctuary/Balance/encounters.json').read_text())
        cls.treasure = json.loads((ROOT/'data/Sanctuary/Balance/encounter-treasure.json').read_text())
        cls.rows = {r['name']:r for r in cls.treasure['classes']}

    def test_reproduces_pinned_sources(self):
        source = ROOT/'.local/research/foundation-pinned'
        if not source.exists(): self.skipTest('Pinned research cache is not installed')
        for name, expected in builder.build(source).items():
            self.assertEqual(json.loads((ROOT/'data/Sanctuary/Balance'/name).read_text()), expected, name)

    def test_four_families_cover_each_bounded_band(self):
        for family in {p['family'] for p in self.config['profiles']}:
            profiles = [p for p in self.config['profiles'] if p['family']==family]
            self.assertEqual(sorted((p['minimumLevel'],p['maximumLevel']) for p in profiles), [(60,69),(70,79),(80,89),(90,99)])
        ids = {p['id']:p for p in self.config['profiles']}
        self.assertEqual(len(self.config['named']), 3)
        self.assertEqual(sorted(n['baseLevel']+3 for n in self.config['named']), [97,98,99])
        for named in self.config['named']:
            profile = ids[named['profile']]
            self.assertLessEqual(profile['minimumLevel'],named['baseLevel'])
            self.assertLessEqual(named['baseLevel'],profile['maximumLevel'])
        self.assertEqual(self.config['maximumAddedImmunities'],2)

    def test_source_elite_roots_and_quality_factors(self):
        self.assertEqual(len(self.treasure['roots']),36)
        for root in self.treasure['roots']:
            row = self.rows[root['name']]
            if root['grade']=='Champion': self.assertEqual(row['picks'],-2)
            if root['grade']=='Unique': self.assertEqual(row['picks'],-3)
        for name in ['Act 1 Citem A','Act 5 (H) Citem C','Act 1 Uitem A','Act 5 (H) Uitem C']:
            self.assertEqual(self.rows[name]['factors'],[800,800,800,1024])
        citem = self.rows['Act 1 Citem A']
        gold = next(e for e in citem['entries'] if e['item']=='gld')
        self.assertEqual((gold['weight'],gold['goldMultiplier']),(60,1280))
        self.assertEqual(self.treasure['setUniquePolicy'],'Empty')
        self.assertEqual(self.treasure['maxDrops'],6)

    def test_reachable_trees_are_acyclic_and_bounded(self):
        def depth(name,path):
            if name not in self.rows: return 0
            self.assertNotIn(name,path)
            row = self.rows[name]
            self.assertTrue(1<=abs(row['picks'])<=6)
            self.assertTrue(all(e['weight']>0 for e in row['entries']))
            return 1+max(depth(e['item'],path|{name}) for e in row['entries'])
        self.assertLessEqual(max(depth(r['name'],set()) for r in self.treasure['roots']),64)

if __name__=='__main__': unittest.main()
