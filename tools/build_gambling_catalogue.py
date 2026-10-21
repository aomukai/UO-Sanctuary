"""Author T45 costs and tavern anchors from the project's pinned source inputs."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def build(source):
    pins = json.loads((ROOT / "data/Sanctuary/Balance/sources.json").read_text())
    prices = []
    source_rows = {}
    for name in ['Weapons', 'Armor', 'Misc']:
        assert hashlib.sha256((source / (name + '.txt')).read_bytes()).hexdigest() == pins[name]['sha256'], name
        with (source / (name + '.txt')).open(encoding='utf-8-sig', newline='') as f:
            for row in csv.DictReader(f, delimiter='\t'):
                if not row.get('code') or not row.get('level'):
                    continue
                source_rows[row['code']] = row
                integer = lambda k: int(row.get(k) or 0)
                prices.append(dict(code=row['code'], level=integer('level'), cost=integer('cost'),
                                   gambleCost=integer('gamble cost'), normal=row.get('normcode') or row['code'],
                                   exceptional=row.get('ubercode') or None, elite=row.get('ultracode') or None,
                                   stack=((integer('minstack') + integer('maxstack')) // 2) or 1))
    sites = []
    folder = ROOT / 'upstream/ModernUO/Distribution/Data'
    for path in sorted((folder / 'Spawns').rglob('*.json')):
        rows = json.loads(path.read_text())
        if not isinstance(rows, list):
            continue
        for row in rows:
            if not any(e.get('name', '').lower() == 'tavernkeeper' for e in row.get('entries', [])):
                continue
            facet = row['map']
            x, y, z = row['location']
            if any(s['map'] == facet and abs(s['x'] - x) <= 12 and abs(s['y'] - y) <= 12
                   and abs(s['z'] - z) <= 5 for s in sites):
                continue
            sites.append(dict(id=f"{facet}/{x}/{y}/{z}", map=facet, x=x, y=y, z=z,
                              source=f"{path.relative_to(folder)}#{row['guid']}"))
    balance = ROOT / 'data/Sanctuary/Balance'
    known = {r['code'] for name in ['normal-bases', 'equipment-tier-bases']
             for r in json.loads((balance / (name + '.json')).read_text())}
    codes = {g['baseCode'] for g in json.loads((balance / 'uo-equipment.json').read_text())
             if g['normalDrop'] and g['allowsAffixes']}
    fields = dict(code='code', name='name', type='type', qualityLevel='level', magicLevel='magic lvl',
                  requiredLevel='levelreq', strength='reqstr', dexterity='reqdex', minDamage='mindam', maxDamage='maxdam',
                  twoHandMin='2handmindam', twoHandMax='2handmaxdam', missileMin='minmisdam', missileMax='maxmisdam',
                  minDefense='minac', maxDefense='maxac', speed='speed', block='block', strengthBonus='StrBonus', dexterityBonus='DexBonus')
    extra = {}
    for code in sorted(codes):
        for tier, key in [('Exceptional', 'ubercode'), ('Elite', 'ultracode')]:
            target = source_rows[code].get(key)
            if not target or target in known or target == '0':
                continue
            row = source_rows[target]
            extra[target] = {field: row.get(source) if field in ['code', 'name', 'type'] else int(row.get(source) or 0)
                             for field, source in fields.items()}
            extra[target].update(twoHanded=row.get('2handed') == '1', oneOrTwoHanded=row.get('1or2handed') == '1',
                                 maxSockets=None, tier=tier)
    return {'gambling-costs': sorted(prices, key=lambda p: p['code']), 'tavern-gamblers': sites,
            'gambling-tier-bases': [extra[k] for k in sorted(extra)]}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source-dir', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    for name, rows in build(args.source_dir).items():
        path = ROOT / 'data/Sanctuary/Balance' / (name + '.json')
        text = json.dumps(rows, indent=2) + '\n'
        if args.check:
            assert path.read_text() == text, f'{name} differs from source inputs'
        else:
            path.write_text(text)
        print(name, len(rows))
