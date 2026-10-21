#!/usr/bin/env python3
"""Check the real C# eligibility export and build the complete named-item return list."""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AUDIT = ROOT / 'tools/reference/named-item-runtime-audit.json'
OUTPUT = ROOT / 'docs/named-item-readiness.md'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(ok, why):
    if not ok:
        raise ValueError(why)


def revisit(reason):
    if reason == 'No reviewed equipment appearance':
        return 'Review representation, slot/activation rules and client art; jewels need their socket consumers too.'
    if reason == 'Set has unavailable pieces':
        return 'Complete every held partner and bonus before enabling the whole set.'
    if reason == 'Set requires dual wielding':
        return 'Implement both-hand equipment, attack/stat rules and save/wardrobe/death behavior.'
    if reason.startswith('Effect: ') or reason.startswith('Set effect: '):
        code = reason.split(': ', 1)[1]
        if code.endswith(tuple('Affix' + str(n) for n in range(1, 7))) or code == 'roguesbow-affix1':
            return 'Resolve the exact pinned property group and all argument ranges/delivery systems; never invent missing Gelid-Affix5.'
        if code in {'hit-skill', 'gethit-skill', 'att-skill', 'kill-skill', 'death-skill', 'levelup-skill', 'charged', 'oskill', 'aura', 'skill'}:
            return 'Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery.'
        guides = {
            'fireskill': 'Add source fire-skill tagging and learned-rank bonuses across classes; this does not grant unlearned spells.',
            'skill-rand': 'Persist the selected source skill and rank, then implement every possible skill effect.',
            'randclassskill': 'Persist the selected source class and apply its learned skill ranks correctly.',
            'skilltab': 'Bind the exact source class/tree and ensure every affected learned rank reaches its actual consumer.',
            'skilltab-war': 'Implement the Warlock source tree, bindings and skill consumers.',
            'war': 'Implement the source Warlock class and class skill bonuses.',
            'slow': 'Implement independent timed slow with source caps/duration, movement/attack consumers and persistence; Cannot Be Frozen must not remove it.',
            'freeze': 'Implement source freeze chance/duration, target restrictions, AI/root ownership and expiry/reload restoration.',
            'stupidity': 'Implement source blind/vision restriction and targeting/AI lifecycle.',
            'howl': 'Implement source flee behavior, target restrictions and AI restoration.',
            'dmg-ac': 'Implement source per-hit target defence reduction, stacking/scope and lifecycle.',
            'dmg-to-mana': 'Verify source event/mitigation order and route incoming damage to mana once; explicitly test damage-over-time and shield interactions.',
            'rep-dur': 'Implement source repair rate/clock, maximum durability and elapsed-time persistence without scanning the world.',
            'rep-quant': 'Implement replenishing throwing/ammunition quantities, rate/clock, stack caps and persistence.',
            'stack': 'Implement source stack capacity and quantity-consuming attacks/trade/save behavior.',
            'pierce': 'Implement projectile continuation, chance and repeat-hit/range/LOS/friendly checks; this is separate from resistance pierce.',
            'stamdrain': 'Implement the source running/stamina-drain consumer and verify its units.',
            'cheap': 'Apply the source discount consistently to actual vendor quotes and final transactions.',
            'rip': 'Mark appropriate corpses and honor the flag in every revival/corpse-skill consumer while preserving ordinary loot.',
            'bloody': 'Implement the source blood/death visual adaptation and actual client display.',
            'state': 'Implement the vampire transformation, class movement/casting/equipment rules and equip/break/unequip/save restoration; golden set auras already work.',
            'fade': 'Implement the source transparency/appearance state and removal/reload restoration.',
            'reanimate': 'Implement source monster template, on-kill ownership, AI, lifetime and persistence.',
            'magicarrow': 'Implement the source Magic Arrow replacement attack and its ammunition/damage rules.',
            'explosivearrow': 'Implement the source exploding-arrow replacement attack, area targeting and damage rules.',
        }
        if code.startswith('pierce-immunity-'):
            return 'Implement the source immunity-breaking mechanic, penalties, scope and interaction with ordinary pierce.'
        return guides.get(code, 'Audit the pinned property definition, arguments, units and all damage/skill consumers before enabling it.')
    return 'Honor the stated class, quest, recipe or source-acquisition restriction; add its actual prerequisite before enabling it.'


