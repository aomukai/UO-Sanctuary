#!/usr/bin/env python3
"""Reproduce per-item spell/skill exclusions from the pinned named catalogue."""
import argparse
import json
import re
from pathlib import Path

from build_named_equipment import ROOT, SOURCE, build, read_sources

DATA = ROOT / 'data/Sanctuary/Balance/named-skill-backlog.json'
REPORT = ROOT / 'docs/deferred-named-skills.md'
MODES = {
    'att-skill': 'on-attack proc dispatcher', 'hit-skill': 'on-hit proc dispatcher',
    'gethit-skill': 'on-taking-hit proc dispatcher', 'kill-skill': 'on-kill proc dispatcher',
    'death-skill': 'on-death proc dispatcher', 'levelup-skill': 'on-level-up proc dispatcher',
    'aura': 'equipped aura lifecycle, radius and stacking',
    'charged': 'charged-skill casting, targeting, charge ledger and recharge',
    'oskill': 'cross-class skill grants and casting',
}
# Inherent item attacks/curses use the same still-missing skill mechanics.
INHERENT = {
    'magicarrow': ('Magic Arrow', 'Inherent magic-arrow attack and projectile delivery'),
    'explosivearrow': ('Exploding Arrow', 'Inherent exploding-arrow attack and area damage'),
}


