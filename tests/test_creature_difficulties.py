"""Higher variants reproduce pinned stats and treasure upgrades without changing Normal."""
import csv
import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import build_creature_difficulties as profiles
import build_creature_gold as gold
import build_gear_drops as gear
BALANCE = ROOT / 'data/Sanctuary/Balance'
SOURCE = ROOT / '.local/research/foundation-pinned'


class CreatureDifficultyTests(unittest.TestCase):
    def test_complete_higher_variant_and_loot_coverage(self):
        catalogue = json.loads((BALANCE / 'creature-difficulties.json').read_text())
        rewards = json.loads((BALANCE / 'creature-rewards.json').read_text())['groups']
        expected = {t for g in rewards if g['variants']['Normal']['enabled'] for t in g['types']}
        actual = {t for g in catalogue['groups'] for t in g['types']}
        self.assertEqual(expected, actual)
        self.assertEqual(len(actual), 324)
        for difficulty in ['Nightmare', 'Hell']:
            treasure = json.loads((BALANCE / ('creature-gold-' + difficulty.lower() + '.json')).read_text())
            drops = json.loads((BALANCE / ('gear-drops-' + difficulty.lower() + '.json')).read_text())
            self.assertEqual(treasure['difficulty'], difficulty)
            self.assertEqual(drops['difficulty'], difficulty)
            self.assertEqual(drops['setUniquePolicy'], 'NamedWhenAvailable')
            self.assertEqual(drops['whiteQuality'], 'Normal')
            pool = {r['name']: r for r in drops['pools']}
            bases = {r['code']: r for r in drops['bases']}
            excluded = {r['item'] for r in drops['excludedLeaves']}
            for row in treasure['classes']:
                self.assertEqual(row['picks'], 1)
                for e in row['entries']:
                    if not e['nested'] and not e['gold']:
                        self.assertTrue(e['item'] in pool or e['item'] in excluded or bases[e['item']]['variants'])
            reachable = {e['code'] for p in pool.values() for e in p['entries'] if bases[e['code']]['variants']}
            self.assertTrue(any(bases[c]['tier'] == 'Exceptional' for c in reachable))
            if difficulty == 'Hell':
                self.assertTrue(any(bases[c]['tier'] == 'Elite' for c in reachable))
            for group in catalogue['groups']:
                row = group['variants'][difficulty]
                self.assertTrue(row['enabled'] and row['applyStats'])
                self.assertTrue(1 <= row['life'] <= 65000)
                self.assertGreater(row['attack1']['minDamage'], 0)
                self.assertGreaterEqual(row['attack1']['maxDamage'], row['attack1']['minDamage'])
            self.assertTrue(any(g['variants'][difficulty]['resistances']['fire'] >= 100 for g in catalogue['groups']))

    @unittest.skipUnless((SOURCE / 'MonStats.txt').exists(), 'Pinned source cache unavailable')
    def test_pinned_reproduction_and_real_source_upgrade(self):
        self.assertEqual(profiles.build(SOURCE), json.loads((BALANCE / 'creature-difficulties.json').read_text()))
        classes = list(csv.DictReader((SOURCE / 'TreasureClassEx.txt').open(), delimiter='\t'))
        monsters = {r['Id']: r for r in csv.DictReader((SOURCE / 'MonStats.txt').open(), delimiter='\t') if r['Id']}
        rewards = {g['id']: g for g in json.loads((BALANCE / 'creature-rewards.json').read_text())['groups']}
        for difficulty, suffix in [('Normal', ''), ('Nightmare', '(N)'), ('Hell', '(H)')]:
            filename = '' if difficulty == 'Normal' else '-' + difficulty.lower()
            actual = gold.build(SOURCE, difficulty)
            self.assertEqual(actual, json.loads((BALANCE / ('creature-gold' + filename + '.json')).read_text()))
            self.assertEqual(gear.build(SOURCE, difficulty), json.loads((BALANCE / ('gear-drops' + filename + '.json')).read_text()))
            upgraded = 0
            for policy in actual['policies']:
                if not policy['treasureClass']:
                    continue
                mon = monsters[policy['reference']]
                start = mon['TreasureClass1' + suffix]
                selected = start
                if difficulty != 'Normal' and mon['noRatio'] != '1' and mon['boss'] != '1':
                    index = next(i for i, c in enumerate(classes) if c['Treasure Class'] == start)
                    row = classes[index]
                    if row['group']:
                        for following in classes[index + 1:]:
                            if following['group'] != row['group'] or int(following['level'] or 0) > rewards[policy['id']]['variants'][difficulty]['level']:
                                break
                            selected = following['Treasure Class']
                self.assertEqual(selected, policy['treasureClass'])
                upgraded += selected != start
            if difficulty != 'Normal':
                self.assertGreater(upgraded, 0)


if __name__ == '__main__':
    unittest.main()
