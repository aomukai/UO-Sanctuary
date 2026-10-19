"""Check the authored economy knobs without running or touching the live world."""
import json
from pathlib import Path
import unittest

BALANCE = Path(__file__).resolve().parents[1] / 'data/Sanctuary/Balance'


class EquipmentEconomyTests(unittest.TestCase):
    def test_provisional_settings_have_variable_yields_and_no_free_essence_loop(self):
        rows = json.loads((BALANCE / 'economy.json').read_text())
        self.assertEqual(len(rows), 1)
        row = rows[0]
        market = json.loads((BALANCE / 'market-settings.json').read_text())[0]
        self.assertEqual(market['VendorResalePercent'], 10)
        self.assertEqual([(market[t + 'Min'], market[t + 'Max']) for t in ['Normal', 'Exceptional', 'Elite']],
                         [(100, 500), (500, 1000), (2500, 5000)])
        self.assertNotIn('resalePercent', row)
        for name in ['magicEssence', 'rareEssence', 'reservedUniqueEssence',
                     'metalScraps', 'woodenChips', 'leatherStrips', 'clothScraps']:
            rule = row[name]
            self.assertLess(rule['min'], rule['max'], name)
            self.assertTrue(0 < rule['chancePercent'] < 100, name)
            if 'levelsPerStep' in rule:
                self.assertGreater(rule['bonusPerStep'], 0)
                self.assertGreater(rule['levelsPerStep'], 0)
                self.assertLessEqual(rule['max'] + 98 // rule['levelsPerStep'] * rule['bonusPerStep'], 60000)
        def mean(rule):
            return (rule['min'] + rule['max']) / 2 * rule['chancePercent'] / 100
        rare = row['rareChancePercent'] / 100
        recycled = (1 - rare) * mean(row['magicEssence']) + rare * mean(row['rareEssence'])
        self.assertLess(recycled, row['imbueEssenceCost'])
        self.assertGreater(row['imbueGoldCost'], 0)


if __name__ == '__main__':
    unittest.main()