def compile_backlog():
    compiled = build()
    manifest, tables = read_sources(SOURCE)
    skills = tables['json/skills']
    descriptions = {r['skilldesc']: r for r in tables['json/skilldesc'].values() if r.get('skilldesc')}
    strings = tables['json/allstrings-eng']
    bindings = {str(r['sourceId']): r for r in compiled['named-skill-bindings']}
    working = set(json.loads((ROOT / 'data/Sanctuary/Balance/named-active-skills.json').read_text()))
    if not working <= {r['talentId'] for r in bindings.values()}:
        raise ValueError('Active skill lacks its source binding')
    by_name = {r['skill'].casefold(): str(r['*Id']) for r in skills.values() if r.get('skill')}
    definitions = {p['code']: p for p in compiled['named-item-properties']}

    def spell(parameter):
        key = str(parameter) if parameter is not None else ''
        key = by_name.get(key.casefold(), key)
        row = skills.get(key)
        if row is None:
            return dict(sourceParameter=parameter, sourceId=None, name=str(parameter), talentId=None, implemented=False)
        desc = descriptions.get(row.get('skilldesc'), {})
        binding = bindings.get(key, {})
        return dict(sourceParameter=parameter, sourceId=row['*Id'],
                    name=strings.get(desc.get('str name'), row['skill']), sourceKey=row['skill'],
                    talentId=binding.get('talentId'), implemented=binding.get('talentId') in working)

    def prerequisites(prop, via=()):
        if not prop['sourceEnabled']:
            return []
        code = prop['code']
        if code in via:
            raise ValueError('Cyclic property group')
        definition = definitions[code]
        result = []
        if definition['kind'] == 'PropertyGroup':
            raw = definition['sourceFields']
            for key, child in raw.items():
                match = re.fullmatch(r'Prop(\d+)', key)
                if not match or not child:
                    continue
                n = match[1]
                for arg in dict.fromkeys([raw.get('ParMin' + n), raw.get('ParMax' + n)]):
                    result += prerequisites(dict(code=child, sourceEnabled=True, slot=prop['slot'], parameter=arg,
                                                 sourceMin=raw.get('ModMin' + n), sourceMax=raw.get('ModMax' + n)), (*via, code))
            return result
        if code in INHERENT:
            name, mechanism = INHERENT[code]
            skill = spell(name)
            result.append(dict(code=code, slot=prop['slot'], via=list(via), skills=[skill],
                               prerequisites=['Implement spell/skill: ' + skill['name'], mechanism]))
        elif code == 'state' and prop['parameter'] == 'monsterset':
            requirement = ('Complete-set cosmetic aura overlay and equip/break/unequip lifecycle' if prop['parameter'] == 'fullsetgeneric'
                           else 'Vampire transformation with movement, casting, equipment and restoration lifecycle')
            result.append(dict(code=code, slot=prop['slot'], via=list(via), skills=[], prerequisites=[requirement]))
        elif code == 'reanimate':
            result.append(dict(code=code, slot=prop['slot'], via=list(via), skills=[],
                               prerequisites=['On-kill reanimation, source monster template, ownership, AI and lifetime'],
                               sourceArguments={k: prop.get(k) for k in ('parameter', 'sourceMin', 'sourceMax')}))
        elif code in MODES or code == 'skill':
            skill = spell(prop['parameter'])
            missing = ([] if skill['implemented'] else ['Implement spell/skill: ' + skill['name']])
            if code in MODES:
                missing.append(MODES[code])
            if missing:
                result.append(dict(code=code, slot=prop['slot'], via=list(via), skills=[skill], prerequisites=missing))
        elif code == 'skill-rand':
            candidates = [spell(i) for i in range(prop['sourceMin'], prop['sourceMax'] + 1)]
            result.append(dict(code=code, slot=prop['slot'], via=list(via), skills=candidates,
                               prerequisites=['Random skill selection and saved skill identity',
                                              'Implement remaining skills in the source ID range'],
                               sourceArguments={k: prop.get(k) for k in ('parameter', 'sourceMin', 'sourceMax')}))
        elif code in ('randclassskill', 'war') or code == 'skilltab' and prop['parameter'] >= 21:
            requirement = {'fireskill': 'Fire-skill rank bonuses and the affected class skills',
                           'randclassskill': 'Random class bonus selection and saved class identity (including Warlock)',
                           'war': 'Warlock class skill ranks'}.get(code, 'Warlock skill-tree rank bonuses')
            result.append(dict(code=code, slot=prop['slot'], via=list(via), skills=[], prerequisites=[requirement]))
        return result

    sets = []
    by_set = {}
    for group in compiled['equipment-sets']:
        deps = [dict(scope=scope, **dep) for scope, key in [('partial', 'partialBonuses'), ('full', 'fullBonuses')]
                for prop in group[key] for dep in prerequisites(prop)]
        by_set[group['id']] = deps
        if deps:
            sets.append(dict(id=group['id'], name=group['name'], sourceAvailable=group['sourceAvailable'],
                             pieceIds=group['pieceIds'], dependencies=deps))
    items = []
    for item in compiled['unique-items'] + compiled['set-items']:
        if item['baseCode'] in {'cm1', 'cm2', 'cm3'}:
            continue  # Charms are intentionally excluded, not deferred.
        deps = [dict(scope=scope, **dep) for scope, key in [('own', 'properties'), ('piece bonus', 'additionalProperties')]
                for prop in item[key] for dep in prerequisites(prop)]
        if deps or by_set.get(item['setId']):
            items.append(dict(id=item['id'], name=item['name'], rarity=item['rarity'], setId=item['setId'],
                              sourceAvailability=item['sourceAvailability'], sourceSpawnable=item['sourceSpawnable'],
                              dependencies=deps, setDependencies=by_set.get(item['setId'], [])))
    # A skill-dependent piece prevents the entire set being supplied.
    dependent_sets = {r['setId'] for r in items if r['setId'] is not None}
    partners = [dict(id=r['id'], name=r['name'], setId=r['setId']) for r in compiled['set-items']
                if r['setId'] in dependent_sets and r['id'] not in {i['id'] for i in items}]
    return dict(schemaVersion=1, sourceCommit=manifest['commit'], workingDirectSkills=sorted(working),
                items=items, sets=sets, heldSetPartners=partners)


