#!/usr/bin/env python3
"""Bind prepared player Chain Lightning and retained historical/NPC scopes."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/sorceress-player-chain'
BUILD = 4
PARENT = '9fd265f'
sha = inventory.sha


def historical(name):
    return subprocess.check_output(['git', 'show', PARENT + ':' + name], cwd=ROOT)


def record(check=False):
    inventory.STAGE = STAGE
    tested = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    snapshot(STAGE / f'pre-build-{BUILD}.json', STAGE / 'birth-inputs.json')
    counts = markers(STAGE / 'birth-native-checks.log')
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert sum(counts.values()) == 310 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_player_chain import configuration
    config = configuration(True)
    assert len(config['extra_families']) == 27 and len(config['extra_checks']) == 6
    for family in config['extra_families']:
        assert counts[family + '-check'] == 1 and counts[family + '-reload-check'] == 2
    for name in config['extra_checks']:
        assert counts[name] == 3
    spells = ('fire-bolt', 'ice-bolt', 'ice-blast', 'glacial-spike', 'fire-ball', 'nova', 'frost-nova', 'static-field', 'lightning-flight', 'charged-flight', 'player-chain')
    for spell in spells:
        assert counts[spell + '-reload-result'] == 2
        for mode in ('right', 'quick'):
            assert counts[spell + '-' + mode + '-check'] == counts[spell + '-' + mode + '-result'] == 3
            assert log.count(' ' + spell + '-' + mode + ': windup') == 3
    for name in ('mage-glacial-live', 'glacial-player-live', 'mage-static-live','mage-chain-live','player-chain-offline'):
        assert counts[name + '-result'] == 3
    assert counts['sorceress-passives-reload-result'] == 2
    assert 'Prepared player Chain:captured root/live retained-rank children' in log
    saved = json.loads((STAGE / 'player-chain-fixtures.json').read_text())
    assert saved['Generation'] == 2
    flight=saved['Flight'];packet=flight['PlayerPacket']
    assert flight['Rank']==1 and flight['Remaining']==5 and flight['Frame']==2 and flight['NpcCaster']==0 and flight['NpcSeed']==0
    assert flight['Min256']==256 and flight['Max256']==11059 and packet['VelocityFp8']==5760
    assert packet['Slow']==dict(VelocityPercent=100,DamagePercent=100) and len(packet['Steps']['Cells'])>=1
    source=ROOT/'tools/reference/player-chain-source-reuse.json';reference=json.loads(source.read_text())
    assert reference['referenceVersion']=='3.3.93847' and len(reference['mappings'])==5 and len(reference['reviewedBodies'])==2
    assert 'no new compiled' in reference['kind']
    for mapping in reference['mappings']:assert sha(ROOT/mapping['canonical'])==mapping['sha256']
    for path,digest in reference['inputHashes'].items():assert sha(ROOT/path)==digest,path
    for kind in ('content', 'integration'):
        build = (STAGE / f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    parent = ROOT / 'tools/reference/charged-flight-native-evidence.json'
    assert parent.read_bytes() == historical(str(parent.relative_to(ROOT)))
    prior = json.loads((ROOT / '.local/class-skills/sorceress-charged-flight/birth-inputs.json').read_text())
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert tested['artifacts'][name] == prior['artifacts'][name]
    assert tested['copiedData'].keys() == prior['copiedData'].keys()
    assert {p for p in tested['copiedData'] if tested['copiedData'][p] != prior['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    status = ROOT / 'data/Sanctuary/Balance/class-skill-status.json'
    now = json.loads(status.read_text()); before = json.loads(historical(str(status.relative_to(ROOT))))
    updated = next(x for x in now['skills'] if x['talentId'] == 'sorceress.chainlightning')
    old = next(x for x in before['skills'] if x['talentId'] == 'sorceress.chainlightning')
    assert old['verification'] == updated['verification'] == 'partial-native-coverage'
    old['remaining'] = updated['remaining']
    assert before == now and not now['completedClasses']
    audited = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    assert audited['artifacts'] == tested['artifacts'] and audited['copiedData'] == tested['copiedData']
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    assert ' ERR]' not in (STAGE / 'audit-native-checks.log').read_text()
    fingerprints = []
    for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        prior_audit = json.loads(historical('tools/reference/' + name))
        identity = ('InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath')
        assert {k: v for k, v in audit.items() if k not in identity} == {k: v for k, v in prior_audit.items() if k not in identity}
        if 'ContentAssemblyPath' in audit:
            assert (ROOT / audit['ContentAssemblyPath']).resolve() == (STAGE / 'content/Sanctuary.Content.dll').resolve()
        fingerprints.append(len(audit['InputHashes']))
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    checks = (STAGE / f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for', 'Eight classes, 240 source skills', 'Class handler coverage, blockers, effects and icons recorded separately.'):
        assert message in checks
    assert 'Traceback' not in checks
    recheck=(STAGE/'source-recheck.log').read_text();assert 'Chain reference:5 canonical source identities/2 exact reviewed bodies' in recheck and 'Traceback' not in recheck
    diagnostic=STAGE/'diagnostic-4/birth-native-checks.log';diagnostic_log=diagnostic.read_text()
    focused=inventory.validate_inventory(STAGE/'diagnostic-4/birth-inputs.json')
    assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
    assert sum(markers(diagnostic).values())==47 and ' ERR]' not in diagnostic_log and ': failed' not in diagnostic_log
    assert diagnostic_log.count('Shutting down')==diagnostic_log.count('Writing world save snapshot done')==3
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    files = [Path(__file__).resolve(), ROOT / 'tests/check_player_chain.py', ROOT / 'tools/review_player_chain_source.py', source, ROOT / 'tools/build_skill_roadmap.py', ROOT / 'tools/reference/chain-lightning-engine-evidence.json', ROOT / 'tools/reference/mage-chain-native-evidence.json', parent,
             *(ROOT / 'docs' / name for name in ('sorceress-player-chain-checkpoint.md', 'sorceress-chain-flight-preflight.md', 'sorceress-thunder-storm-preflight.md', 'class-skills-resume-checkpoint.md', 'class-skill-roadmap.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in (f'pre-build-{BUILD}.json', 'birth-inputs.json', 'birth-native-checks.log', f'full-run-{BUILD}.log', 'player-chain-fixtures.json', 'source-recheck.log', 'diagnostic-4/birth-native-checks.log', 'diagnostic-4/birth-inputs.json', 'diagnostic-run-4.log', 'audit-inputs.json', 'audit-native-checks.log', f'audit-run-{BUILD}.log', 'python-tests.log', f'checks-{BUILD}.log', f'content-build-{BUILD}.log', f'integration-build-{BUILD}.log')),
             *(ROOT / 'tools/reference' / name for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'))]
    result = dict(referenceVersion='3.3.93847', component='Prepared player Chain Lightning with historical/NPC separation', installed=False, graphicalClientVerified=False, completedClasses=[],
                  verified=dict(nativeMarkers=310,focusedMarkers=47,boots=3,actualReloads=2,saves=3,retainedRealInputActions=60,playerChainRealInputActions=6,postLoadPlayerChains=2,actualDisconnectControls=3,naturalMageChainSessions=3,sourceIdentityMappings=5,exactReviewedBodies=2,fixtureFamilies=27,sharedChecks=6,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
                  markers=dict(sorted(counts.items())), artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=['New player root/child segments use shared source do1/Size2/ordered cells and prepared typed elemental impact. Root capture and fresh child rank-preserving mastery/synergy/gear/Slow Missiles, cyclic targeting/finite count/cast-local delays, contact-time four-channel potion and immunity separation pass.',
                               'Deep-copied delay/step arrays, atomic malformed restore, owner lifecycle/no-replay aging, actual disconnect/input and two resumed partial source flights pass. Shared basic/cold impact callers and natural Mage fixed10 Chain sessions remain covered.',
                               'Historical player V0 records without PlayerPacket retain their previous delivery/cadence. Old chain-lightning fixture scopes are explicitly historical; new player-chain fixtures establish current behavior. Five canonical source hashes/two reviewed bodies match prior references; original older compiled outputs are retained,not re-executed.',
                               'Only Chain remaining-work metadata changes runtime data. Item/affix decisions,kernel and106 installed artifacts remain unchanged. No class certificate/deployment; final effects/audio and icons stay separate.'],
                  remaining=['Complete modern initial dispatch/global RNG/factory/events,NHD/filter/room/candidate order,world/path geometry,PvP/Sunder/special recipients,graphical and actual old-DLL/unheld concurrent save acceptance remain open.',
                             'Shared12tile aim and player owner-removal/no-replay policy remain declared host adapters. Historical packet behavior is compatibility scope,not modern parity. Continue remaining Sorceress contracts;Inferno held-input/payment/release is separate unfinished work.'])
    output = ROOT / 'tools/reference/player-chain-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Player Chain:310 native markers/three boots/two reloads, six real inputs/two resumed flights/three actual disconnects, four audits/71 Python; installed artifacts preserved; no class certificate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
