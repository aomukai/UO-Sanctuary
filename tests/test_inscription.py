"""Keep the approved economy values and bonus-only Teleport supply intact."""
import json
from pathlib import Path
import unittest

BALANCE = Path(__file__).resolve().parents[1]/'data/Sanctuary/Balance'


class InscriptionTests(unittest.TestCase):
    def test_approved_ladder_and_material_quantities(self):
        rows = json.loads((BALANCE/'inscription-utilities.json').read_text())
        for category, reagent in [('Identify', 'BlackPearl'), ('Town Portal', 'Bloodmoss')]:
            group = [r for r in rows if r['Category'] == category]
            self.assertEqual([(r['Skill'], r['Charges']) for r in group],
                             [(0,1),(50,3),(60,4),(70,5),(80,6),(90,7),(100,10)])
            for r in group:
                expected = {'BlankScroll': r['Charges'], reagent: r['Charges']}
                if r['Skill']:
                    expected['Leather'] = 1
                self.assertEqual({i['Type']: i['Amount'] for i in r['Ingredients']}, expected)

    def test_scribe_starting_prices_and_bonus_only_teleport(self):
        rows = json.loads((BALANCE/'market-supplies.json').read_text())
        prices = {r['Type']: r['Price'] for r in rows if 'Scribe' in r['Roles']}
        for kind, price in [('IdentifyScroll',80),('TownPortalScroll',100),('IdentifyTome',300),('TownPortalTome',360)]:
            self.assertEqual(prices[kind], price)
        self.assertNotIn('TeleportScroll', {r['Type'] for r in rows})
        recipes = json.loads((BALANCE/'inscription-utilities.json').read_text())
        self.assertNotIn('TeleportScroll', {r['Type'] for r in recipes})
