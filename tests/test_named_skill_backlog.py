import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from build_named_skill_backlog import compile_backlog, render


class NamedSkillBacklogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = compile_backlog()
        cls.items = {r['id']: r for r in cls.data['items']}

    def test_shared_control_and_excluded_charms_are_not_class_tasks(self):
        definitions = json.loads((ROOT / 'data/Sanctuary/Balance/unique-items.json').read_text())
        excluded = [r['id'] for r in definitions if r['baseCode'] in {'cm1', 'cm2', 'cm3'}]
        for item_id in ['unique/1', 'unique/5', 'unique/49', 'piece/66', 'piece/69', *excluded]:
            self.assertNotIn(item_id, self.items)
        self.assertIn('unique/62', self.items)  # Both possible arrow skills remain explicit.

    def test_reproducible_data_and_readable_checklist(self):
        self.assertEqual(self.data, json.loads((ROOT / 'data/Sanctuary/Balance/named-skill-backlog.json').read_text()))
        self.assertEqual(render(self.data), (ROOT / 'docs/deferred-named-skills.md').read_text())

    def test_proc_arguments_resolve_actual_source_spells(self):
        coldkill = self.items['unique/129']['dependencies']
        self.assertEqual([(r['code'], r['skills'][0]['sourceId']) for r in coldkill], [('hit-skill', 45), ('gethit-skill', 44)])
        self.assertEqual([r['skills'][0]['name'] for r in coldkill], ['Ice Blast', 'Frost Nova'])
        self.assertTrue(all(any('dispatcher' in value for value in r['prerequisites']) for r in coldkill))

    def test_existing_spell_still_requires_charge_delivery(self):
        charged = next(r for r in self.items['piece/120']['dependencies'] if r['code'] == 'charged')
        self.assertEqual(charged['skills'][0]['talentId'], 'sorceress.teleport')
        self.assertTrue(charged['skills'][0]['implemented'])
        self.assertEqual(charged['prerequisites'], ['charged-skill casting, targeting, charge ledger and recharge'])

    def test_grouped_random_choices_and_set_partners_are_not_lost(self):
        bow = self.items['unique/62']['dependencies']
        self.assertEqual({s['name'] for d in bow for s in d['skills']}, {'Fire Arrow', 'Cold Arrow'})
        self.assertTrue(all(d['via'] == ['roguesbow-affix1'] for d in bow))
        self.assertTrue(self.data['heldSetPartners'])
        self.assertTrue(all(r['setId'] is not None for r in self.data['heldSetPartners']))
        self.assertTrue(any(s['name'] == "Trang-Oul's Avatar" for s in self.data['sets']))


if __name__ == '__main__':
    unittest.main()
