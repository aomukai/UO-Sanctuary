"""Verify potion values and supply against the pinned data, without substituting gear outcomes."""
import csv
import hashlib
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'
SOURCE = ROOT / '.local/research/foundation-pinned'


class AlchemyTests(unittest.TestCase):
    @unittest.skipUnless((SOURCE / 'Misc.txt').exists(), 'Pinned D2 tables unavailable')
    def test_ordinary_amounts_durations_and_shop_costs_match_pinned_source(self):
        raw = (SOURCE / 'Misc.txt').read_bytes()
        self.assertEqual(hashlib.sha256(raw).hexdigest(), json.loads((BALANCE / 'sources.json').read_text())['Misc']['sha256'])
        source = {r['code']: r for r in csv.DictReader(raw.decode('utf-8-sig').splitlines(), delimiter='\t')}
        potions = json.loads((BALANCE / 'restoration-potions.json').read_text())
        supplies = json.loads((BALANCE / 'market-supplies.json').read_text())
        for potion in potions:
            if potion['kind'] not in ['Healing','Mana']:
                continue
            code = ('hp' if potion['kind'] == 'Healing' else 'mp') + str(potion['tier'])
            row = source[code]
            self.assertEqual(potion['amount'], int(row['calc1']))
            self.assertEqual(potion['durationMs'], int(row['len']) * 40)
            offers = [r for r in supplies if r['Type'] == 'RestorationPotion' and r['Kind'] == potion['kind'] and r['Tier'] == potion['tier']]
            self.assertEqual(len(offers), 1 if potion['tier'] < 5 else 0)
            if offers:
                self.assertEqual(offers[0]['Price'], int(row['cost']))
                self.assertEqual(offers[0]['Roles'], ['Alchemist'])
        self.assertEqual(len([r for r in supplies if r['Type'] == 'RestorationPotion']), 8)
        for tier, code in [(1, 'rvs'), (2, 'rvl')]:
            potion = next(r for r in potions if r['kind'] == 'Rejuvenation' and r['tier'] == tier)
            self.assertEqual(potion['amount'], int(source[code]['calc1']))
            self.assertEqual(potion['amount'], int(source[code]['calc2']))
            self.assertEqual(potion['durationMs'], 0)

    def test_four_agreed_mixtures_and_independent_ordinary_profiles(self):
        recipes = [r for r in json.loads((BALANCE / 'alchemy-restoratives.json').read_text()) if r['kind'] != 'CombatBuff']
        self.assertEqual([(r['kind'], r['tier'], r['skill'], r['healingTier'], r['healing'], r['manaTier'], r['mana'], r['bottles']) for r in recipes],
                         [('InstantHealing', 1, 50, 5, 3, 0, 0, 1), ('InstantMana', 1, 60, 0, 0, 5, 3, 1),
                          ('Rejuvenation', 1, 70, 4, 3, 4, 3, 1), ('Rejuvenation', 2, 80, 5, 3, 5, 3, 1)])
        potions = json.loads((BALANCE / 'restoration-potions.json').read_text())
        self.assertEqual(len(potions), 19)
        self.assertEqual(len({(r['kind'], r['tier']) for r in potions}), 19)
        self.assertEqual(len([r for r in potions if r['kind'] in ['Healing','Mana']]), 10)

    def test_agreed_combat_recipe_economy_and_effects(self):
        recipes = [r for r in json.loads((BALANCE / 'alchemy-restoratives.json').read_text()) if r['kind'] == 'CombatBuff']
        potions = [r for r in json.loads((BALANCE / 'restoration-potions.json').read_text()) if r['kind'] == 'CombatBuff']
        self.assertEqual([(r['tier'], r['skill'], r['bottles']) for r in recipes], [(1,30,1),(2,100,1),(3,100,1),(4,110,1),(5,120,1)])
        expected = [('Bloodmoss MandrakeRoot SulfurousAsh',5), ('Nightshade SpidersSilk',10),
                    ('Nightshade SulfurousAsh Bloodmoss',10), ('Garlic Ginseng Nightshade SpidersSilk',15),
                    ('Garlic Ginseng MandrakeRoot SulfurousAsh',20)]
        for recipe, (names, amount) in zip(recipes, expected):
            self.assertEqual([(r['type'],r['amount']) for r in recipe['reagents']], [(n,amount) for n in names.split()])
        self.assertEqual([(r['tier'],r['amount'],r['durationMs']) for r in potions], [(1,30,30000),(2,10,30000),(3,10,30000),(4,7,30000),(5,10,30000)])
        supplies = json.loads((BALANCE / 'market-supplies.json').read_text())
        self.assertFalse(any(r.get('Kind') == 'CombatBuff' for r in supplies))

    def test_all_difficulties_keep_rejuvenation_and_other_reserved_leaves_empty(self):
        for suffix in ['', '-nightmare', '-hell']:
            catalog = json.loads((BALANCE / ('gear-drops' + suffix + '.json')).read_text())
            excluded = {r['item']: r['reason'] for r in catalog['excludedLeaves']}
            treasure = json.loads((BALANCE / ('creature-gold' + suffix + '.json')).read_text())
            leaves = {e['item'] for c in treasure['classes'] for e in c['entries'] if not e['nested']}
            for leaf in leaves:
                if len(leaf) == 3 and leaf[:2] in ['hp', 'mp'] and leaf[2] in '12345':
                    self.assertIn('T28 restorative potion supply', excluded[leaf])
            for leaf in ['rvs', 'rvl']:
                if leaf in leaves:
                    self.assertIn('Reserved non-gear system', excluded[leaf])


if __name__ == '__main__':
    unittest.main()
