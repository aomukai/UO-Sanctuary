#!/usr/bin/env python3
"""Pin all source NPC melee footprints/reach against owned 3.3.93847 tables."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def build(check=False):
    directory = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((directory / 'sources.json').read_text())
    assert manifest['mechanicsReferenceVersion'] == '3.3.93847'
    observations = json.loads((ROOT / 'tools/reference/druid-beasts-user-observations.json').read_text())
    owned = {}
    inputs = {}
    for name in ('monstats', 'monstats2'):
        source = directory / 'json' / (name + '.json')
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        assert digest == manifest['files']['json/' + name + '.json']['sha256']
        path = ROOT / '.local/research/d2r-3.3.93847' / (name + '-sentries-verified.txt')
        assert hashlib.sha256(path.read_bytes()).hexdigest() == observations['localTableChecks']['files'][str(path.relative_to(ROOT))]['sha256']
        with path.open(encoding='utf-8-sig', newline='') as stream:
            rows = list(csv.DictReader(stream, delimiter='\t'))
        key = 'Id'
        owned[name] = {row[key]: row for row in rows if row.get(key) and row[key] != 'Expansion'}
        inputs[name] = dict(jsonPath=str(source.relative_to(ROOT)), jsonSha256=digest,
                            ownedPath=str(path.relative_to(ROOT)), ownedSha256=hashlib.sha256(path.read_bytes()).hexdigest())
    monsters = json.loads((directory / 'json/monstats.json').read_text())
    models = json.loads((directory / 'json/monstats2.json').read_text())
    result = []
    for name, row in monsters.items():
        original = owned['monstats'][name]
        assert int(original['*hcIdx']) == row['*hcIdx'] and original['MonStatsEx'] == row['MonStatsEx'], name
        model = models[row['MonStatsEx']]
        original_model = owned['monstats2'][row['MonStatsEx']]
        values = {field: model.get(field, 0) for field in ('SizeX', 'SizeY', 'MeleeRng')}
        assert all(int(original_model[field] or 0) == value for field, value in values.items()), name
        assert values['SizeX'] == values['SizeY'] and all(0 <= value <= 255 for value in values.values()), name
        result.append(dict(Key=name, BaseId=row['*hcIdx'], Size=values['SizeX'], RawRange=values['MeleeRng']))
    assert len(result) == 751 and len({row['BaseId'] for row in result}) == 751
    target = ROOT / 'data/Sanctuary/Balance/revive-melee-geometry.json'
    encoded = json.dumps(result, indent=2) + '\n'
    receipt = dict(referenceVersion='3.3.93847', inputs=inputs, runtimePath=str(target.relative_to(ROOT)),
                   runtimeSha256=hashlib.sha256(encoded.encode()).hexdigest(), comparedRows=751,
                   comparedFields=['hcIdx', 'MonStatsEx', 'SizeX', 'SizeY', 'MeleeRng'],
                   scope='Owned table parity only; shared source geometry algorithm and explicit native ray bridge are separate evidence.')
    for path, content in ((target, encoded), (ROOT / 'tools/reference/revive-melee-geometry-source-evidence.json', json.dumps(receipt, indent=2) + '\n')):
        if check:
            assert path.read_text() == content
        else:
            path.write_text(content)
    print('Revive melee geometry:751 source IDs/model links/square footprints/raw ranges match owned 3.3.93847.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
