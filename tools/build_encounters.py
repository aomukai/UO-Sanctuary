#!/usr/bin/env python3
"""Project the pinned LoD encounter treasure trees without changing ordinary loot."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMMIT = '45112569deb9384738ccafe5c24ebbb71f41c7c9'
FAMILIES = [('crypt-sentinel', 'Crypt Sentinel', 'Melee', 'skeleton1', 50, 0x48D),
            ('bog-revenant', 'Bog Revenant', 'Tank', 'zombie1', 3, 471),
            ('ossuary-archer', 'Ossuary Archer', 'Ranged', 'sk_archer1', 50, 0x48D),
            ('cinder-hierophant', 'Cinder Hierophant', 'Caster', 'fallenshaman1', 24, 0x3E9)]
CAMPAIGN_BOSSES = {
    'campaign-skeleton-king': 'Diablo',
    'campaign-lazarus': 'Mephisto (N)',
    'campaign-diablo': 'Diablo (H)',
}
HYTHLOTH_MINIBOSSES = {
    'herald': {'mxb': 30, 'hxb': 25, 'cbw': 25, 'chn': 10, 'amu': 10},
    'jester': {'cst': 25, 'wst': 25, 'wnd': 20, 'rin': 15, 'amu': 15},
    'chamberlain': {'mac': 20, 'mst': 25, 'mau': 20, 'kit': 20, 'tow': 15},
    'executioner': {'gix': 40, 'btx': 20, 'gax': 20, 'plt': 20},
}

def read(path):
    with path.open(encoding='utf-8-sig') as stream:
        return list(csv.DictReader(stream, delimiter='\t'))

def build(source):
    rows = read(source / 'TreasureClassEx.txt')
    classes = {r['Treasure Class']: r for r in rows if r['Treasure Class'] and r['Treasure Class'] != 'Expansion'}
    monsters = {r['Id']: r for r in read(source / 'MonStats.txt')}
    pools, equipment = set(), set()
    for suffix in ['', '-nightmare', '-hell']:
        catalog = json.loads((ROOT / f'data/Sanctuary/Balance/gear-drops{suffix}.json').read_text())
        pools.update(r['name'] for r in catalog['pools'])
        equipment.update(r['code'] for r in catalog['bases'] if r['variants'])
    roots, pending = [], list(CAMPAIGN_BOSSES.values())
    for family, _, _, ref, _, _ in FAMILIES:
        for diff, suffix in [('Normal', ''), ('Nightmare', '(N)'), ('Hell', '(H)')]:
            for grade, column in [('Ordinary', 1), ('Champion', 2), ('Unique', 3)]:
                name = monsters[ref][f'TreasureClass{column}{suffix}']
                roots.append(dict(family=family, difficulty=diff, grade=grade, name=name))
                pending.append(name)
                group = classes[name]['group']
                if diff != 'Normal' and group:
                    pending.extend(r['Treasure Class'] for r in rows if r['group'] == group)
    result = {}
    while pending:
        name = pending.pop()
        if name in result or name in pools or name not in classes:
            continue
        row = classes[name]
        entries = []
        for i in range(1, 11):
            item, weight = row[f'Item{i}'], int(row[f'Prob{i}'] or 0)
            if not item or weight <= 0:
                continue
            parts = item.split(',')
            code = parts[0]
            multiplier = next((int(p[4:]) for p in parts[1:] if p.startswith('mul=')), 256)
            entries.append(dict(item=code, weight=weight, goldMultiplier=multiplier))
            pending.append(code)
        result[name] = dict(name=name, group=int(row['group'] or 0), level=int(row['level'] or 0),
                            picks=int(row['Picks'] or 0), noDrop=int(row['NoDrop'] or 0),
                            factors=[int(row[k] or 0) for k in ['Unique', 'Set', 'Rare', 'Magic']], entries=entries)
    miniboss_roots = {}
    quality = result['Act 2 Uitem B']['factors']
    potion_pool = 'Hythloth miniboss potions'
    source_potions = result['Hpotion 2']
    result[potion_pool] = dict(source_potions, name=potion_pool, picks=2,
                               entries=[entry for entry in source_potions['entries']
                                        if entry['item'].startswith(('hp', 'mp'))])
    for role, weights in HYTHLOTH_MINIBOSSES.items():
        if not weights.keys() <= equipment:
            raise ValueError(f'Unsupported equipment for Hythloth {role}')
        name = f'Hythloth {role}'
        miniboss_roots[f'hythloth-{role}'] = name
        gear = f'{name} equipment'
        result[name] = dict(name=name, group=0, level=0, picks=-4, noDrop=0, factors=[0]*4, entries=[
            dict(item=gear, weight=2, goldMultiplier=256),
            dict(item=potion_pool, weight=2, goldMultiplier=256),
        ])
        result[gear] = dict(name=gear, group=0, level=0, picks=1, noDrop=0, factors=quality, entries=[
            dict(item=code, weight=weight, goldMultiplier=256) for code, weight in weights.items()
        ])
    provenance = {name: hashlib.sha256((source/name).read_bytes()).hexdigest() for name in ['MonStats.txt', 'TreasureClassEx.txt']}
    treasure = dict(sourceCommit=COMMIT, sourceSha256=provenance, maxDrops=6, setUniquePolicy='NamedWhenAvailable',
                    roots=roots, campaignBossRoots=CAMPAIGN_BOSSES, hythlothMinibossRoots=miniboss_roots,
                    classes=sorted(result.values(), key=lambda r:r['name']))
    profiles = []
    for family, name, archetype, ref, body, sound in FAMILIES:
        for index, (lo, hi) in enumerate([(60,69), (70,79), (80,89), (90,99)]):
            # Regional resist themes are authored; the life/offense curve remains the pinned players-1 curve.
            resist = {'Physical': 0, 'Magic': 0, 'Fire': 0, 'Lightning': 0, 'Cold': 0, 'Poison': 50}
            if family == 'cinder-hierophant': resist.update(Fire=40 + index*20, Poison=0)
            if family == 'bog-revenant': resist.update(Physical=index*10, Poison=70 + index*10)
            if family == 'ossuary-archer': resist.update(Cold=20 + index*20)
            profiles.append(dict(id=f'{family}-{lo}', family=family, name=name, archetype=archetype, reference=ref,
                                 minimumLevel=lo, maximumLevel=hi, body=body, sound=sound, resistances=resist))
    named = [dict(id='ossuary-warden', name='The Ossuary Warden', profile='crypt-sentinel-90', baseLevel=94,
                  modifiers=['ExtraStrong', 'StoneSkin', 'Cursed'], aura='None'),
             dict(id='carrion-matriarch', name='The Carrion Matriarch', profile='bog-revenant-90', baseLevel=95,
                  modifiers=['ColdEnchanted', 'AuraEnchanted', 'ManaBurn'], aura='HolyFreeze'),
             dict(id='ashen-hierophant', name='The Ashen Hierophant', profile='cinder-hierophant-90', baseLevel=96,
                  modifiers=['FireEnchanted', 'Multishot', 'Teleport'], aura='None')]
    config = dict(schemaVersion=1, profiles=profiles, named=named, teleportCooldownMs=8000, teleportRadius=6,
                  teleportHealPercent=25, auraPulseMs=2000, maximumAddedImmunities=2)
    return {'encounter-treasure.json': treasure, 'encounters.json': config}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-dir', type=Path, default=ROOT/'.local/research/foundation-pinned')
    parser.add_argument('--check', action='store_true')
    args=parser.parse_args()
    for name, data in build(args.source_dir).items():
        path=ROOT/'data/Sanctuary/Balance'/name
        text=json.dumps(data, ensure_ascii=False, indent=2)+'\n'
        if args.check:
            if not path.exists() or path.read_text() != text: raise SystemExit(f'Out of date: {path}')
        else: path.write_text(text)
    print('Encounter profiles and pinned treasure projection verified' if args.check else 'Wrote encounter profiles and treasure projection')
if __name__ == '__main__': main()
