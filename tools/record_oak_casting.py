#!/usr/bin/env python3
"""Bind learned Oak casting, innate Command rank and retained resource acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/oak-casting'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
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
    for name in ['oak-rank', 'oak-casting', 'oak-world', 'oak-sage-body', 'battle-orders', 'spirit-wolf-world', 'creeper-world', 'creeper-poison', 'creeper-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for name in ['oak-right', 'oak-quick']:
        assert log.count(name + '-check: passed') == log.count(name + '-result: passed') == 3
    assert '510complete source getter comparisons' in log and '15363native ordered decision comparisons pass' in log
    assert log.count('oak-world-live-result: passed') == 3
    assert log.count('oak-expired-stage: passed') == log.count('oak-expired-reload-check: passed') == 2
    assert log.count('creeper-world-live-result: passed') == log.count('creeper-poison-live-result: passed') == 3
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert 'Learned Oak: three boots/two reloads' in (STAGE / 'stats-run.log').read_text()
    rank = json.loads((STAGE / 'oak-rank-fixtures.json').read_text())
    assert rank['State']['Birth']['AuraRank'] == 1 and rank['Self']['Rank'] == 2 and rank['Self']['Percent'] == 35 and rank['Life'] == 6407
    saved = json.loads((STAGE / 'oak-casting-fixtures.json').read_text())
    assert saved['State']['Owner'] == saved['Owner'] and saved['State']['Profile'] == 'source-birth-and-totem-motion-v2'
    assert saved['State']['Controller']['Fault'] is None and saved['Body'] != saved['Wolf'] != saved['Raven']
    assert saved['PendingMana'] > 0 and saved['State']['Birth']['Rank'] == 1
    for filename in ['oak-sage-engine-evidence.json', 'oak-aura-engine-evidence.json', 'totem-ai-engine-evidence.json', 'oak-skill-rank-engine-evidence.json']:
        source = json.loads((ROOT / 'tools/reference' / filename).read_text())
        assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256'], filename
        for name, digest in source.get('sourceHashes', {}).items():
            assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / name) == digest, name
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        audits.append(audit)
    baseline = json.loads(subprocess.check_output(['git', 'show', '8cf01d9:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present']) == 168
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present' and x['characterClass'] == 'Druid']) == 12
    assert status['completedClasses'] == []
    client = json.loads((STAGE / 'client-proof.log').read_text())
    assert client['Result'] == 'passed'
    for name in ['Baseline', 'Candidate']:
        row = client[name]
        assert row['Ability'] == 174 and row['Label'] == 'Oak Sage' and row['Class'] == 'Druid'
        assert not row['SelfTarget'] and not row['Left'] and row['Right']
        assert row['Sha256'] == sha(STAGE / 'client/cuo.dll')
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log',
        'oak-world-fixtures.json', 'oak-sage-body-fixtures.json', 'oak-expired-aura-fixtures.json', 'oak-rank-fixtures.json', 'oak-casting-fixtures.json',
        'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'client-proof.log', 'client-proof/Program.cs', 'client-proof/check.csproj', 'client-proof-build.log', 'client/cuo.dll']]
    artifacts += [ROOT / 'tools/reference' / name for name in ['oak-sage-preflight.json', 'oak-sage-engine-evidence.json', 'totem-ai-engine-evidence.json', 'oak-aura-engine-evidence.json', 'oak-skill-rank-engine-evidence.json']]
    artifacts += [ROOT / name for name in ['tests/check_oak_casting.py', 'tests/check_creeper_stats.py', 'tools/record_oak_casting.py', 'tools/probe_oak_skill_rank_reference.py']]
    full_markers = None
    if full_class:
        full = json.loads((STAGE / 'class-inputs.json').read_text())
        assert full['artifacts'] == inputs['artifacts'] and full['copiedData'] == inputs['copiedData']
        for name, digest in full['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log
        assert full_log.count('Shutting down') == 3
        assert 'Native class skills and projectile checks passed, including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        assert full_log.count('named-gear-check: passed') == 1 and full_log.count('named-gear-reload-check: passed') == 2
        for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
            assert json.loads((STAGE / ('full-' + name)).read_text()) == json.loads((STAGE / name).read_text()), name
            artifacts.append(STAGE / ('full-' + name))
        full_markers = full_log.count(': passed')
        assert full_markers > 1180
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False,
        handlers=168, druidHandlers=12, completedClasses=[], goalComplete=False, currentExecutableParity=False,
        verified=dict(boots=3, actualReloads=2, connectedRightCasts=3, connectedQuickCasts=3,
            innateRankSourceComparisons=510, sourceGetterContexts=2040, sourceMotionComparisons=15363,
            naturalConnectedFollowExpiryRuns=3, realTimeExpiredAuraReloads=2, passMarkers=log.count(': passed'),
            pythonTests=70, namedDecisions=573, namedInputFingerprints=len(audits[0]['InputHashes']), firstLoopInputFingerprints=len(audits[1]['InputHashes'])),
        fullClassRegression=full_class, fullClassBoots=3 if full_class else 0, fullClassPassMarkers=full_markers,
        clientReusedByteIdentical=True, clientMetadataReflection=True, graphicalClientAcceptance=False,
        sourceInputs=inputs['sourceInputs'], copiedData=inputs['copiedData'], testedAssemblies=inputs['artifacts'],
        artifacts={str(path.relative_to(ROOT)): sha(path) for path in artifacts},
        established=[
            'Learned right/quick SC casting uses14+effective rank mana once, after successful allocation and before shared totem replacement. Fractional insufficiency, early/duplicate release, stop/movement/facet roundtrip/recovery/freeze, lost mana, invalid destination, failed allocation and post-allocation payment failure preserve the existing spirit and do not create a free replacement.',
            'Ground or selected-target placement reuses the existing wolf destination adapter, including live target position, deleted target cached fallback and deterministic adjacent fit. Town follows InTown1. Actual Oak replacement leaves wolves and Ravens alive; all three source spirit categories are recognized, while the other two actors are not yet implemented.',
            'The saved body/controller/life aura retains existing motion, difficulty, history and resource consumers. Three natural connected follow/aura/source-deletion-expiry sessions and two actual expired-lease reloads pass. Saved paid Oak, coexisting pets, right/quick bindings and Command-enhanced aura survive two real world reloads; unpaid windup is discarded without payment or replay.',
            'Complete older innate getter contexts plus current MaxLvl99 support Battle Command increasing NPC aura rank at the next pulse. Immutable birth life/defense/base aura rank do not change. Lower rank does not overwrite a still-live stronger lease after Command removal; it resumes after expiry.',
            'Three real loopback right-hold and three quick packets exercise natural unpaid windup, release, once-only mana and placement. Existing client metadata reflects correct174/Druid/Oak Sage/right-only identity without changing client bytes. This is not a graphical playtest.'
        ], remaining=[
            'Item-only/staffmod permission remains explicitly refused: persistent permission currently requires an allocated Oak point. Exceptional NPC gear/shrine/oskill/single/element skill bonuses, PvP and unmapped recipient contexts remain unsupported.',
            'Initial-think current-aidel-first-think-provisional-v1, source target/vision/node scan, native tile/height/ally occupancy/free-position ring/LOS and global event/RNG order remain declared projections. Source AStar fallback is unsupported and records a fault; independent aura/recovery remain supported.',
            'Full modern executable cadence/room/geometry/current-client cached target and graphical acceptance remain unverified. Other spirit actors still need their own shared-category replacement acceptance. No class is complete.',
            'Final XW body/emergence/movement/hurt/death, recipient/control overlays and spirit/summon audio remain pending effects; talent/right/quick icons remain separately tracked text-label placeholders.'
        ])
    output = ROOT / 'tools/reference/oak-casting-native-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Learned Oak:168handlers/Druid12,3boots/2reloads/3right+3quick casts; fullClass={full_class}; no installation or class completion claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
