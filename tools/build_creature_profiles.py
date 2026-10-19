#!/usr/bin/env python3
"""Build the common NPC schema/catalog, preserving per-type hand-authored overrides.

Inventory is exported by CreatureInventory from the built UOContent assembly, without
constructing mobiles. D2 tables/animation metadata are the pinned local research inputs.
"""
import argparse
import csv
import hashlib
import json
import re
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'data/Sanctuary/Balance'


def build(inventory, source, animations):
    def table(name):
        return list(csv.DictReader((source / f'{name}.txt').open(encoding='utf-8-sig'), delimiter='\t'))

    def save(name, data):
        (OUT / f'{name}.json').write_text(json.dumps(data, indent=2) + '\n')

    monsters = {r['Id']: r for r in table('MonStats') if r['Id']}
    extra = {r['Id']: r for r in table('MonStats2') if r['Id']}
    levels = {int(r['Level']): r for r in table('MonLvl') if r['Level'].isdigit()}
    anim, p, data = {}, 0, animations.read_bytes()
    while p < len(data):
        count, = struct.unpack_from('<I', data, p)
        p += 4
        for _ in range(count):
            key, frames, speed = struct.unpack_from('<8sII', data, p)
            anim[key.rstrip(b'\0').decode()] = dict(frames=frames, speed=speed)
            p += 160

    def number(row, key):
        return int(row.get(key) or 0)

    attack = dict.fromkeys(['minDamage', 'maxDamage', 'attackRating', 'range', 'cycleMs', 'impactMs',
                           'afterAttackMs', 'physical', 'fire', 'lightning', 'cold', 'poison', 'magic',
                           'criticalChance', 'skill', 'skillLevel', 'missile'])
    native = dict(enabled=True, applyStats=False, level=None, life=None, defense=None,
                  resistances=dict.fromkeys(['physical', 'magic', 'fire', 'lightning', 'cold', 'poison']),
                  attack1=attack, attack2=attack, spell=attack,
                  hitRecovery=dict(enabled=True, frames=4, speed=256, fhr=0),
                  movement=dict(walkMs=None, runMs=None, idleMs=None),
                  reaction=dict(behavior='Native', noticeMs=None, scanMs=None, approachChance=None,
                                awarenessRange=None, retryMs=None, wander=None, attackChance=None,
                                attack1Chance=None, aiDelayMs=None),
                  rewards=dict(enabled=False, level=None, experience=None, treasureClass=None,
                               reference=None, rationale=None),
                  sourceStats={}, sourceModes={}, targeting=dict(policy='Native', threat=None, retainValidTarget=True),
                  traits=dict(family='Native', undead=None, demon=None, boss=None, revivable=None,
                              leavesCorpse=None, size=None, regeneration=None, drainEffectiveness=None,
                              chillEffectiveness=None, blockChance=None))
    templates = dict(defaults=dict(Normal=native, Nightmare=dict(enabled=False, applyStats=False),
                                  Hell=dict(enabled=False, applyStats=False)), native=dict(Normal={}),
                     vendor=dict(Normal=dict(hitRecovery=dict(enabled=False), rewards=dict(experience=0))))

    def species(reference, prefix):
        mon = monsters[reference]
        stat = extra[mon['MonStatsEx']]
        level = number(mon, 'Level')
        experience = number(levels[level], 'L-XP') * number(mon, 'Exp') // 100
        return dict(sourceStats=mon, sourceModes=stat, rewards=dict(experience=experience),
                    targeting=dict(threat=number(mon, 'threat')),
                    hitRecovery=dict(enabled=stat['mGH'] == '1', **anim[prefix]),
                    traits=dict(family=mon['BaseId'], undead=mon['hUndead'] == '1' or mon['lUndead'] == '1',
                                demon=mon['demon'] == '1', boss=mon['boss'] == '1',
                                revivable=stat['revive'] == '1', leavesCorpse=mon['corpseSel'] == '1' if 'corpseSel' in mon else None,
                                size=number(stat, 'SizeX'), regeneration=number(mon, 'DamageRegen')))

    sk = species('skeleton1', 'SKGH1HS')
    zm = species('zombie1', 'ZMGHHTH')
    templates['skeleton'] = dict(Normal=sk)
    templates['zombie'] = dict(Normal=zm)
    # Approach values are project UO-tile adaptations of the source species AI.
    for key, chance, radius, wander in [('project-skeleton', 60, 0, False), ('project-zombie', 30, 10, True)]:
        row = json.loads(json.dumps(sk if 'skeleton' in key else zm))
        row['reaction'] = dict(behavior='Species', noticeMs=0, scanMs=600, approachChance=chance,
                               awarenessRange=radius, retryMs=600, wander=wander)
        templates[key] = dict(Normal=row)
    templates['project-lich'] = dict(Normal=dict(rewards=dict(experience=200)))
    guardian = json.loads(json.dumps(sk))
    guardian['rewards'] = dict(experience=0)
    templates['project-guardian'] = dict(Normal=guardian)

    # A proposed fire-dragon analogue, kept out of the live world until its abilities
    # and spawn difficulty are wired to this profile. These are players-1 source bounds.
    mon = monsters['megademon3']
    dragon = {}
    for difficulty, suffix in [('Normal', ''), ('Nightmare', '(N)'), ('Hell', '(H)')]:
        level = number(mon, 'Level' + suffix)
        lvl = levels[level]
        def scaled(field, baseline):
            return max(1, number(mon, field + suffix) * number(lvl, 'L-' + baseline + suffix) // 100)
        row = species('megademon3', 'DMGHHTH')
        row.update(enabled=False, applyStats=False, level=level,
                   life=scaled('maxHP' if not suffix else 'MaxHP', 'HP'), defense=scaled('AC', 'AC'),
                   resistances={key: number(mon, col + suffix) for key, col in
                                [('physical', 'ResDm'), ('magic', 'ResMa'), ('fire', 'ResFi'),
                                 ('lightning', 'ResLi'), ('cold', 'ResCo'), ('poison', 'ResPo')]},
                   attack1=dict(minDamage=scaled('A1MinD', 'DM'), maxDamage=scaled('A1MaxD', 'DM'),
                                attackRating=scaled('A1TH', 'TH'), physical=50, fire=50, lightning=0,
                                cold=0, poison=0, magic=0, range=1, criticalChance=number(mon, 'Crit')),
                   rewards=dict(experience=scaled('Exp', 'XP'), treasureClass=mon.get('TreasureClass1' + suffix)))
        row['traits'].update(drainEffectiveness=number(mon, 'Drain' + suffix), chillEffectiveness=number(mon, 'coldEffect' + suffix))
        dragon[difficulty] = row
    templates['fire-dragon-proposal'] = dragon
    save('creature-templates', templates)

    # The compiled inventory includes private/nested creatures and non-Mobile namespaces.
    rows = json.loads(inventory.read_text())
    old_path = OUT / 'uo-creatures.json'
    old = {r['type']: r for r in json.loads(old_path.read_text())} if old_path.exists() else {}
    for r in rows:
        r.update(source='ModernUO UOContent compiled type inventory', template='vendor' if r['role'] == 'Vendor' else 'native',
                 reference=None, status='Native stats retained; D2 conversion provisional')
        name = r['type'].split('.')[-1]
        if name in ('Skeleton', 'Zombie'):
            r.update(template=name.lower(), reference=name.lower() + '1', status='Native stats; D2 species hit recovery')
        if name in ('Dragon', 'Drake', 'GreaterDragon', 'AncientWyrm', 'SerpentineDragon'):
            r.update(template='fire-dragon-proposal', reference='megademon3', status='Proposed creative analogue; stat application disabled')
        if r['type'] in old and 'variants' in old[r['type']]:
            r['variants'] = old[r['type']]['variants']
    for name, template, reference in [('GraveyardSkeleton', 'project-skeleton', 'skeleton1'),
                                      ('GraveyardZombie', 'project-zombie', 'zombie1'),
                                      ('GraveyardLich', 'project-lich', None), ('SkeletonGuardian', 'project-guardian', 'skeleton1')]:
        row = dict(type='Sanctuary.' + name, parent='Server.Mobiles.BaseCreature', role='Project',
                         source='src/Sanctuary.Content', template=template, reference=reference,
                         status='Project combat implementation; common recovery, reaction and reward profile')
        if row['type'] in old and 'variants' in old[row['type']]:
            row['variants'] = old[row['type']]['variants']
        rows.append(row)
    save('uo-creatures', sorted(rows, key=lambda r: r['type']))
    build_rewards(rows, monsters, levels, save)
    hit_divisors = {'1hss': 8, '1ht': 8, 'bow': 8, 'xbow': 8, '2hsl': 64, '2hss': 32, 'club': 32}
    save('creature-hit-classes', [dict(code=r['code'], hitClass=r['hit class'], divisor=hit_divisors.get(r['hit class'], 16))
                                 for r in table('Weapons') if r['code']])
    # Every source column is retained in the field audit, including graphics and flags;
    # not all are runtime stats or have UO equivalents.
    audit = dict(sourceCommit='45112569deb9384738ccafe5c24ebbb71f41c7c9',
                 animationSha256=hashlib.sha256(data).hexdigest(), tables={})
    for name in ['MonStats', 'MonStats2', 'MonLvl']:
        audit['tables'][name] = dict(sha256=hashlib.sha256((source / f'{name}.txt').read_bytes()).hexdigest(),
                                    fields=list(table(name)[0]))
    save('d2-creature-fields', audit)
    print(f'{len(rows)} common profiles ({len(rows) - 4} built-in + 4 project)')


def build_rewards(rows, monsters, levels, save):
    """Resolve the reviewed, exhaustive type assignments; never guess from UO fame/HP."""
    rules = json.loads((OUT / 'creature-reward-rules.json').read_text())
    expected = {r['type'] for r in rows}
    assigned, identifiers, groups = set(), set(), []
    for rule in rules['groups']:
        assert rule['id'] not in identifiers, f"Duplicate reward group: {rule['id']}"
        identifiers.add(rule['id'])
        assert rule['rationale'] and rule['types'], f"Incomplete reward rule: {rule['id']}"
        reference = rule['reference']
        mon = monsters[reference] if reference else None
        if mon:
            assert mon['enabled'] == '1' and mon['killable'] == '1' and mon['boss'] != '1', reference
            assert len(rule['levels']) == 3
        else:
            assert rule['levels'] is None and 'experience' not in rule
        variants = {}
        for index, (difficulty, suffix) in enumerate([('Normal', ''), ('Nightmare', '(N)'), ('Hell', '(H)')]):
            level = rule['levels'][index] if mon else None
            experience = 0
            if mon:
                assert 1 <= level <= (43 if index == 0 else 68 if index == 1 else 85), rule['id']
                percentage = int(mon['Exp' + suffix] or 0)
                experience = int(levels[level]['L-XP' + suffix]) * percentage // 100
                assert experience > 0, reference
                override = rule.get('experience', [None] * 3)[index]
                if override is not None:
                    assert rule['id'] == 'graveyard-lich' and index == 0 and override == 200
                    experience = override
            variants[difficulty] = dict(enabled=bool(mon), level=level, experience=experience,
                                        reference=reference, rationale=rule['rationale'])
        for name in rule['types']:
            assert name in expected, f"Stale reward assignment: {name}"
            assert name not in assigned, f"Duplicate reward assignment: {name}"
            assigned.add(name)
        groups.append(dict(id=rule['id'], types=rule['types'], variants=variants))
    assert assigned == expected, f"Missing reward assignments: {sorted(expected - assigned)}"
    save('creature-rewards', dict(sourceCommit=rules['sourceCommit'], groups=groups))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', required=True, type=Path)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--animations', required=True, type=Path)
    args = parser.parse_args()
    build(args.inventory, args.source, args.animations)
