"""Protect the agreed five bands and real named Britannian dungeon catalogue."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT/'data/Sanctuary/Balance'


class MapSurveyTests(unittest.TestCase):
    def test_agreed_progression_and_effects(self):
        rows = json.loads((BALANCE/'map-surveys.json').read_text())
        self.assertEqual([(r['id'], r['skill'], r['maximum'], r['effect'], r['magnitude']) for r in rows], [
            ('city',0,30,'city',5), ('world',30,60,'world',5), ('dungeon-1',60,80,'dungeon',10),
            ('dungeon-2',80,100,'dungeon',20), ('dungeon-3',100,120,'dungeon',30)])
        supplies = json.loads((BALANCE/'market-supplies.json').read_text())
        self.assertTrue(any(r['Type']=='BlankMap' and 'Cartographer' in r['Roles'] for r in supplies))

    def test_dungeons_are_named_native_regions_not_generic_outdoor_areas(self):
        native = json.loads((ROOT/'upstream/ModernUO/Distribution/Data/regions.json').read_text())
        expected = {(r['Map'],r['Name']) for r in native if r.get('Map') in ('Felucca','Trammel')
                    and r.get('$type')=='DungeonRegion' and r['Name']!='Misc Dungeons'}
        rows = json.loads((BALANCE/'survey-dungeons.json').read_text())
        actual = {(r['map'],r['name']) for r in rows}
        self.assertEqual(actual, expected)
        self.assertEqual(len(rows),len(actual))
        self.assertTrue({('Trammel','Deceit'),('Trammel','Ice'),('Felucca','Shame')} <= actual)


if __name__ == '__main__':
    unittest.main()
