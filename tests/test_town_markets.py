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
        towns = {}
        roadside = {}
        for row in rows:
            if row['source'] == 'Roadside trainer':
                self.assertEqual(row['map'], 'Trammel')
                self.assertNotIn(row['role'], roadside)
                roadside[row['role']] = (row['x'], row['y'], row['z'])
                continue
            key = (row['town'], row['map'])
            towns.setdefault(key, set())
            self.assertNotIn(row['role'], towns[key])
            towns[key].add(row['role'])
            self.assertTrue(row['source'].startswith('Spawns/') or
                            (key == ('New Haven', 'Trammel') and row['source'] == 'New Haven mage tower'))
        self.assertEqual(roadside, {'Fisher': (2733, 1061, 0), 'Cook': (2745, 1066, 0)})
        self.assertEqual(len(towns), 34)
        roles = {'Smith','Tailor','Bowyer','Carpenter','Alchemist','Cook','Cartographer','Scribe',
                 'Tinker','Imbuer','Miner','Lumberjack','Fisher','Jeweller'}
        overrides = {('Cove', 'Trammel'): {'Jeweller'}, ('New Haven', 'Trammel'): roles | {'Scholar'}}
        for key, found in towns.items():
            self.assertEqual(found, overrides.get(key, roles), key)
        self.assertIn(('Ocllo','Felucca'), towns)
        self.assertIn(('New Haven','Trammel'), towns)

    def test_each_trainer_site_has_its_own_portrait(self):
        sites = json.loads((BALANCE / 'town-markets.json').read_text())
        assignments = json.loads((BALANCE / 'trainer-portraits.json').read_text())
        trainer_sites = {row['id'] for row in sites if row['role'] not in {'Jeweller', 'Provisioner', 'Scholar'}}
        self.assertCountEqual([row['site'] for row in assignments], trainer_sites)
        portraits = [row['portrait'] for row in assignments]
        self.assertEqual(len(portraits), len(set(portraits)))
        self.assertTrue(all(portrait >= 256 for portrait in portraits))
        art = ROOT / 'art/player-ui'
        for sheet in {(portrait - 256) // 36 for portrait in portraits}:
            self.assertTrue((art / f'portrait-trainers-{sheet:02}.png').is_file())

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