def validate(audit, root=ROOT, assembly=None):
    require(audit['SchemaVersion'] == 1, 'Unknown runtime audit version')
    for name, expected in audit['InputHashes'].items():
        path = (root / name).resolve()
        require(path.is_relative_to(root.resolve()), 'Audit input escapes workspace')
        require(digest(path) == expected, 'Stale named runtime evidence: ' + name)
    inputs = {p.relative_to(root).as_posix()
              for directory in ['src/Sanctuary.Content', 'shared', 'data/Sanctuary/Balance']
              for p in (root / directory).iterdir() if p.is_file() and p.suffix in {'.cs', '.json'}}
    inputs.update(p.relative_to(root).as_posix() for p in (root / 'patches/ModernUO').glob('*.patch'))
    inputs.update(p.relative_to(root).as_posix() for p in (root / 'patches/ClassicUO').glob('*.patch'))
    inputs.update('tests/Sanctuary.Integration/' + name for name in ['NamedGearChecks.cs', 'NamedLifecycleChecks.cs', 'NamedVisualUiChecks.cs'])
    require(set(audit['InputHashes']) == inputs, 'Incomplete runtime input inventory: reexport after adding/removing sources')
    if assembly:
        require(digest(assembly) == audit['ContentAssemblySha256'], 'Runtime audit assembly differs')
    balance = root / 'data/Sanctuary/Balance'
    source = {d['id']: d for file in ['unique-items', 'set-items']
              for d in json.loads((balance / (file + '.json')).read_text())}
    rows = audit['Definitions']
    ids = [r['Id'] for r in rows]
    require(len(set(ids)) == len(ids) and set(ids) == set(source), 'Runtime audit lost or duplicated named definitions')
    by_id = {r['Id']: r for r in rows}
    for row in rows:
        definition = source[row['Id']]
        for field in ['Name', 'Rarity', 'BaseCode', 'SetId', 'SourceAvailability']:
            require(row[field] == definition[field[0].lower() + field[1:]], 'Runtime identity differs: ' + row['Id'])
        require(isinstance(row['Available'], bool) and isinstance(row['Reasons'], list), 'Invalid runtime decision')
        require(row['Available'] == (not row['Reasons']), 'Eligibility and blockers disagree: ' + row['Id'])
        require(not row['Available'] or definition['sourceSpawnable'], 'Source-inactive definition enabled')
        if row['BaseCode'] in {'cm1', 'cm2', 'cm3'}:
            require(not row['Available'] and 'Charms are excluded by project policy' in row['Reasons'], 'Excluded charm enabled or lost policy')
    for item_set in json.loads((balance / 'equipment-sets.json').read_text()):
        active = [by_id[i]['Available'] for i in item_set['pieceIds']]
        require(not any(active) or all(active), 'Partially enabled set: ' + item_set['name'])
    return rows


