"""Complete pinned named-item definitions, source fidelity and special cases."""
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import build_named_equipment as compiler


class NamedEquipmentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.compiled = compiler.build()
        cls.manifest, cls.source = compiler.read_sources(compiler.SOURCE)
        cls.items = cls.compiled['unique-items'] + cls.compiled['set-items']

    def test_every_source_row_has_a_distinct_lossless_definition(self):
        for table, output in [('uniqueitems', 'unique-items'), ('setitems', 'set-items'), ('sets', 'equipment-sets')]:
            source = self.source['json/' + table]
            generated = self.compiled[output]
            self.assertEqual(len(generated), len(source))
            self.assertEqual({json.dumps(row, sort_keys=True) for row in source.values()},
                             {json.dumps(row['sourceFields'], sort_keys=True) for row in generated})
            self.assertEqual(len(generated), len({row['id'] for row in generated}))
        counts = self.compiled['named-item-coverage']['counts']
        self.assertEqual((counts['uniqueRows'], counts['setPieces'], counts['sets']), (433, 140, 35))
        self.assertEqual((counts['spawnableSetPieces'], counts['sourceAvailableSets']), (135, 34))

    def test_all_property_slots_and_arguments_survive_without_rolling_proc_fields(self):
        for item in self.items:
            source = item['sourceFields']
            slots = {key[4:] for key in source if re.fullmatch(r'prop\d+', key)}
            self.assertEqual(slots, {p['slot'] for p in item['properties']}, item['id'])
            for prop in item['properties']:
                n = prop['slot']
                self.assertEqual(prop['sourceCode'], source['prop' + n])
                self.assertEqual(prop['parameter'], source.get('par' + n))
                self.assertEqual(prop['sourceMin'], source.get('min' + n))
                self.assertEqual(prop['sourceMax'], source.get('max' + n))
                self.assertEqual(prop['sourceEnabled'], not source['prop' + n].startswith('*'))
        facets = [r for r in self.items if r['sourceKey'] == 'Rainbow Facet']
        self.assertEqual(len(facets), 8)
        self.assertEqual(len({r['id'] for r in facets}), 8)
        self.assertTrue(any(p['code'] in ('death-skill', 'levelup-skill') for r in facets for p in r['properties']))
        disabled = [(r['id'], p['sourceCode']) for r in self.items for p in r['properties'] if not p['sourceEnabled']]
        self.assertEqual(len(disabled), 4)

    def test_membership_partial_full_and_specific_piece_bonuses(self):
        pieces = {r['id']: r for r in self.compiled['set-items']}
        for group in self.compiled['equipment-sets']:
            expected = [r['id'] for r in self.compiled['set-items'] if r['setId'] == group['id']]
            self.assertEqual(expected, group['pieceIds'])
            raw = group['sourceFields']
            self.assertEqual(len(group['partialBonuses']), sum(bool(v) for k, v in raw.items() if re.fullmatch(r'PCode\d+[ab]', k)))
            self.assertEqual(len(group['fullBonuses']), sum(bool(v) for k, v in raw.items() if re.fullmatch(r'FCode\d+', k)))
        ward = pieces['piece/0']
        self.assertEqual([(p['code'], p['requiredPieceId']) for p in ward['additionalProperties']],
                         [('mana', 'piece/1'), ('res-pois', 'piece/2')])
        tal = next(g for g in self.compiled['equipment-sets'] if g['sourceKey'] == "Tal Rasha's Wrappings")
        mf = next(p for p in tal['partialBonuses'] if p['code'] == 'mag%')
        self.assertEqual((mf['requiredPieces'], mf['sourceMin'], mf['sourceMax']), (3, 65, 65))
        self.assertEqual(next(p['sourceMin'] for p in tal['fullBonuses'] if p['code'] == 'sor'), 3)

    def test_availability_localization_and_source_inconsistency_are_explicit(self):
        uniques = {r['id']: r for r in self.compiled['unique-items']}
        self.assertEqual(uniques['unique/29']['sourceAvailability'], 'DisabledSourceRow')
        self.assertEqual(uniques['unique/263']['sourceAvailability'], 'DisabledSourceRow')
        self.assertEqual(uniques['unique/295']['sourceAvailability'], 'IncompleteSourceRow')
        self.assertEqual(uniques['unique/427']['sourceAvailability'], 'SourceRecipe')
        self.assertEqual(uniques['unique/427']['name'], 'Renewed Cold Rupture')
        self.assertFalse(uniques['unique/427']['sourceSpawnable'])
        self.assertTrue(uniques['unique/427']['recipes'])
        guard = next(p for p in self.compiled['set-items'] if p['sourceId'] == 107)
        self.assertEqual((guard['sourceKey'], guard['name']), ("Wihtstan's Guard", "Whitstan's Guard"))
        coverage = self.compiled['named-item-coverage']
        self.assertEqual(coverage['activation'], 'RuntimeGated')
        self.assertEqual(coverage['unresolvedSourceProperties'], ['Gelid-Affix5'])
        missing = next(p for p in self.compiled['named-item-properties'] if p['code'] == 'Gelid-Affix5')
        self.assertEqual((missing['kind'], missing['sourceFields']), ('MissingSourceDefinition', {}))

    def test_every_base_and_recursive_property_dependency_is_accounted_for(self):
        bases = {b['code']: b for b in self.compiled['named-item-bases']}
        properties = {p['code']: p for p in self.compiled['named-item-properties']}
        for item in self.items:
            if item['baseCode']:
                self.assertIn(item['baseCode'], bases)
            for prop in item['properties'] + item['additionalProperties']:
                self.assertIn(prop['code'], properties)
        visiting, visited = set(), set()
        def visit(code):
            self.assertNotIn(code, visiting, 'Cyclic source property group')
            if code in visited:
                return
            visiting.add(code)
            definition = properties[code]
            if definition['kind'] == 'PropertyGroup':
                for key, child in definition['sourceFields'].items():
                    if re.fullmatch(r'Prop\d+', key) and child:
                        self.assertIn(child, properties)
                        visit(child)
            visiting.remove(code)
            visited.add(code)
        for code in properties:
            visit(code)
        coverage = self.compiled['named-item-coverage']
        self.assertEqual(coverage['missingProjectBases'], [b['code'] for b in bases.values() if not b['projectBasePresent']])

    def test_exact_offline_reproduction_including_readable_catalogue(self):
        for name, rows in self.compiled.items():
            expected = json.dumps(rows, indent=2, ensure_ascii=False) + '\n'
            self.assertEqual((compiler.BALANCE / (name + '.json')).read_text(), expected, name)
        self.assertEqual(compiler.REPORT.read_text(), compiler.render_report(self.compiled))

    def test_source_tampering_is_rejected_before_projection(self):
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / 'source'
            shutil.copytree(compiler.SOURCE, destination)
            (destination / 'json/uniqueitems.json').write_text('{}')
            with self.assertRaisesRegex(ValueError, 'Pinned source differs: json/uniqueitems.json'):
                compiler.build(destination)


if __name__ == '__main__':
    unittest.main()
