import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class SocketGemCatalogueTests(unittest.TestCase):
    def setUp(self):
        self.settings, = json.loads((ROOT / 'data/Sanctuary/Balance/socket-gems.json').read_text())
        self.families = {row['id']: row for row in self.settings['families']}

    def test_mapping_and_primary_reference_values(self):
        self.assertEqual(set(self.families), {'Amber', 'Amethyst', 'Citrine', 'Diamond', 'Emerald', 'Ruby', 'Sapphire', 'StarSapphire', 'Tourmaline'})
        self.assertEqual(self.families['Citrine']['source'], 'topaz')
        self.assertEqual(self.families['Tourmaline']['source'], 'skull')
        perfect_ruby = self.families['Ruby']['effects'][4]
        self.assertEqual((perfect_ruby['weapon'][0]['minimum'], perfect_ruby['weapon'][0]['maximum']), (15, 20))
        self.assertEqual(perfect_ruby['shield'][0]['minimum'], 40)
        self.assertEqual(perfect_ruby['armor'][0]['minimum'], 38)
        self.assertEqual(self.families['Citrine']['effects'][4]['armor'][0]['minimum'], 24)
        self.assertEqual(self.families['Diamond']['effects'][4]['shield'][0]['minimum'], 19)
        self.assertEqual(self.families['Emerald']['effects'][4]['weapon'][0]['durationMs'], 7000)
        for family, values in [('Amber', [4, 8, 12, 16, 20]), ('StarSapphire', [1, 2, 3, 4, 5])]:
            for quality, value in enumerate(values):
                row = self.families[family]['effects'][quality]
                self.assertEqual(row['weapon'], row['shield'])
                self.assertEqual(row['shield'], row['armor'])
                self.assertEqual(row['weapon'][0]['minimum'], value)

    def test_agreed_progression_and_authoring_defaults(self):
        self.assertEqual(self.settings['minimumSkills'], [40, 50, 60, 70, 80])
        self.assertEqual(self.settings['prices'], [25, 75, 250, 500, 1500])
        self.assertEqual((self.settings['dropTickets'], self.settings['dropDenominator'], self.settings['quantity']), (5, 100, 1))
        self.assertEqual(self.settings['selection'], 'uniform-family-and-unlocked-quality')
        self.assertEqual(self.settings['upgradeRatio'], 3)
        self.assertEqual(self.settings['upgradeSkills'], [40, 50, 60, 70])
        self.assertTrue(self.settings['fullPackAtFeet'])
        self.assertTrue(all(len(row['effects']) == 5 for row in self.families.values()))
