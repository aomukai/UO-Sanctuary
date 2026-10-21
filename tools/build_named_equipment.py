#!/usr/bin/env python3
"""Compile every pinned D2R Unique/Set definition for reviewed runtime gating.

The vendored CASC extraction is primary mechanical data, not wiki prose. No
network or game process is involved. Source fields are retained losslessly.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'tools/reference/d2r-named-equipment'
BALANCE = ROOT / 'data/Sanctuary/Balance'
REPORT = ROOT / 'docs/named-equipment-catalogue.md'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_sources(source):
    manifest = json.loads((source / 'sources.json').read_text())
    if manifest['schemaVersion'] != 1 or not re.fullmatch('[0-9a-f]{40}', manifest['commit']):
        raise ValueError('Unsupported named-item source manifest')
    for path, pin in manifest['files'].items():
        if digest(source / path) != pin['sha256']:
            raise ValueError('Pinned source differs: ' + path)
    tables = {p[:-5]: json.loads((source / p).read_text())
              for p in manifest['files'] if p.endswith('.json')}
    return manifest, tables


def properties(row, pattern, param_prefix, min_prefix, max_prefix):
    """Retain source arguments; min/max are not always a random range (procs!)."""
    result = []
    for key, code in row.items():
        match = re.fullmatch(pattern, key)
        if not match or not code:
            continue
        slot = match[1]
        result.append(dict(slot=slot, code=code.lstrip('*'), sourceCode=code, sourceEnabled=not code.startswith('*'), parameter=row.get(param_prefix + slot),
                           sourceMin=row.get(min_prefix + slot), sourceMax=row.get(max_prefix + slot)))
    return sorted(result, key=lambda p: (int(re.match(r'\d+', p['slot'])[0]), p['slot']))


def build(source=SOURCE, balance=BALANCE):
    manifest, tables = read_sources(source)
    table = lambda name: tables['json/' + name]
    strings = table('allstrings-eng')
    localize = lambda key: strings.get(key, key)
    unique_rows = sorted(table('uniqueitems').values(), key=lambda row: row['*ID'])
    piece_rows = sorted(table('setitems').values(), key=lambda row: row['*ID'])
    set_rows = sorted(table('sets').values(), key=lambda row: row['lineNumber'])
    set_ids = {row['index']: 'set/' + str(row['lineNumber']) for row in set_rows}
    members = {name: [row for row in piece_rows if row['set'] == name] for name in set_ids}
    if any(not rows for rows in members.values()):
        raise ValueError('Source set has no defined members')

    old_uniques = {r['*ID']: r for r in tables['json/base/uniqueitems'].values()}
    old_pieces = {r['*ID']: r for r in tables['json/base/setitems'].values()}
    recipes = defaultdict(list)
    for key, row in table('cubemain').items():
        for column in ('output', 'output b', 'output c'):
            if row.get(column) and row.get('enabled') == 1:
                recipes[str(row[column]).split(',')[0]].append(dict(sourceId=key, outputColumn=column, sourceFields=row))

    def item(row, rarity):
        is_unique = rarity == 'Unique'
        code = row.get('code' if is_unique else 'item')
        own = properties(row, r'prop(\d+)', 'par', 'min', 'max')
        status = ('IncompleteSourceRow' if not code or not own else
                  'DisabledSourceRow' if row.get('spawnable') == 0 else
                  'SpawnableDefinition' if row.get('spawnable') == 1 else
                  'SourceRecipe' if row['index'] in recipes else 'NonSpawnableSourceRow')
        old = (old_uniques if is_unique else old_pieces).get(row['*ID'])
        result = dict(id=('unique/' if is_unique else 'piece/') + str(row['*ID']),
                      sourceId=row['*ID'], sourceRow=row['lineNumber'], sourceKey=row['index'],
                      name=localize(row['index']), rarity=rarity, baseCode=code,
                      sourceAvailability=status, sourceSpawnable=row.get('spawnable') == 1,
                      resurrectedExpansionAddition=old is None or row.get('spawnable') == 1 and old.get('spawnable') != 1,
                      selectionWeight=row.get('rarity', 0), qualityLevel=row.get('lvl', 0),
                      requiredLevel=row.get('lvl req', 0), properties=own,
                      additionalProperties=[], setId=None, recipes=recipes.get(row['index'], []), sourceFields=row)
        if not is_unique:
            result['setId'] = set_ids[row['set']]
            siblings = [s for s in members[row['set']] if s['*ID'] != row['*ID']]
            mode = row.get('add func', 0)
            if mode not in (0, 1, 2):
                raise ValueError('Unknown set-piece activation mode')
            for prop in properties(row, r'aprop(\d+[ab])', 'apar', 'amin', 'amax'):
                n = int(prop['slot'][:-1])
                if mode == 1 and n > len(siblings):
                    raise ValueError('Set-piece trigger has no sibling')
                prop.update(activation=('Always' if mode == 0 else 'SpecificPiece' if mode == 1 else 'PieceCount'),
                            requiredPieces=n + 1 if mode == 2 else 0,
                            requiredPieceId='piece/' + str(siblings[n - 1]['*ID']) if mode == 1 else None)
                result['additionalProperties'].append(prop)
        return result

    uniques = [item(row, 'Unique') for row in unique_rows]
    pieces = [item(row, 'Set') for row in piece_rows]
    sets = []
    for row in set_rows:
        partial = properties(row, r'PCode(\d+[ab])', 'PParam', 'PMin', 'PMax')
        for prop in partial:
            prop['requiredPieces'] = int(prop['slot'][:-1])
        sets.append(dict(id=set_ids[row['index']], sourceRow=row['lineNumber'], sourceKey=row['index'],
                         name=localize(row.get('name', row['index'])),
                         sourceAvailable=any(r.get('spawnable') == 1 for r in members[row['index']]),
                         pieceIds=['piece/' + str(r['*ID']) for r in members[row['index']]],
                         partialBonuses=partial,
                         fullBonuses=properties(row, r'FCode(\d+)', 'FParam', 'FMin', 'FMax'),
                         sourceFields=row))

    all_items = uniques + pieces
    source_bases = {code: (name, row) for name in ('weapons', 'armor', 'misc') for code, row in table(name).items()}
    project_bases = {row['code']: row for name in ('normal-bases', 'equipment-tier-bases', 'gambling-tier-bases')
                     for row in json.loads((balance / (name + '.json')).read_text())}
    variants = defaultdict(dict)
    for name in ('gear-drops', 'gear-drops-nightmare', 'gear-drops-hell'):
        for row in json.loads((balance / (name + '.json')).read_text())['bases']:
            for appearance in row['variants']:
                variants[row['code']][appearance['type']] = appearance
    bases = []
    for code in sorted({i['baseCode'] for i in all_items if i['baseCode']}):
        if code not in source_bases:
            raise ValueError('Named source item refers to missing base: ' + code)
        table_name, row = source_bases[code]
        tier = next((tier for column, tier in [('normcode', 'Normal'), ('ubercode', 'Exceptional'), ('ultracode', 'Elite')]
                     if row.get(column) == code), 'Special')
        bases.append(dict(code=code, name=localize(row.get('namestr', code)), sourceTable=table_name,
                          tier=tier, itemType=row['type'], requiredClass=table('itemtypes')[row['type']].get('Class'),
                          sourceRequiredLevel=row.get('levelreq', 0),
                          projectBasePresent=code in project_bases,
                          projectAppearances=sorted(variants[code].values(), key=lambda r: r['type']),
                          sourceFields=row))

    occurrences = defaultdict(set)
    def collect(owner, props):
        for prop in props:
            occurrences[prop['code']].add(owner)
    canonical = {key.casefold(): key for name in ('properties', 'propertygroups') for key in table(name)}
    def normalize(props):
        for prop in props:
            prop['code'] = canonical.get(prop['code'].casefold(), prop['code'])
    for row in all_items:
        normalize(row['properties'] + row['additionalProperties'])
    for row in sets:
        normalize(row['partialBonuses'] + row['fullBonuses'])
    for row in all_items:
        collect(row['id'], row['properties'] + row['additionalProperties'])
    for row in sets:
        collect(row['id'], row['partialBonuses'] + row['fullBonuses'])
    pending = list(occurrences)
    definitions = {}
    while pending:
        code = pending.pop()
        if code in definitions:
            continue
        prop = table('properties').get(code)
        group = table('propertygroups').get(code)
        definitions[code] = dict(code=code, kind='Property' if prop else 'PropertyGroup' if group else 'MissingSourceDefinition',
                                 usedBy=sorted(occurrences[code]), sourceFields=prop or group or {})
        if group:
            for key, child in group.items():
                if re.fullmatch(r'Prop\d+', key) and child:
                    occurrences[child].add('property-group/' + code)
                    pending.append(child)
    # A property reached through several groups must retain every backreference.
    for code, row in definitions.items():
        row['usedBy'] = sorted(occurrences[code])

    # Dependency hashes make mapping/coverage drift visible without changing live data.
    mapping_files = ['normal-bases', 'equipment-tier-bases', 'gambling-tier-bases',
                     'gear-drops', 'gear-drops-nightmare', 'gear-drops-hell']
    bases_by_code = {r['code']: r for r in bases}
    coverage = dict(schemaVersion=1, activation='RuntimeGated', sourceCommit=manifest['commit'],
                    sourceFiles={p: pin['sha256'] for p, pin in manifest['files'].items()},
                    mappingInputs={name + '.json': digest(balance / (name + '.json')) for name in mapping_files},
                    counts=dict(uniqueRows=len(uniques), uniqueAvailability=dict(sorted(Counter(r['sourceAvailability'] for r in uniques).items())),
                                setPieces=len(pieces), spawnableSetPieces=sum(r['sourceSpawnable'] for r in pieces),
                                sets=len(sets), sourceAvailableSets=sum(r['sourceAvailable'] for r in sets),
                                bases=len(bases), propertyCodes=len(definitions),
                                propertyKinds=dict(sorted(Counter(r['kind'] for r in definitions.values()).items()))),
                    missingProjectBases=[r['code'] for r in bases if not r['projectBasePresent']],
                    itemsWithoutProjectAppearance=[r['id'] for r in all_items if r['baseCode']
                                                   and not bases_by_code[r['baseCode']]['projectAppearances']],
                    unresolvedSourceProperties=[code for code, row in sorted(definitions.items()) if row['kind'] == 'MissingSourceDefinition'],
                    effectIntegration='NamedGear gates each complete definition and complete set against reviewed appearances and implemented effects; unsupported outcomes retain their source weight.')
    descriptions = {r['skilldesc']: r for r in table('skilldesc').values() if r.get('skilldesc')}
    normalize = lambda value: re.sub('[^a-z0-9]', '', value.lower())
    source_skills = {}
    for row in table('skills').values():
        if row.get('charclass') not in ('ama', 'sor', 'nec', 'pal', 'bar', 'dru', 'ass'):
            continue
        description = descriptions.get(row.get('skilldesc'), {})
        for name in [row['skill'], localize(description.get('str name', row['skill']))]:
            source_skills[normalize(name)] = (row, description)
    skill_bindings = []
    for character in json.loads((ROOT / 'data/Sanctuary/talents.json').read_text()):
        for tree in character['trees']:
            for talent in tree['skills']:
                key = normalize(talent['name'])
                # D2R renamed source skill127; Sanctuary retains its LoD label.
                row, description = source_skills['blademastery' if key == 'swordmastery' else key]
                tab = ('ama', 'sor', 'nec', 'pal', 'bar', 'dru', 'ass').index(row['charclass']) * 3 + description['SkillPage'] - 1
                skill_bindings.append(dict(sourceId=row['*Id'], sourceKey=row['skill'], talentId=talent['id'],
                                           characterClass=character['class'], classCode=row['charclass'], sourceTab=tab, element=row.get('EType')))
    coverage['skillBindingInput'] = digest(ROOT / 'data/Sanctuary/talents.json')
    coverage['activeSkillInput'] = digest(balance / 'named-active-skills.json')
    outputs = {'unique-items': uniques, 'set-items': pieces, 'equipment-sets': sets,
               'named-item-bases': bases, 'named-item-properties': sorted(definitions.values(), key=lambda r: r['code']),
               'named-item-coverage': coverage, 'named-skill-bindings': sorted(skill_bindings, key=lambda r: r['sourceId'])}
    return outputs


def render_report(outputs):
    u, p, sets = (outputs[k] for k in ('unique-items', 'set-items', 'equipment-sets'))
    bases = {b['code']: b for b in outputs['named-item-bases']}
    counts = outputs['named-item-coverage']['counts']
    def cell(value):
        return str(value if value is not None else '—').replace('|', '\\|').replace('\n', ' ')
    def prop(value):
        arg = '' if value['parameter'] is None else '[' + str(value['parameter']) + ']'
        values = '' if value['sourceMin'] is None and value['sourceMax'] is None else ' ' + str(value['sourceMin']) + '..' + str(value['sourceMax'])
        return cell(('SOURCE-DISABLED ' if not value['sourceEnabled'] else '') + value['code'] + arg + values)
    lines = ['# Complete named-equipment catalogue', '',
             'Generated by `tools/build_named_equipment.py`; edit the source/compiler, not this file.', '',
             'These are complete **source definitions**. `NamedGear` separately gates runtime creation and acquisition against working effects, '
             'compatible appearances and complete sets. The [implementation audit](set-unique-audit.md) tracks integration, and '
             'the [deferred spell/skill checklist](deferred-named-skills.md) records prerequisites for withheld items. '
             'The [runtime return list](named-item-readiness.md) accounts for every eligible and held definition, including shared mechanics and asset review. '
             'All source rows are retained, including disabled/incomplete entries; no properties are invented for them.', '',
             f"Source: [blizzhackers/d2data](https://github.com/blizzhackers/d2data/tree/{outputs['named-item-coverage']['sourceCommit']}) "
             '(CASC game-table extraction, D2R including Reign of the Warlock). '
             'The pinned files and MIT notice are in [the reference directory](../tools/reference/d2r-named-equipment/README.md).', '',
             f"Inventory: **{len(u)} Unique source rows**, **{len(p)} Set pieces** and **{len(sets)} sets**. "
             f"The source marks {counts['uniqueAvailability']['SpawnableDefinition']} Unique variants and "
             f"{counts['spawnableSetPieces']} Set pieces in {counts['sourceAvailableSets']} sets as spawnable. "
             'This flag alone does not authorize ordinary loot: source quality levels and special acquisition restrictions still apply. '
             'Recipe-only and non-spawnable rows remain distinct. See the machine-readable coverage file for all categories.', '',
             'Property notation below preserves `code[parameter] sourceMin..sourceMax`. These are source arguments: '
             'for example, a proc uses chance/skill-level fields, not a uniformly random range. '
             'Displayed required levels are the maximum of the named row and base requirement, before Sanctuary adaptation. '
             'Full source flags, costs, art references and requirements remain in the JSON definitions. '
             'Native appearances below are source projection candidates; runtime filtering and reviewed family fallbacks determine usable equipment appearances.', '',
             '## Uniques', '', '| ID | Name | Base / tier | Required level | Source availability | Properties |',
             '| --- | --- | --- | --- | --- | --- |']
    for row in u:
        basis = bases.get(row['baseCode'])
        lines.append('| ' + ' | '.join(map(cell, [row['id'], row['name'],
                     (basis['name'] + ' / ' + basis['tier']) if basis else row['baseCode'], max(row['requiredLevel'], basis['sourceRequiredLevel'] if basis else 0),
                     row['sourceAvailability'], '; '.join(prop(v) for v in row['properties'])])) + ' |')
    for group in sets:
        lines += ['', '## ' + group['name'], '', f"ID: `{group['id']}`. Source available: **{group['sourceAvailable']}**.", '',
                  '| Piece ID | Name | Base | Required level | Properties | Additional properties |',
                  '| --- | --- | --- | --- | --- | --- |']
        for row in p:
            if row['setId'] != group['id']:
                continue
            extra = []
            for value in row['additionalProperties']:
                trigger = (f"{value['requiredPieces']} pieces" if value['activation'] == 'PieceCount' else
                           value['requiredPieceId'] if value['activation'] == 'SpecificPiece' else 'always')
                extra.append(prop(value) + ' (' + trigger + ')')
            lines.append('| ' + ' | '.join(map(cell, [row['id'], row['name'], bases[row['baseCode']]['name'],
                          max(row['requiredLevel'], bases[row['baseCode']]['sourceRequiredLevel']), '; '.join(prop(v) for v in row['properties']), '; '.join(extra)])) + ' |')
        lines += ['', 'Partial bonuses: ' + ('; '.join(f"{v['requiredPieces']} pieces: {prop(v)}" for v in group['partialBonuses']) or 'none') + '.', '',
                  'Full bonuses: ' + ('; '.join(prop(v) for v in group['fullBonuses']) or 'none') + '.']
    lines += ['', '## Integration coverage', '',
              f"{len(outputs['named-item-coverage']['missingProjectBases'])} referenced source bases were absent from the baseline project base catalogue. "
              f"{len(outputs['named-item-coverage']['itemsWithoutProjectAppearance'])} item definitions have no direct mapped loot appearance. "
              'Source definitions include charms/jewels and new class/base types. Charms are intentionally excluded by project policy; their source records remain for provenance, with no implementation or class-return tasks.', '',
              'Source inconsistency: Renewed Cold Rupture (`unique/427`) refers to `Gelid-Affix5`, which has no source property/group definition. '
              'This reference is retained and explicitly reported, without an invented effect. The remaining190 property codes and33 groups resolve to source definitions.', '',
              'Definition identity uses source numeric IDs, so Rainbow Facet variants, retired Azurewrath and current Azurewrath remain distinct. '
              'English localization is applied without replacing source keys. Set bonuses retain individual property slots and exact piece/count conditions.', '']
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-dir', type=Path, default=SOURCE)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs = build(args.source_dir)
    payloads = {BALANCE / (name + '.json'): json.dumps(rows, indent=2, ensure_ascii=False) + '\n'
                for name, rows in outputs.items()}
    payloads[REPORT] = render_report(outputs)
    for path, payload in payloads.items():
        if args.check:
            if path.read_text() != payload:
                raise ValueError('Stale named-item catalogue: ' + str(path.relative_to(ROOT)))
        else:
            path.write_text(payload)
    print(json.dumps(outputs['named-item-coverage']['counts'], sort_keys=True))


if __name__ == '__main__':
    main()
