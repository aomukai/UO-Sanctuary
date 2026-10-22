"""Guard the class reference, durable identities, separate asset work and reproducible catalogues."""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('class_skill_builder',ROOT/'tools/build_class_skills.py')
builder = importlib.util.module_from_spec(spec); spec.loader.exec_module(builder)


class ClassSkillCatalogueTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.talents,cls.skills,cls.assets = builder.compile_skills()

    def test_complete_roster_and_stable_existing_ids(self):
        self.assertEqual([len(c['trees']) for c in self.talents],[3]*8)
        self.assertEqual([len(t['skills']) for c in self.talents for t in c['trees']],[10]*24)
        self.assertEqual(len(self.skills),240)
        self.assertEqual(len({r['ability'] for r in self.skills}),240)
        self.assertLess(max(r['ability'] for r in self.skills),256)
        by_id={r['talentId']:r['ability'] for r in self.skills}
        self.assertEqual([by_id[t] for t in ['necromancer.teeth','necromancer.raiseskeleton','sorceress.teleport']],[2,3,4])

    def test_warlock_source_is_not_borrowed_from_another_game(self):
        rows=[r for r in self.skills if r['characterClass']=='Warlock']
        self.assertEqual({r['sourceId'] for r in rows},set(range(373,403)))
        self.assertEqual({r['sourceTab'] for r in rows},{21,22,23})
        self.assertEqual({r['tree'] for r in rows},{'Demon','Eldritch','Chaos'})
        self.assertTrue(all(r['sourceFields']['charclass']=='war' for r in rows))
        self.assertEqual(builder.warlock_stats()['life'],55)
        self.assertEqual(builder.warlock_stats()['manaEnergy4'],8)

    def test_prerequisites_and_legacy_talent_ids_stay_with_their_class(self):
        for character in self.talents:
            talents={s['id']:s for t in character['trees'] for s in t['skills']}
            for skill in talents.values():
                for prerequisite in skill['prerequisites']:
                    self.assertIn(prerequisite,talents)
                    self.assertLessEqual(talents[prerequisite]['requiredLevel'],skill['requiredLevel'])
        self.assertIn('necromancer.skeletalmage',{r['talentId'] for r in self.skills})
        self.assertIn('barbarian.swordmastery',{r['talentId'] for r in self.skills})

    def test_generated_products_are_current(self):
        for path,text in builder.products().items():
            self.assertEqual(path.read_text(),text,str(path.relative_to(ROOT)))

    def test_icons_and_effects_are_independent_and_explicitly_unfinished(self):
        icons=json.loads((ROOT/'data/Sanctuary/Balance/skill-icons.json').read_text())
        effects=json.loads((ROOT/'data/Sanctuary/Balance/skill-effects.json').read_text())
        self.assertEqual({r['talentId'] for r in icons},{r['talentId'] for r in effects})
        self.assertEqual(len(icons),240)
        self.assertTrue(all('effects' not in r and 'talentIcon' in r for r in icons))
        self.assertTrue(all('talentIcon' not in r and 'effects' in r for r in effects))
        self.assertTrue(all(r['generation']=='pending' and r['clientMapping']=='pending' for r in icons+effects))
        by_id={r['talentId']:r for r in icons}
        self.assertEqual(by_id['necromancer.skeletonmastery']['hotkeyIcon'],'not-applicable')
        self.assertEqual(by_id['necromancer.bonespear']['hotkeyIcon'],'needed')

    def test_version_and_hashes_are_pinned_with_sanctuary_precedence(self):
        pin=json.loads((ROOT/'data/Sanctuary/skill-reference.json').read_text())
        self.assertEqual(pin['version'],'3.3.93847')
        self.assertEqual(pin['sourceCommit'],'fa1286949266498b294c40c2942521a7c0d7207a')
        self.assertTrue(all(not r['differences'] for r in pin['comparison'].values()))
        self.assertIn('Sanctuary decisions override',pin['precedence'])
        manifest=json.loads((builder.SOURCE/'sources.json').read_text())
        for rel,entry in manifest['files'].items():
            self.assertEqual(hashlib.sha256((builder.SOURCE/rel).read_bytes()).hexdigest(),entry['sha256'])


if __name__=='__main__':unittest.main()