def render(audit):
    rows = sorted(audit['Definitions'], key=lambda r: (r['Rarity'], int(r['Id'].split('/')[1])))
    ready = Counter(r['Rarity'] for r in rows if r['Available'])
    excluded = [r for r in rows if r['BaseCode'] in {'cm1', 'cm2', 'cm3'}]
    blocked = [r for r in rows if not r['Available'] and r not in excluded and r['SourceAvailability'] == 'SpawnableDefinition']
    inactive = [r for r in rows if not r['Available'] and r not in excluded and r['SourceAvailability'] != 'SpawnableDefinition']
    reasons = Counter(reason for r in blocked for reason in r['Reasons'])
    lines = ['# Named-item runtime readiness and return list', '',
             'Generated from the real native factory/eligibility fixture. This records staged acquisition eligibility; '
             'it does not certify deployment or every gameplay path. Every source definition appears in the runtime audit, '
             'and every deferred definition is listed below. Excluded charms remain in the source audit only. Rebuild with `python3 tools/build_named_readiness.py`; verify with `--check`.', '',
             f"Currently eligible: **{ready['Unique']} Unique variants and {ready['Set']} Set pieces**. "
             f'**{len(blocked)} source-spawnable definitions remain held**; {len(inactive)} other source records retain their source/quest/recipe restrictions. '
             f'{len(excluded)} charm definitions are intentionally excluded, with no implementation or class-return task.', '',
             'Unavailable outcomes keep their original weight: empty drops or refunded gambles, without replacement rolls. '
             'A Set stays held until all its pieces and bonuses can work. '
             'The [spell/skill prerequisite list](deferred-named-skills.md) gives the exact source skills and delivery systems.', '',
             '## Required revisit during class work', '',
             '1. Implement the named spell/skill and its proc, aura, charge or cross-class delivery system; learning a spell alone does not complete these dependencies.',
             '2. Review visual needs for every returning skill/item. Custom imagegen art is approved where needed; track icons, effects, directional frames, client resource integration and in-game verification.',
             '3. Recheck every affected item and whole set below, including socketable jewels, dual wielding, transformations, source groups and non-spell effects.',
             '4. Run the native named fixture, export a fresh runtime audit and regenerate both return lists. Stale input hashes fail verification.',
             '5. Verify acquisition → identification → equip/bonuses → durability/socket changes → trade/wardrobe → death/recovery → save/reload before enabling the definition.', '',
             'Class integration contract: consume `NamedSkills.Rank` and shared damage, defence, status and control APIs. '
             'New throwing/missile skills must spend quantity once at release and share the pierce budget; corpse skills must honor Rest in Peace. '
             'Recheck actual-life-loss damage-to-mana when Energy Shield or another mitigation layer is introduced.', '',
             '## Outstanding blocker inventory', '', 'A definition may have several blockers. These include shared item mechanics as well as class skills.', '',
             '| Blocker | Definitions | Return requirement |', '| --- | ---: | --- |']
    def workstream(reason):
        code = reason.split(': ', 1)[-1]
        if code in {'skill', 'skilltab', 'war', 'skilltab-war', 'allskills', 'fireskill', 'oskill', 'skill-rand', 'randclassskill',
                    'hit-skill', 'gethit-skill', 'att-skill', 'kill-skill', 'death-skill', 'levelup-skill', 'charged', 'aura',
                    'reanimate', 'magicarrow', 'explosivearrow'}:
            return 'Class skills and effect delivery'
        if code in {'slow', 'freeze', 'stupidity', 'howl', 'dmg-ac', 'dmg-to-mana', 'pierce', 'rip', 'bloody', 'stamdrain'} or code.startswith('pierce-immunity-'):
            return 'Shared combat, statuses and projectiles'
        if code in {'rep-dur', 'rep-quant', 'stack', 'cheap'}:
            return 'Durability, quantities and prices'
        if code in {'state', 'fade'} or reason == 'No reviewed equipment appearance' or reason == 'Set requires dual wielding':
            return 'Equipment, sockets and appearance'
        if reason == 'Set has unavailable pieces':
            return 'Whole-set dependency gate'
        return 'Source groups, restrictions and unresolved data'
    groups = {}
    for row in blocked:
        for reason in row['Reasons']:
            name = workstream(reason)
            group = groups.setdefault(name, {'items': set(), 'reasons': set()})
            group['items'].add(row['Id']); group['reasons'].add(reason)
    board = ['', '## Dependency map', '',
             'Counts overlap: one item may need several systems. Shared combat, maintenance and set visuals are implemented. '
             'The remaining work is grouped below; whole-set partner gates clear automatically when every piece and bonus works.', '',
             '| Workstream | Held definitions | Remaining gates |', '| --- | ---: | --- |']
    descriptions = {
        'Class skills and effect delivery': 'Implement each listed skill/effect and its proc, aura, charge or cross-class delivery. A rank bonus alone does not complete these paths.',
        'Equipment, sockets and appearance': 'Rainbow Facet socket delivery; dual wielding; vampire form; held Warlock/quest gear representations. The item rows identify each dependency.',
        'Source groups, restrictions and unresolved data': 'Saved random source-group choices, six quest-only acquisition paths, Warlock prerequisites and unresolved source data. Never invent missing definitions.',
        'Whole-set dependency gate': 'These are dependent partner pieces, not separate mechanics. Release the whole set after all pieces and bonuses work.',
    }
    for name, group in sorted(groups.items()):
        board.append(f"| {name} | {len(group['items'])} | {descriptions.get(name, '; '.join(sorted(group['reasons'])))} |")
    # Put the map ahead of the detailed blocker inventory.
    location = lines.index('## Outstanding blocker inventory')
    lines[location:location] = board + ['']
    lines += [f'| {reason} | {count} | {revisit(reason)} |' for reason, count in sorted(reasons.items())]
    for title, group in [('Source-spawnable items to complete', blocked), ('Restricted or inactive source records', inactive)]:
        lines += ['', '## ' + title, '', '| ID | Item | Remaining prerequisites |', '| --- | --- | --- |']
        for row in group:
            lines.append(f"| {row['Id']} | {row['Name'].replace('|', '/')} | {'; '.join(row['Reasons']).replace('|', '/')} |")
    lines += ['', '## Eligible definitions', '', '| ID | Item | Rarity |', '| --- | --- | --- |']
    lines += [f"| {r['Id']} | {r['Name'].replace('|', '/')} | {r['Rarity']} |" for r in rows if r['Available']]
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit', type=Path, default=AUDIT)
    parser.add_argument('--assembly', type=Path)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    audit = json.loads(args.audit.read_text())
    validate(audit, assembly=args.assembly)
    result = render(audit)
    if args.check:
        require(args.output.read_text() == result, 'Named readiness list is stale; regenerate from current runtime evidence')
    else:
        args.output.write_text(result)
    print(f"Named readiness: {len(audit['Definitions'])} definitions accounted for")


if __name__ == '__main__':
    main()
