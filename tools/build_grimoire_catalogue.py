#!/usr/bin/env python3
"""Compile owned Grimoire bases/automatic properties; no item roll generator."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.local/class-skills/warlock-hands-reference'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    extraction = json.loads((SOURCE / 'grimoire-extraction.json').read_text())
    installation = json.loads((ROOT / 'tools/reference/d2r-local-installation-evidence.json').read_text())
    assert extraction['referenceVersion'] == installation['referenceVersion'] == '3.3.93847'
    assert extraction['executableSha256'] == installation['executable']['sha256']
    assert extraction['buildInfoSha256'] == installation['buildInfo']['sha256']
    assert not extraction['gameFilesWritten'] and not extraction['processMemoryRead']
    inputs = {str((SOURCE / 'grimoire-extraction.json').relative_to(ROOT)): sha(SOURCE / 'grimoire-extraction.json')}

    def table(name):
        path = SOURCE / name
        assert sha(path) == extraction['files'][name]['sha256']
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return list(csv.DictReader(path.read_text().splitlines(), delimiter='\t'))

    bases = []
    for row in table('armor.txt'):
        if row.get('type') != 'grim':
            continue
        assert row['auto prefix'] == '306' and row['reqdex'] == '0' and row['spawnable'] == '1'
        tier = 'Normal' if row['code'] == row['normcode'] else 'Exceptional' if row['code'] == row['ubercode'] else 'Elite'
        bases.append(dict(code=row['code'], name=row['name'], tier=tier, normal=row['normcode'],
                          qualityLevel=int(row['level']), requiredLevel=int(row['levelreq'] or 0),
                          strength=int(row['reqstr']), minimumDefense=int(row['minac']), maximumDefense=int(row['maxac']),
                          block=int(row['block']), durability=int(row['durability']), maximumSockets=int(row['gemsockets'])))
    assert len(bases) == 15 and [row['tier'] for row in bases] == ['Normal'] * 5 + ['Exceptional'] * 5 + ['Elite'] * 5
    automatic = []
    for row in table('automagic.txt'):
        if row.get('group') != '306':
            continue
        assert row['version'] == '100' and row['spawnable'] == '1' and not row['maxlevel'] and not row['mod3code']
        assert row['itype1'] == 'armo' and row['itype2'] == 'weap'
        magic = row['mod1code'] == 'dmg-mag'
        if magic:
            assert not row['mod2code']
        else:
            assert row['mod1code'] == 'fire-min' and row['mod2code'] == 'fire-max'
            assert row['mod1min'] == row['mod1max'] and row['mod2min'] == row['mod2max']
        automatic.append(dict(name=row['Name'], affixLevel=int(row['level']), requiredLevel=int(row['levelreq']),
                              rare=row['rare'] == '1', frequency=int(row['frequency']),
                              magicMinimum=int(row['mod1min']) if magic else 0,
                              magicMaximum=int(row['mod1max']) if magic else 0,
                              fireMinimum=0 if magic else int(row['mod1min']),
                              fireMaximum=0 if magic else int(row['mod2min'])))
    assert len(automatic) == 8
    outputs = {}
    for name, rows in (('warlock-grimoire-bases', bases), ('warlock-grimoire-automatic', automatic)):
        path = ROOT / 'data/Sanctuary/Balance' / (name + '.json')
        encoded = json.dumps(rows, indent=2) + '\n'
        if check:
            assert path.read_text() == encoded
        else:
            path.write_text(encoded)
        outputs[str(path.relative_to(ROOT))] = sha(path)
    result = dict(referenceVersion='3.3.93847', component='Owned Grimoire runtime catalogue',
                  bases=15, normalFamilies=5, automaticRows=8, inputs=inputs, outputs=outputs,
                  limitations=['No random generation,affix selection,staffmod selection,equip or acquisition is certified by these data rows. Blank third-property codes contribute no effect despite nonempty unused min/max cells.'])
    path = ROOT / 'tools/reference/grimoire-catalogue-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Owned Grimoire catalogue:15bases/five families/eight automatic rows;no generation or equip behavior claimed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
