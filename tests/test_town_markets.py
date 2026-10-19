"""T11's authored town coverage and acquisition/economy boundaries."""
import hashlib
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'


class TownMarketTests(unittest.TestCase):
    def test_every_britannian_town_has_all_services_on_each_facet(self):
        raw = (BALANCE / 'town-markets.json').read_bytes()
        rows = json.loads(raw)
        evidence = json.loads((ROOT / 'tools/reference/town-markets-evidence.json').read_text())
        self.assertEqual(hashlib.sha256(raw).hexdigest(), evidence['catalogSha256'])
        self.assertEqual(len(rows), 476)
        towns = {}
        for row in rows:
            key = (row['town'], row['map'])
            towns.setdefault(key, set())
            self.assertNotIn(row['role'], towns[key])
            towns[key].add(row['role'])
            self.assertTrue(row['source'].startswith('Spawns/'))
        self.assertEqual(len(towns), 34)
        roles = {'Smith','Tailor','Bowyer','Carpenter','Alchemist','Cook','Cartographer','Scribe',
                 'Tinker','Imbuer','Miner','Lumberjack','Fisher','Jeweller'}
        self.assertTrue(all(found == roles for found in towns.values()))
        self.assertIn(('Ocllo','Felucca'), towns)
        self.assertIn(('New Haven','Trammel'), towns)

    def test_supply_sources_and_prices_do_not_reintroduce_banned_materials(self):
        supplies = json.loads((BALANCE / 'market-supplies.json').read_text())
        forbidden = {'IronIngot','Board','Log','Leather','Hides','IronOre','Bandage','MetalScraps','WoodenChips','LeatherStrips'}
        self.assertFalse(forbidden.intersection(r['Type'] for r in supplies))
        self.assertEqual({r['Price'] for r in supplies if r['Type'] == 'Cloth'}, {20})
        self.assertTrue(all(0 <= r['SellPrice'] < r['Price'] for r in supplies))
        potions = [r for r in supplies if r['Type'] == 'RestorationPotion']
        self.assertEqual({(r['Kind'],r['Tier']) for r in potions}, {(k,t) for k in ['Healing','Mana'] for t in range(1,5)})
        self.assertTrue(all(r['Roles'] == ['Alchemist'] for r in potions))
        for tool in ['SmithHammer','FletcherTools','Saw','MortarPestle','Skillet','MapmakersPen','ScribesPen','SewingKit','TinkerTools','ImbuingLadle']:
            self.assertEqual([r['Price'] for r in supplies if r['Type'] == tool], [10])


if __name__ == '__main__':
    unittest.main()
