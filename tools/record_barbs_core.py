#!/usr/bin/env python3
"""Bind prepared Barbs body/shared aura/flat-ATD acceptance; learned casting remains disabled."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/barbs'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {p for p in inputs['sourceInputs'] if p.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        build = (STAGE / (kind + '-build.log')).read_text()
        assert '0 Warning(s)' in build and '0 Error(s)' in build
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    assert log.count(': passed') == 135
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert '46089native ordered decision comparisons' in log
    for name in ['barbs-aura', 'barbs-world', 'gem-activation', 'prepared-weapon-event', 'wolverine-aura', 'wolverine-world', 'wolverine-casting', 'oak-world', 'creeper-world', 'jab', 'power-strike']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for name in ['barbs-world', 'wolverine-world', 'oak-world']:
        assert log.count(name + '-live-result: passed') == 3, name
    assert 'Barbs prepared core:' in (STAGE / 'stats-run.log').read_text()
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
    saved = json.loads((STAGE / 'barbs-aura-fixtures.json').read_text())
    assert saved['Life'] == 6407 and saved['Maximum'] == 54528
    assert saved['Self']['Ability'] == saved['Recipient']['Ability'] == 180
    assert saved['Self']['FlatDamage'] == saved['Recipient']['FlatDamage'] == 32
    assert saved['State']['Birth']['Regeneration256'] == 39
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present']) == 169
    assert status['completedClasses'] == []
    evidence_paths = [ROOT / 'tools/reference' / name for name in ['spirit-of-barbs-preflight.json', 'spirit-of-barbs-engine-evidence.json', 'barbs-aura-engine-evidence.json', 'flat-retaliation-engine-evidence.json']]
    for path in evidence_paths:
        source = json.loads(path.read_text())
        if 'fixture' in source:
            assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256'], path
        for name, digest in source.get('sourceHashes', {}).items():
            assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / name) == digest, name
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'barbs-aura-fixtures.json', 'barbs-world-fixtures.json', 'gem-activation.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']]
    artifacts += evidence_paths + [ROOT / name for name in ['tests/check_barbs.py', 'tests/check_creeper_stats.py', 'tools/build_class_skills.py', 'tools/record_barbs_core.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, handlerEnabled=False, handlers=169, druidHandlers=13, completedClasses=[], fullClassRegression=False, currentExecutableParity=False,
        scope='Prepared saved Barbs body/controller and shared combat-spirit leases/flat ATD; no learned Barbs entry or installation. Full regression belongs to the later cast candidate.',
        verified=dict(boots=3, actualReloads=2, passMarkers=135, naturalBarbsFollowExpiryRuns=3, sourceBirthCompositions=180, sourceAuraContexts=440, effectiveRanks=255, sourceTotemComparisons=46089, pythonTests=70),
        sourceInputs=inputs['sourceInputs'], copiedData=inputs['copiedData'], testedAssemblies=inputs['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Own current213life/196defense,25percent birth-life scaling,39FP regeneration and X4walking248 reuse existing do119/Oak birth, saved vulnerable body, proportional facet scaling and Totem motion. Native incoming physical/elemental damage and poison resistance are exercised.',
            'Legacy persistence175/name and optional lease defaults preserve old Heart JSON. Separate ability-keyed self/recipient leases provide only Barbs flat ATD; Heart AR/ED remains independent. Exact rank/creator/supplier/expiry and NPC Command rank changes leave immutable birth stats intact.',
            'Barbs,flat Thorns and compatible item/gem ATD join one physical stat before mitigation; percentage Thorns remains a second packet. Actual Amber/native missed swing,hit,ranged/spell,physical immunity and nonrecursive return pass,including fractional rounding that distinguishes separate packets.',
            'Gem actual root leech and item ATD assertions now inspect FP256 balances. Gem activation and both real reloads pass; the older recovery package extra-gem fixture failure was an obsolete expectation and is not silently relabelled as a passing old run.',
        ], remaining=[
            'Learned SC/right/quick casting,24+rank mana transaction and actual Oak/Heart/Barbs replacement are not enabled or accepted here. Full class regression and preserved installation are still required for the final candidate.',
            'The source incoming HP/death/event ordering gap remains: native/content return still precedes incoming life commitment. Iron Maiden/other callbacks,native absorb chronology and modern global event/RNG/mode equivalence are not certified.',
            'Existing item-only permission,exceptional NPC/PvP/unmapped producer contexts,current first-think/vision/room/geometry/path/AStar and graphical acceptance limits remain explicit. Shared scheduling is not a current executable certificate.',
            'Final X4 body/emergence/walk/hurt/death,recipient/control overlays,retaliation contact and audio remain pending; silent wisp0x3A/violet0x48D and shared retaliation flash are placeholders. Talent/right/quick icons remain separate text labels.',
        ])
    output = ROOT / 'tools/reference/barbs-core-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Prepared Barbs:135markers/3boots/2reloads/3natural runs,169handlers; learned cast/full regression/install remain unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
