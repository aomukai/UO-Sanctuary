"""Keep the authored recipe economy, availability and agreed effect ladder reproducible."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'


class CookingMealsTests(unittest.TestCase):
    def setUp(self):
        self.rows = json.loads((BALANCE/'cooking-meals.json').read_text())

    def test_complete_unique_effect_ladder_and_preserved_74_capacity(self):
        self.assertEqual(len(self.rows), 18)
        self.assertEqual(len({r['id'] for r in self.rows}), 18)
        self.assertEqual(len({r['name'] for r in self.rows}), 18)
        effects = ['light','capacity','resources','speed','carving','quiet']
        for tier in [1,2,3]:
            rows = [r for r in self.rows if r['tier'] == tier]
            self.assertEqual({r['effect'] for r in rows}, set(effects))
            self.assertEqual(len(rows), 6)
            self.assertEqual({r['chips'] for r in rows}, {dict(enumerate([5,10,30],1))[tier]})
            for r in rows:
                self.assertEqual(r['magnitude'], [25,50,74][tier-1] if r['effect']=='capacity' else tier)

    def test_economical_portions_and_accessible_ingredient_sources(self):
        shop = {r['Type'] for r in json.loads((BALANCE/'market-supplies.json').read_text())}
        harvested = {'RawFishSteak','RawRibs','RawBird','RawLambLeg'}
        used = set()
        for r in self.rows:
            self.assertTrue(2 <= len(r['ingredients']) <= 6)
            types = [i['type'] for i in r['ingredients']]
            self.assertEqual(len(types), len(set(types)))
            self.assertTrue(set(types) <= shop | harvested)
            self.assertEqual({i['amount'] for i in r['ingredients']}, {1})
            used.update(types)
        self.assertTrue({'RawFishSteak','RawRibs','RawBird','RawLambLeg','BatWing','DaemonBlood','SackFlour'} <= used)

    def test_recipes_are_spaced_through_each_agreed_band(self):
        for tier, lo, hi in [(1,0,50),(2,50,100),(3,100,120)]:
            skills = [r['skill'] for r in self.rows if r['tier']==tier]
            self.assertEqual(len(set(skills)), 6)
            self.assertTrue(all(lo <= s < hi for s in skills))
            self.assertEqual(min(skills), lo)
            self.assertGreaterEqual(max(skills), lo+(hi-lo)*.75)


if __name__ == '__main__':
    unittest.main()
