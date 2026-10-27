#!/usr/bin/env python3
"""Generate weapon animation classes for all pinned bases, including unnamed ones."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def build(check=False):
    source = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((source / 'sources.json').read_text())
    path = source / 'json/weapons.json'
    assert hashlib.sha256(path.read_bytes()).hexdigest() == manifest['files']['json/weapons.json']['sha256']
    rows = []
    for row in json.loads(path.read_text()).values():
        if not row.get('code') or not row.get('wclass'):
            continue
        rows.append(dict(code=row['code'], oneHanded=row['wclass'].upper(), twoHanded=(row.get('2handedwclass') or row['wclass']).upper()))
    rows.sort(key=lambda row: row['code'])
    assert len({row['code'] for row in rows}) == len(rows)
    assert next(row for row in rows if row['code'] == 'glv') == dict(code='glv', oneHanded='1HT', twoHanded='1HT')
    output = ROOT / 'data/Sanctuary/Balance/weapon-animation-classes.json'
    text = json.dumps(rows, indent=2) + '\n'
    if check:
        assert output.read_text() == text
    else:
        output.write_text(text)
    print(f'Weapon animation classes:{len(rows)}pinned base identities,including ordinary throwing weapons; no stats or equipment gates changed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
