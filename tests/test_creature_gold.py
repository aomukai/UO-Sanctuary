"""Gold projection coverage, exact probabilities and pinned extraction guards."""
import csv
import hashlib
import importlib.util
import json
from fractions import Fraction
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'
SPEC = importlib.util.spec_from_file_location('gold_builder', ROOT / 'tools/build_creature_gold.py')
builder = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(builder)


class CreatureGoldTests(unittest.TestCase):
    def test_complete_policy_and_exact_branch_projection(self):
        catalog = json.loads((BALANCE / 'creature-gold.json').read_text())
        rewards = json.loads((BALANCE / 'creature-rewards.json').read_text())['groups']
        identities = {r['type'] for r in json.loads((BALANCE / 'uo-creatures.json').read_text())}
        policies = {p['id']: p for p in catalog['policies']}
        seen = set()
        for group in rewards:
            policy = policies[group['id']]
            self.assertEqual(policy['types'], group['types'])
            self.assertEqual(policy['eligible'], group['variants']['Normal']['enabled'])
            for name in policy['types']:
                self.assertNotIn(name, seen)
                seen.add(name)
        self.assertEqual(seen, identities)
        self.assertEqual(sum(len(p['types']) for p in policies.values() if p['eligible']), 324)
        self.assertEqual(sum(len(p['types']) for p in policies.values() if p['eligible'] and not p['treasureClass']), 9)
        classes = {c['name']: c for c in catalog['classes']}
        self.assertEqual(len(classes), 104)
        def probability(name, path=()):
            if not name:
                return Fraction(0)
            self.assertNotIn(name, path)
            row = classes[name]
            self.assertEqual(row['picks'], 1)
            total = row['noDrop'] + sum(e['weight'] for e in row['entries'])
            self.assertGreater(total, 0)
            result = Fraction(0)
            for entry in row['entries']:
                self.assertGreater(entry['weight'], 0)
                self.assertEqual(entry['gold'], entry['item'] == 'gld')
                result += Fraction(entry['weight'], total) * (1 if entry['gold'] else probability(entry['nested'], (*path, name)))
            return result
        self.assertEqual(probability('Act 1 H2H A'), Fraction(21, 160))
        for policy in policies.values():
            self.assertTrue(0 <= probability(policy['treasureClass']) <= 1)

    def test_extractor_rejects_source_drift_and_unsupported_semantics(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            monster = 'Id\tTreasureClass1\nzombie1\tFixture\n'
            (folder / 'MonStats.txt').write_text(monster)
            rewards = {'groups': [{'id': 'test', 'types': ['Fixture'], 'variants': {
                'Normal': {'enabled': True, 'reference': 'zombie1'}}}]}
            (folder / 'creature-rewards.json').write_text(json.dumps(rewards))
            def inputs(picks=1, item='gld'):
                fields = ['Treasure Class', 'Picks', 'NoDrop'] + [v for i in range(1, 11) for v in (f'Item{i}', f'Prob{i}')]
                with (folder / 'TreasureClassEx.txt').open('w') as stream:
                    writer = csv.DictWriter(stream, fields, delimiter='\t')
                    writer.writeheader()
                    writer.writerow({'Treasure Class': 'Fixture', 'Picks': picks, 'NoDrop': 100, 'Item1': item, 'Prob1': 21})
                pins = {n: {'sha256': hashlib.sha256((folder / (n + '.txt')).read_bytes()).hexdigest()}
                        for n in ['MonStats', 'TreasureClassEx']}
                (folder / 'sources.json').write_text(json.dumps(pins))
            with patch.object(builder, 'BALANCE', folder):
                inputs()
                self.assertEqual(builder.build(folder)['classes'][0]['entries'][0]['weight'], 21)
                for picks, item in [(2, 'gld'), (-1, 'gld'), (1, 'gld,mul=128'), (1, 'Fixture')]:
                    inputs(picks, item)
                    with self.assertRaises(AssertionError):
                        builder.build(folder)
                inputs()
                (folder / 'MonStats.txt').write_text(monster + '\n')
                with self.assertRaisesRegex(AssertionError, 'MonStats'):
                    builder.build(folder)


if __name__ == '__main__':
    unittest.main()
