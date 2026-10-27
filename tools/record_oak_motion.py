#!/usr/bin/env python3
"""Bind source Totem policy to saved native motion and real aura expiry acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/oak-motion'


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
    assert log.count('totem-ai-check: passed') == 1
    assert '15363native ordered decision comparisons pass' in log
    for name in ['oak-world', 'oak-sage-body', 'battle-orders', 'spirit-wolf-world', 'creeper-world', 'creeper-poison', 'creeper-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    assert log.count('oak-world-live-result: passed') == 3
    assert log.count('oak-expired-stage: passed') == log.count('oak-expired-reload-check: passed') == 2
    assert log.count('creeper-world-live-result: passed') == log.count('creeper-poison-live-result: passed') == 3
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert 'three connected natural follow/expiry sessions' in (STAGE / 'stats-run.log').read_text()
    saved = json.loads((STAGE / 'oak-world-fixtures.json').read_text())
    state = saved['State']; controller = state['Controller']
    assert state['Owner'] == saved['Owner'] and state['Profile'] == 'source-birth-and-totem-motion-v2'
    assert controller['Profile'] == 'source-totem-native-ground-v1'
    assert controller['EntryTiming'] == 'current-aidel-first-think-provisional-v1'
    assert controller['Fault'] is None and controller['Frame'] == 84 and controller['Thinks'] > 0
    assert state['NextRecoveryUtc'] == controller['NextUtc'] and saved['Life'] == 6435
    expired = json.loads((STAGE / 'oak-expired-aura-fixtures.json').read_text())
    assert expired['SavedWithAura'] and expired['Completed']
    assert expired['OwnerBaseMaximum'] == 51712 and expired['OwnerMaximum'] == 67072
    assert expired['BodyBaseMaximum'] == 15360 and expired['BodyMaximum'] == 19968
    assert expired['OwnerLife'] == 33543 and expired['BodyLife'] == 6407
    source = json.loads((ROOT / 'tools/reference/totem-ai-engine-evidence.json').read_text())
    assert source['fixture']['cases'] == 5121 and sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
    assert sha(ROOT / '.local/class-skills/totem-ai-source-probe.cpp') == source['fixture']['generatedSourceSha256']
    assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / source['sourcePath']) == source['sourceSha256']
    for helper in source['reviewedHelpers']:
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / helper['path']) == helper['sha256']
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        audits.append(audit)
    baseline_definitions = json.loads(subprocess.check_output(['git', 'show', '8cf01d9:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))['Definitions']
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline_definitions
    baseline_path = ROOT / 'tools/reference/oak-sage-world-native-evidence.json'
    historical = subprocess.check_output(['git', 'show', 'afc68f4:tools/reference/oak-sage-world-native-evidence.json'], cwd=ROOT)
    assert baseline_path.read_bytes() == historical
    baseline = json.loads(historical)
    assert baseline['verified']['fullClassPassMarkers'] == 1181
    # Freeze the previous full resource-regression package. Do not imply that
    # its1181markers came from this new motion package.
    assert sha(ROOT / '.local/class-skills/oak-sage/content/Sanctuary.Content.dll') == baseline['testedAssemblies']['Assemblies/Sanctuary.Content.dll']
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log',
        'oak-world-fixtures.json', 'oak-sage-body-fixtures.json', 'oak-expired-aura-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']]
    artifacts += [ROOT / 'tools/reference' / name for name in ['oak-sage-preflight.json', 'totem-ai-engine-evidence.json', 'oak-aura-engine-evidence.json']]
    artifacts += [ROOT / name for name in ['tests/check_oak_motion.py', 'tools/record_oak_motion.py']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        scope='Actual saved Totem movement and retained Oak life aura; learned casting/shared spirit replacement remain unfinished.',
        verified=dict(sourceContexts=5121, nativeDecisionComparisons=15363, boots=3, reloads=2,
            naturalConnectedFollowExpiryRuns=3, realTimeExpiredAuraReloads=2, passMarkers=log.count(': passed'),
            pythonTests=70, namedDecisions=573, namedInputFingerprints=len(audits[0]['InputHashes']), firstLoopInputFingerprints=len(audits[1]['InputHashes'])),
        sourceInputs=inputs['sourceInputs'], copiedData=inputs['copiedData'], testedAssemblies=inputs['artifacts'],
        artifacts={str(path.relative_to(ROOT)): sha(path) for path in artifacts},
        priorResourceRegression=dict(commit='afc68f4', evidence='tools/reference/oak-sage-world-native-evidence.json',
            sha256=sha(baseline_path), fullClassPassMarkers=1181,
            scope='Previous resource-only package, frozen separately at oak-sage. Full class regression is not repeated or claimed for the new oak-motion package.'),
        established=[
            'Complete source Totem decisions match15363native ordered service contexts across current difficulty rows. World motion reuses SourcePetMovement,SourceActorPath,SourceActorMotion,PetOwnerRoutes and clamped unit-distance helpers; native follower AI/melee remain disabled.',
            'Actual following, chance-gated retreat, free-position owner teleport and25frame post-teleport idle are exercised. Velocity reads current Vigor and movement slow each walking frame. Frozen/parked/offline contexts skip motion/RNG; owner facet transport uses legal shared native placement and source difficulty rescale.',
            'Saved controller retains source pose/path points,seed/frame,decision counters and owner/history state. Caller path arrays are copied; malformed state is refused atomically. Old body-only JSON remains valid without inventing movement or losing birth data.',
            'Three connected natural sessions observe automatic following and the life bonus,delete the emitter and observe real timer expiry restoring the owner maximum. Two other real six-frame leases are confirmed active at save and expired after actual restart; exact current-life fractions and source regeneration return without healing or replay.',
            'Both actual controller reloads retain saved state and injury,discard inactive frames without RNG/actions/healing and resume exactly one active recovery frame. The retained body/aura,Battle Orders,Wolf,Creeper/Raven/item consumers remain green.'
        ], adapters=[
            'current-aidel-first-think-provisional-v1uses the current15frame table aidel for initial entry. This is labelled inference, not a verified modern initializer/event schedule.',
            'Current target wrapper includes24and retains supplied combat on a null result. The live target/vision/node scan remains the existing native closest/retained/LOS/serial projection; pure source services do not certify that adapter.',
            'Source teleport requests size2/mask0x1C09free coordinates. Native tile/16height/friendly occupancy and shared ring/LOS placement approximate source room/collision geometry. Source AStar fallback is explicitly unsupported and saves a fault instead of inventing a path.',
            'A motion fault does not invent another AI or suppress the independent supported life/recovery/aura consumers. Full current event/global RNG/equal-deadline/animation/room/path ordering remains unverified.'
        ], remaining=[
            'Implement shared totem cap/replacement and successful allocation/payment rollback,then learned SC/FCR cancellation/right/quick input and actual client acceptance.',
            'Complete Battle Command and exceptional summon equipment/staffmod-only/PvP/unmapped recipient contexts. Resolve or retain explicit current first-think/target/path/geometry limits before claiming full parity.',
            'Run appropriate learned-cast/full regression and preserved installation before rollout; current installed main remains167handlers/Druid11.',
            'Final XWbody/emergence/movement/hurt/death,recipient/control overlays and audio are pending effects. Talent/right/quick icons remain separate text-label placeholders.'
        ])
    output = ROOT / 'tools/reference/oak-motion-native-evidence.json'; rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Oak motion:15363source/native decisions,3boots/2reloads/3natural follow-expiry runs/2real expired-lease reloads; learned casting remains disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
