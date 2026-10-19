"""Reviewed roster coverage and XP authoring safeguards; no live world or research cache needed."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'
SPEC = importlib.util.spec_from_file_location('creature_builder', ROOT / 'tools/build_creature_profiles.py')
builder = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(builder)


class CreatureRewardTests(unittest.TestCase):
    def test_every_type_has_one_explicit_policy_and_difficulty_rewards(self):
        identities = {r['type']: r for r in json.loads((BALANCE / 'uo-creatures.json').read_text())}
        policies = json.loads((BALANCE / 'creature-rewards.json').read_text())['groups']
        authored = json.loads((BALANCE / 'creature-reward-rules.json').read_text())['groups']
        self.assertEqual({g['id']: g['types'] for g in authored}, {g['id']: g['types'] for g in policies})
        seen = {}
        for group in policies:
            for name in group['types']:
                self.assertNotIn(name, seen)
                seen[name] = group['variants']
            levels, xp = [], []
            for difficulty, maximum in [('Normal', 43), ('Nightmare', 68), ('Hell', 85)]:
                row = group['variants'][difficulty]
                self.assertTrue(row['rationale'])
                if row['enabled']:
                    self.assertTrue(row['reference'])
                    self.assertLessEqual(1, row['level'])
                    self.assertLessEqual(row['level'], maximum)
                    self.assertGreater(row['experience'], 0)
                    levels.append(row['level'])
                    xp.append(row['experience'])
                else:
                    self.assertEqual(row['experience'], 0)
                    self.assertIsNone(row['level'])
            self.assertEqual(levels, sorted(levels))
            self.assertEqual(xp, sorted(xp))
        self.assertEqual(set(identities), set(seen))
        self.assertEqual(sum(v['Normal']['enabled'] for v in seen.values()), 324)
        for name, identity in identities.items():
            if identity['role'] == 'Vendor' or identity['parent'] == 'Server.Mobiles.BaseChampion':
                self.assertFalse(seen[name]['Normal']['enabled'], name)
        for name in ['Server.Mobiles.Cow', 'Server.Mobiles.Harrower', 'Server.Mobiles.Barracoon',
                     'Server.Mobiles.Provisioner', 'Sanctuary.SkeletonGuardian', 'Server.Mobiles.Revenant']:
            self.assertFalse(seen[name]['Normal']['enabled'])
        for name in ['Server.Mobiles.Orc', 'Server.Mobiles.Dragon', 'Server.Mobiles.ShadowFiend']:
            self.assertTrue(seen[name]['Normal']['enabled'])

    def test_source_percent_is_not_mistaken_for_base_xp_and_bad_assignments_fail(self):
        # Pinned Venom Lord rows: raw Exp is 152/130/130 percent, not the kill reward.
        mon = dict(enabled='1', killable='1', boss='', Exp='152', **{'Exp(N)': '130', 'Exp(H)': '130'})
        levels = {30: {'L-XP': '659'}, 57: {'L-XP(N)': '10044'}, 84: {'L-XP(H)': '49836'}}
        rule = dict(id='dragon', reference='megademon3', levels=[30, 57, 84],
                    rationale='Fire-breathing dragon analogue.', types=['Server.Mobiles.Dragon'])
        rules = dict(sourceCommit='fixture', groups=[rule])
        with tempfile.TemporaryDirectory() as temporary, patch.object(builder, 'OUT', Path(temporary)):
            path = Path(temporary) / 'creature-reward-rules.json'
            path.write_text(json.dumps(rules))
            output = {}
            builder.build_rewards([{'type': 'Server.Mobiles.Dragon'}], {'megademon3': mon}, levels,
                                  lambda name, data: output.update({name: data}))
            variants = output['creature-rewards']['groups'][0]['variants']
            self.assertEqual([variants[d]['experience'] for d in ['Normal', 'Nightmare', 'Hell']], [1001, 13057, 64786])
            with self.assertRaisesRegex(AssertionError, 'Missing reward assignments'):
                builder.build_rewards([{'type': 'Server.Mobiles.Dragon'}, {'type': 'NewMonster'}],
                                      {'megademon3': mon}, levels, lambda *_: None)
            duplicate = copy.deepcopy(rule)
            duplicate['id'] = 'duplicate'
            rules['groups'].append(duplicate)
            path.write_text(json.dumps(rules))
            with self.assertRaisesRegex(AssertionError, 'Duplicate reward assignment'):
                builder.build_rewards([{'type': 'Server.Mobiles.Dragon'}], {'megademon3': mon}, levels, lambda *_: None)
            rules['groups'].pop()
            path.write_text(json.dumps(rules))
            with self.assertRaises(AssertionError):
                builder.build_rewards([{'type': 'Server.Mobiles.Dragon'}], {'megademon3': mon | {'boss': '1'}},
                                      levels, lambda *_: None)


if __name__ == '__main__':
    unittest.main()
