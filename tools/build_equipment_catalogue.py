#!/usr/bin/env python3
"""Reproduce the authored equipment tier ladder from pinned local reference tables."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'


def build(source):
    pins = json.loads((BALANCE / 'sources.json').read_text())
    tables = {}
    for name in ('Weapons', 'Armor'):
        path = source / (name + '.txt')
        if hashlib.sha256(path.read_bytes()).hexdigest() != pins[name]['sha256']:
            raise ValueError('Pinned source hash differs: ' + name)
        tables.update({r['code']: r for r in csv.DictReader(path.open(encoding='utf-8-sig'), delimiter='\t') if r['code']})
    authored = json.loads((BALANCE / 'equipment-recipes.json').read_text())
    normals = [r for r in authored if r['tier'] == 'Normal']
    recipes = []
    codes = {}
    for normal in normals:
        source_row = tables[normal['baseCode']]
        n = normal['requiredSkill']
        if not 0 <= n <= 80:
            raise ValueError('Normal skill outside band: ' + normal['id'])
        window = normal['maxSkill'] - n
        for i, tier in enumerate(('Normal', 'Exceptional', 'Elite')):
            code = [normal['baseCode'], source_row['ubercode'], source_row['ultracode']][i]
            skill = [n, 80 + math.ceil(30 * n / 80), 110 + math.ceil(10 * n / 80)][i]
            recipes.append(dict(normal, id=normal['type'] + '-' + tier, tier=tier, baseCode=code,
                                requiredSkill=skill, maxSkill=round(skill + window, 4),
                                ingredients=[dict(v, amount=v['amount'] * (i + 1)) for v in normal['ingredients']]))
            if i:
                if code not in tables or tables[code]['ubercode'] != source_row['ubercode'] or tables[code]['ultracode'] != source_row['ultracode']:
                    raise ValueError('Broken source tier links: ' + code)
                codes[code] = (tier, tables[code])
    bases = []
    for code, (tier, r) in sorted(codes.items()):
        def number(key):
            return int(r.get(key) or 0)
        bases.append(dict(code=code, name=r['name'], type=r['type'], qualityLevel=number('level'),
                          magicLevel=number('magic lvl'), requiredLevel=number('levelreq'),
                          strength=number('reqstr'), dexterity=number('reqdex'), minDamage=number('mindam'),
                          maxDamage=number('maxdam'), twoHandMin=number('2handmindam'), twoHandMax=number('2handmaxdam'),
                          missileMin=number('minmisdam'), missileMax=number('maxmisdam'), minDefense=number('minac'),
                          maxDefense=number('maxac'), speed=number('speed'), block=number('block'),
                          strengthBonus=number('StrBonus'), dexterityBonus=number('DexBonus'),
                          twoHanded=r.get('2handed') == '1', oneOrTwoHanded=r.get('1or2handed') == '1',
                          maxSockets=None, tier=tier))
    return recipes, bases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-dir', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    recipes, bases = build(args.source_dir)
    for name, expected in [('equipment-recipes', recipes), ('equipment-tier-bases', bases)]:
        path = BALANCE / (name + '.json')
        if args.check:
            if json.loads(path.read_text()) != expected:
                raise ValueError('Catalogue differs: ' + name)
        else:
            path.write_text(json.dumps(expected, indent=2) + '\n')
    print(f'{"Checked" if args.check else "Wrote"} {len(recipes)} recipes and {len(bases)} linked higher bases; original Normal catalogue untouched.')


if __name__ == '__main__':
    main()
