#!/usr/bin/env python3
"""Bind learned Heart casting to saved spirit motion and combat-aura acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/wolverine'


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
    for name in ['wolverine-casting', 'wolverine-aura', 'wolverine-world', 'oak-rank', 'oak-casting', 'oak-world', 'oak-sage-body', 'battle-orders', 'spirit-wolf-world', 'creeper-world', 'creeper-poison', 'creeper-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for name in ['oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick']:
        assert log.count(name + '-check: passed') == log.count(name + '-result: passed') == 3
    assert '510complete source getter comparisons' in log and '30726native ordered decision comparisons pass' in log
    assert log.count('oak-world-live-result: passed') == log.count('wolverine-world-live-result: passed') == 3
    assert log.count('oak-expired-stage: passed') == log.count('oak-expired-reload-check: passed') == 2
    assert log.count('creeper-world-live-result: passed') == log.count('creeper-poison-live-result: passed') == 3
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert 'Learned Wolverine: three boots/two reloads' in (STAGE / 'stats-run.log').read_text()
    rank = json.loads((STAGE / 'oak-rank-fixtures.json').read_text())
    assert rank['State']['Birth']['AuraRank'] == 1 and rank['Self']['Rank'] == 2 and rank['Self']['Percent'] == 35 and rank['Life'] == 6407
    saved = json.loads((STAGE / 'oak-casting-fixtures.json').read_text())
    assert saved['State']['Owner'] == saved['Owner'] and saved['State']['Profile'] == 'source-birth-and-totem-motion-v2'
    assert saved['State']['Controller']['Fault'] is None and saved['Body'] != saved['Wolf'] != saved['Raven']
    assert saved['PendingMana'] > 0 and saved['State']['Birth']['Rank'] == 1
    for filename in ['heart-of-wolverine-engine-evidence.json', 'wolverine-aura-engine-evidence.json', 'oak-sage-engine-evidence.json', 'oak-aura-engine-evidence.json', 'totem-ai-engine-evidence.json', 'oak-skill-rank-engine-evidence.json']:
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
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present']) == 169
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present' and x['characterClass'] == 'Druid']) == 13
    assert status['completedClasses'] == []
    client = json.loads((STAGE / 'client-proof.log').read_text())
    assert client['Result'] == 'passed'
    for name in ['Baseline', 'Candidate']:
        row = client[name]
        assert row['Ability'] == 177 and row['Label'] == 'Heart of Wolverine' and row['Class'] == 'Druid'
        assert not row['SelfTarget'] and not row['Left'] and row['Right']
        assert row['Sha256'] == sha(STAGE / 'client/cuo.dll')
    core_path=ROOT / 'tools/reference/wolverine-core-native-evidence.json'
    core=json.loads(core_path.read_text())
    assert core['verified']['passMarkers']==94 and not core['handlerEnabled']
    for path, digest in core['artifacts'].items():
        assert sha(ROOT / path)==digest, path
    heart=json.loads((STAGE / 'wolverine-aura-fixtures.json').read_text())
    assert heart['State']['Birth']['Life256']==34816 and heart['State']['Birth']['Regeneration256']==25
    assert heart['Self']['AttackPercent']==25 and heart['Self']['DamagePercent']==20 and heart['Life']==6407
    cast=json.loads((STAGE / 'wolverine-casting-fixtures.json').read_text())
    assert cast['State']['Birth']['Rank']==1 and cast['State']['Controller']['Fault'] is None
    artifacts = [STAGE / name for name in ['wolverine-aura-fixtures.json', 'wolverine-world-fixtures.json', 'wolverine-casting-fixtures.json', 'stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log',
        'oak-world-fixtures.json', 'oak-sage-body-fixtures.json', 'oak-expired-aura-fixtures.json', 'oak-rank-fixtures.json', 'oak-casting-fixtures.json',
        'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'client-proof.log', 'client-proof/Program.cs', 'client-proof/check.csproj', 'client-proof-build.log', 'client/cuo.dll']]
    artifacts += [ROOT / 'tools/reference' / name for name in ['heart-of-wolverine-preflight.json', 'heart-of-wolverine-engine-evidence.json', 'wolverine-aura-engine-evidence.json', 'wolverine-core-native-evidence.json', 'oak-sage-preflight.json', 'oak-sage-engine-evidence.json', 'totem-ai-engine-evidence.json', 'oak-aura-engine-evidence.json', 'oak-skill-rank-engine-evidence.json']]
    artifacts += [ROOT / name for name in ['tests/check_wolverine.py', 'tests/check_creeper_stats.py', 'tools/record_wolverine_candidate.py', 'tools/probe_oak_skill_rank_reference.py']]
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
        handlers=169, druidHandlers=13, completedClasses=[], goalComplete=False, currentExecutableParity=False,
        verified=dict(boots=3, actualReloads=2, connectedRightCasts=3, connectedQuickCasts=3,
            innateRankSourceComparisons=510, sourceGetterContexts=2040, sourceMotionComparisons=30726, sourceBirthCompositions=180, sourceAuraContexts=440, effectiveRankControls=255,
            naturalWolverineFollowExpiryRuns=3, retainedNaturalOakFollowExpiryRuns=3, retainedRealTimeExpiredOakAuraReloads=2, passMarkers=log.count(': passed'),
            pythonTests=70, namedDecisions=573, namedInputFingerprints=len(audits[0]['InputHashes']), firstLoopInputFingerprints=len(audits[1]['InputHashes'])),
        fullClassRegression=full_class, fullClassBoots=3 if full_class else 0, fullClassPassMarkers=full_markers,
        clientReusedByteIdentical=True, clientMetadataReflection=True, graphicalClientAcceptance=False,
        sourceInputs=inputs['sourceInputs'], copiedData=inputs['copiedData'], testedAssemblies=inputs['artifacts'],
        artifacts={str(path.relative_to(ROOT)): sha(path) for path in artifacts},
        established=[
            'Shared do119birth/base/passives and saved body/controller preserve the actual derived Heart type and old Oak save shape. Heart uses136life/123defense,25percent birth-life scaling,25FP regeneration and232walking rate. Three natural connected movement/aura/expiry runs and two real body/controller/aura/cast reloads pass, with retained Oak/Wolf/Creeper/resource checks.',
            'Separate control162/recipient148stat lists carry AR25+7perrank and physical ED20+7perrank. Highest/equal/lower rank,creator/supplier,5frame refresh/6frame expiry and live NPC Command match the bounded source/native contract. Command changes aura rank,not birth. The retained Oak fixture additionally checks saved NPC Command rank across both reloads.',
            'CombatAuras joins existing additive player,NPC,skeleton,wolf and mapped weapon/skill pools. Might/curse/skill ED is not multiplied twice. Actual converted Valorite sword material fire remains unchanged while physical damage rises. Wolf birth/cold damage and zero physical component remain unchanged; only its AR pool gains the aura. Life/mana/stamina callbacks are not added.',
            'Learned SC/right/quick casting pays19+effective rank mana once after allocation,before shared totem replacement. Fractional/lost mana,early/duplicate release,stop/movement/facet/recovery/freeze,invalid aim,failed allocation and failed post-allocation payment preserve old spirits. Live/deleted target coordinates,town,soft rank and two real reloads with saved binding/unpaid-windup discard pass.',
            'Actual Oak-to-Heart and Heart-to-Oak replacement deletes the previous spirit without deleting wolves/Ravens. Three real right-hold and three quick casts exercise natural windup/release/mana/ground placement. Existing client bytes reflect177/Druid/Heart of Wolverine/right-only metadata; no graphical playtest is inferred.'
        ], remaining=[
            'Item-only/staffmod permission remains refused because persistent permission requires an allocated point. Exceptional NPC equipment/shrine/oskill bonuses,PvP/unmapped recipients and per-producer NPC skill/missile bonus transport remain individual acceptance gaps.',
            'Current-aidel first-think,source target/vision/node scan,native tile/height/ally occupancy/free-position ring/LOS,global event/RNG/resumption ordering and source AStar limitations remain explicit. AStar records a motion fault rather than inventing a path.',
            'Dedicated owner/respec/special bonus and graphical current-executable acceptance remain incomplete. Shared category recognizes Spirit of Barbs,whose actual actor still needs implementation and replacement tests. No class is complete.',
            'Final X3body/emergence/walk/hurt/death,heart_of_wolverine recipient/control effects and spirit/summon audio remain pending. Silent UO wisp0x3A/red0x21,shared SC/sound0x1E5 and text-label talent/right/quick icons are explicitly identified placeholders.'
        ])
    output = ROOT / 'tools/reference/wolverine-native-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Learned Wolverine:169handlers/Druid13,3boots/2reloads/3right+3quick casts; fullClass={full_class}; no installation or class completion claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