def render(data):
    def cell(value):
        return str(value).replace('|', '\\|').replace('\n', ' ')
    def describe(deps):
        parts = []
        for dep in deps:
            names = ', '.join(s['name'] + (' (spell implemented)' if s['implemented'] else '') for s in dep['skills'])
            text = f"{dep['scope']} `{dep['code']}`"
            if dep['via']:
                text += ' via ' + ', '.join(dep['via'])
            parts.append(text + (': ' + names if names else '') + ' — ' + '; '.join(dep['prerequisites']))
        return '<br>'.join(parts) or 'See set-wide prerequisites below.'
    active = [r for r in data['items'] if r['sourceSpawnable']]
    inactive = [r for r in data['items'] if not r['sourceSpawnable']]
    lines = ['# Deferred named-item spells and skills', '',
             'User decision: withhold items whose spell/skill effects cannot work yet, and return to them when their prerequisites exist. '
             'Charms are excluded by project policy and have no return tasks. '
             'Keep their acquisition weight unchanged: unavailable drops yield nothing, and unavailable gambles refund the stake without rerolling.', '',
             'Art decision (22 October 2026): custom imagegen assets may supply missing equipment art, class/skill icons and spell effects. '
             'For each returning skill or item, review existing art, record any required new assets, retain the prompt and source image in the project, '
             'then verify transparency, scale, directional frames/timing and the actual client resource mapping. '
             'Generation, client integration and gameplay verification are separate completion checks.', '',
             f"Pinned source: `{data['sourceCommit']}`. Reproduce with `python3 tools/build_named_skill_backlog.py`; verify with `--check`. "
             'The machine-readable companion is `data/Sanctuary/Balance/named-skill-backlog.json`.', '',
             'Direct bonuses currently work for Teeth, Raise Skeleton, Skeleton Mastery and Teleport. '
             'A working spell alone does not supply a proc, aura, charge or cross-class grant system. '
             'Generic class/tree/all-skill and source-tagged fire-skill bonuses raise learned or directly granted skills without spending points or unlocking every skill. '
             'Item freeze, blind and flee are implemented shared effects; they do not require the full player class spells to exist.', '',
             f'{len(active)} source-spawnable item definitions have direct or set-wide spell/skill dependencies. '
             f"{len(data['heldSetPartners'])} additional pieces are held so their sets remain completable. "
             'These counts describe skill dependencies, not total runtime availability; other source, appearance or combat-effect gates may also apply.', '']
    for title, rows in [('Source-spawnable items to revisit', active), ('Source-inactive or recipe-only definitions', inactive)]:
        lines += ['## ' + title, '', '| ID | Item | Prerequisite spells/skills and systems |', '| --- | --- | --- |']
        for row in rows:
            lines.append('| ' + ' | '.join(cell(v) for v in [row['id'], row['name'], describe(row['dependencies'] + row['setDependencies'])]) + ' |')
        lines.append('')
    lines += ['## Set-wide dependencies', '', '| Set | Pieces withheld | Partial/full prerequisites |', '| --- | --- | --- |']
    for group in data['sets']:
        lines.append('| ' + ' | '.join(cell(v) for v in [group['name'], ', '.join(group['pieceIds']), describe(group['dependencies'])]) + ' |')
    lines += ['', '## Other pieces held with their set', '', '| ID | Item | Set ID |', '| --- | --- | --- |']
    for row in data['heldSetPartners']:
        lines.append('| ' + ' | '.join(cell(row[k]) for k in ('id', 'name', 'setId')) + ' |')
    lines += ['', 'Before releasing a row, implement and test every listed skill and its delivery mechanism, then review all remaining '
              'runtime gates. Test real acquisition, saved rolls/charges, equip/unequip, broken/unidentified suppression, death, trading and save/reload. '
              'Release complete sets together. Source-disabled definitions stay disabled. '
              'Renewed Cold Rupture also has the separately tracked missing source property `Gelid-Affix5`; no effect has been invented.', '']
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    data = compile_backlog()
    outputs = {DATA: json.dumps(data, indent=2, ensure_ascii=False) + '\n', REPORT: render(data)}
    for path, text in outputs.items():
        if args.check:
            if path.read_text() != text:
                raise SystemExit('Stale named skill backlog: ' + str(path))
        else:
            path.write_text(text)
    print(f"Named skill backlog: {len(data['items'])} item rows, {len(data['sets'])} set-wide dependencies")


if __name__ == '__main__':
    main()
