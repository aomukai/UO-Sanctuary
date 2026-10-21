#!/usr/bin/env python3
"""Extract the bounded difficulty-specific gear projection; retain unsupported probability mass."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re

from build_creature_gold import build as gold_catalogue

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'
COMMIT = '45112569deb9384738ccafe5c24ebbb71f41c7c9'


def build(source, difficulty="Normal"):
    pins = json.loads((BALANCE / 'sources.json').read_text())
    tables = {}
    for name in ['Weapons', 'Armor', 'Misc', 'ItemTypes', 'MonStats', 'TreasureClassEx', 'ItemRatio']:
        raw = (source / (name + '.txt')).read_bytes()
        assert hashlib.sha256(raw).hexdigest() == pins[name]['sha256'], name
        tables[name] = list(csv.DictReader(raw.decode('utf-8-sig').splitlines(), delimiter='\t'))
    gold = gold_catalogue(source, difficulty)
    assert gold == json.loads((BALANCE / ('creature-gold' + ('' if difficulty == 'Normal' else '-' + difficulty.lower()) + '.json')).read_text()), 'Stale shared treasure projection'
    classes = {r['Treasure Class']: r for r in tables['TreasureClassEx'] if r['Treasure Class']}
    for row in gold['classes']:
        # The current ordinary cohort has no quality factors or multi-pick
        # classes. Boss encounters need their own reviewed expansion.
        assert all(int(classes[row['name']][key] or 0) == 0 for key in ['Unique', 'Set', 'Rare', 'Magic']), row['name']
    types = {r['Code']: r for r in tables['ItemTypes'] if r['Code']}
    items = {r['code']: r for name in ['Weapons', 'Armor', 'Misc'] for r in tables[name] if r['code']}

    def is_type(code, ancestor, path=()):
        assert code not in path, code
        if code == ancestor:
            return True
        return any(is_type(parent, ancestor, (*path, code)) for parent in [types[code]['Equiv1'], types[code]['Equiv2']] if parent)

    definitions = json.loads((BALANCE / 'uo-equipment.json').read_text())
    scope = {r['type']: r for r in json.loads((BALANCE / 'equipment-crafting-scope.json').read_text())}
    allowed = {r['type']: r for r in definitions if r['normalDrop'] and r['allowsAffixes']
               and not (r['category'] == 'Clothing' and r['slot'] != 'Belt' and scope.get(r['type'], {}).get('material') != 'Leather')}
    variants = {}
    for definition in allowed.values():
        variants.setdefault(definition['baseCode'], {})[definition['type']] = definition['type']
    for recipe in json.loads((BALANCE / 'equipment-recipes.json').read_text()):
        if recipe['type'] in allowed and recipe['tier'] != 'Normal':
            variants.setdefault(recipe['baseCode'], {})[recipe['type']] = recipe['name']

    leaves = sorted({e['item'] for c in gold['classes'] for e in c['entries'] if not e['nested'] and not e['gold']})
    pools = []
    required = set(variants)
    for leaf in leaves:
        match = re.fullmatch(r'([a-z]+)(\d+)', leaf)
        if not match or match[1] not in types or types[match[1]]['TreasureClass'] != '1':
            if leaf in items:
                required.add(leaf)
            continue
        family, level = match[1], int(match[2])
        assert level in range(3, 97, 3), leaf
        entries = []
        for row in items.values():
            kind = row['type']
            if row['spawnable'] != '1' or int(row['quest'] or 0) != 0 or not is_type(kind, family):
                continue
            if family != 'tpot' and is_type(kind, 'tpot'):
                continue
            if level - 3 < int(row['level'] or 0) <= level:
                entries.append(dict(code=row['code'], weight=max(1, int(types[kind]['Rarity'] or 0))))
                required.add(row['code'])
        # Some synthesized source classes (for example bow15) are empty too.
        pools.append(dict(name=leaf, entries=entries))

    bases = []
    for code in sorted(required):
        assert code in items, code
        row = items[code]
        kind = types[row['type']]
        tier = 'Elite' if code == row.get('ultracode') else 'Exceptional' if code == row.get('ubercode') else 'Normal'
        choices = [dict(type=name, name=label) for name, label in sorted(variants.get(code, {}).items())]
        bases.append(dict(code=code, qualityLevel=int(row['level'] or 0), tier=tier,
                          classSpecific=bool(kind['Class']), alwaysMagic=kind['Magic'] == '1', rare=kind['Rare'] == '1',
                          variants=choices, excludedReason=None if choices else 'No current usable UO gear mapping; no reroll'))
    ratios = []
    for row in tables['ItemRatio']:
        if row['Version'] != '1':
            continue
        ratio = dict(uber=row['Uber'] == '1', classSpecific=row['Class Specific'] == '1')
        for quality in ['Unique', 'Set', 'Rare', 'Magic']:
            ratio[quality.lower()] = dict(value=int(row[quality]), divisor=int(row[quality + 'Divisor']), minimum=int(row[quality + 'Min']))
        ratios.append(ratio)
    assert len(ratios) == 4
    pool_names = {p['name'] for p in pools}
    supported = {b['code'] for b in bases if b['variants']}
    excluded_leaves = [dict(item=leaf, reason='T28 restorative potion supply; excluded from gear rolls' if re.fullmatch(r'[hm]p[1-5]', leaf) else 'Reserved non-gear system or unmapped base; no reroll')
                       for leaf in leaves if leaf not in pool_names and leaf not in supported]
    return dict(sourceCommit=COMMIT, difficulty=difficulty, players=1, maxDrops=1,
                setUniquePolicy='NamedWhenAvailable', whiteQuality='Normal',
                ratios=ratios, pools=pools, bases=bases, excludedLeaves=excluded_leaves)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-dir', required=True, type=Path)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--difficulty', choices=['Normal', 'Nightmare', 'Hell'], default='Normal')
    args = parser.parse_args()
    result = build(args.source_dir, args.difficulty)
    text = json.dumps(result, indent=2) + '\n'
    target = BALANCE / ('gear-drops' + ('' if args.difficulty == 'Normal' else '-' + args.difficulty.lower()) + '.json')
    if args.check:
        assert target.read_text() == text, 'Stale gear-drop extraction'
    else:
        target.write_text(text)
    print(f"Verified {len(result['pools'])} source base pools; {sum(bool(b['variants']) for b in result['bases'])} mapped bases; unsupported branches retained")


if __name__ == '__main__':
    main()
