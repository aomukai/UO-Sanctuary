#!/usr/bin/env python3
"""Bind saved player Charged Bolt volley evidence without certifying the class."""
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
STAGE = ROOT / '.local/class-skills/sorceress-charged-flight'
BUILD = 2
PARENT = '00b5f1a'
sha = inventory.sha


def historical(name):
    return subprocess.check_output(['git', 'show', PARENT + ':' + name], cwd=ROOT)


def record(check=False):
    inventory.STAGE = STAGE
    tested = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    snapshot(STAGE / f'pre-build-{BUILD}.json', STAGE / 'birth-inputs.json')
    counts = markers(STAGE / 'birth-native-checks.log')
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert sum(counts.values()) == 284 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_charged_flight import configuration
    config = configuration(True)
    assert len(config['extra_families']) == 25 and len(config['extra_checks']) == 6
    for family in config['extra_families']:
        assert counts[family + '-check'] == 1 and counts[family + '-reload-check'] == 2
    for name in config['extra_checks']:
        assert counts[name] == 3
    spells = ('fire-bolt', 'ice-bolt', 'ice-blast', 'glacial-spike', 'fire-ball', 'nova', 'frost-nova', 'static-field', 'lightning-flight', 'charged-flight')
    for spell in spells:
        assert counts[spell + '-reload-result'] == 2
        for mode in ('right', 'quick'):
            assert counts[spell + '-' + mode + '-check'] == counts[spell + '-' + mode + '-result'] == 3
            assert log.count(' ' + spell + '-' + mode + ': windup') == 3
    for name in ('mage-glacial-live', 'glacial-player-live', 'mage-static-live'):
        assert counts[name + '-result'] == 3
    assert counts['sorceress-passives-reload-result'] == 2
    assert 'Player Charged Bolt:765rank/hard goldens,32rank/direction emissions' in log
    saved = json.loads((STAGE / 'charged-flight-fixtures.json').read_text())
    assert saved['Generation'] == 2 and len(saved['Flights']) == 3
    for ordinal, flight in enumerate(saved['Flights']):
        assert flight['Frames'] == 77 and flight['Frame'] == 7 and flight['VelocityFp8'] == 2304
        assert flight['Minimum256'] == 512 and flight['Maximum256'] == 1024
        assert flight['ColdFrames'] == 0 and flight['RadialIndex'] == -1 and flight['Cast'] == 0
        assert flight['Charged']['Ordinal'] == ordinal and flight['Charged']['Next'] > 1 and len(flight['Charged']['Path']) == 39
    source = ROOT / 'tools/reference/player-charged-bolt-emitter-source.json'
    evidence = json.loads(source.read_text())
    assert evidence['cases'] == 800 and evidence['referenceVersion'] == '3.3.93847' and evidence['sanitizers'] == 'ASan/UBSan clean'
    for path, digest in evidence['artifacts'].items():
        assert sha(ROOT / path) == digest, path
    for kind in ('content', 'integration'):
        build = (STAGE / f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    parent = ROOT / 'tools/reference/lightning-flight-native-evidence.json'
    assert parent.read_bytes() == historical(str(parent.relative_to(ROOT)))
    prior = json.loads((ROOT / '.local/class-skills/sorceress-lightning-flight/birth-inputs.json').read_text())
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert tested['artifacts'][name] == prior['artifacts'][name]
    assert tested['copiedData'].keys() == prior['copiedData'].keys()
    assert {p for p in tested['copiedData'] if tested['copiedData'][p] != prior['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    status = ROOT / 'data/Sanctuary/Balance/class-skill-status.json'
    now = json.loads(status.read_text()); before = json.loads(historical(str(status.relative_to(ROOT))))
    updated = next(x for x in now['skills'] if x['talentId'] == 'sorceress.chargedbolt')
    old = next(x for x in before['skills'] if x['talentId'] == 'sorceress.chargedbolt')
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
    reuse = (STAGE / 'reuse-recheck.log').read_text()
    assert 'Player Charged Bolt: do17 reviewed' in reuse and 'Traceback' not in reuse
    diagnostic = STAGE / 'diagnostic-1/birth-native-checks.log'
    diagnostic_log = diagnostic.read_text()
    focused = json.loads((STAGE / 'diagnostic-1/birth-inputs.json').read_text())
    assert focused['artifacts'] == tested['artifacts']
    for path, digest in focused['sourceInputs'].items():
        assert sha(ROOT / path) == digest, path
    assert focused['copiedData'] == prior['copiedData']
    # Focused run predates only the accepted remaining-work annotation update.
    assert set(focused['copiedData']) == set(tested['copiedData'])
    assert sum(markers(diagnostic).values()) == 32 and ' ERR]' not in diagnostic_log and ': failed' not in diagnostic_log
    assert diagnostic_log.count('Shutting down') == diagnostic_log.count('Writing world save snapshot done') == 3
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    files = [Path(__file__).resolve(), ROOT / 'tests/check_charged_flight.py', ROOT / 'tools/probe_player_charged_bolt_source.py', source, ROOT / 'tools/build_skill_roadmap.py', ROOT / 'tools/review_player_charged_bolt_reuse.py', ROOT / 'tools/reference/player-charged-bolt-reuse-review.json', parent,
             *(ROOT / 'docs' / name for name in ('sorceress-charged-flight-checkpoint.md', 'sorceress-chain-flight-preflight.md', 'sorceress-lightning-flight-preflight.md', 'class-skills-resume-checkpoint.md', 'class-skill-roadmap.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in (f'pre-build-{BUILD}.json', 'birth-inputs.json', 'birth-native-checks.log', f'full-run-{BUILD}.log', 'charged-flight-fixtures.json', 'reuse-recheck.log', 'diagnostic-1/birth-native-checks.log', 'diagnostic-1/birth-inputs.json', 'diagnostic-run-1.log', 'audit-inputs.json', 'audit-native-checks.log', f'audit-run-{BUILD}.log', 'python-tests.log', f'checks-{BUILD}.log', f'content-build-{BUILD}.log', f'integration-build-{BUILD}.log')),
             *(ROOT / 'tools/reference' / name for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'))]
    result = dict(referenceVersion='3.3.93847', component='Player Charged Bolt saved independent volleys', installed=False, graphicalClientVerified=False, completedClasses=[],
                  verified=dict(nativeMarkers=284, focusedMarkers=32, boots=3, actualReloads=2, saves=3, retainedRealInputActions=54, chargedRealInputActions=6, postLoadChargedVolleys=2, postLoadChargedFlights=6, sourceEmitterCases=800, nativeRankGoldens=765, nativeEmissionContexts=32, fixtureFamilies=25, sharedChecks=6, auditMarkers=4, auditFingerprints=fingerprints, pythonTests=71, unchangedInstalledArtifacts=106),
                  markers=dict(sorted(counts.items())), artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=['Pinned source38/do17/missile56 emission reuses the complete older800-case emitter proof and exact initializer45/path2160 evidence. Range98 is capped to77 by the reused initializer; count is3at rank1 and caps24.',
                               'Independent saved paths and damage packets capture ranks/gear/positive mastery/Slow Missiles, with live potion/resistance and once-only volley payment.765 rank goldens,32 emission contexts, deep-copy/atomic restore, collision/lifecycle, six connected inputs and two resumed three-bolt volleys pass.',
                               'Only Charged Bolt remaining-work metadata changes runtime data. Kernel, item/affix audit decisions and106 installed artifacts remain unchanged. Final effects/audio and icons stay separate.'],
                  remaining=['Complete modern factory/initial dispatch/global RNG/events, native room/path rebuild/coordinate projection/candidate order, PvP/Sunder/special recipients, graphical and actual old-DLL/unheld concurrent save acceptance remain open.',
                             'Shared12tile aim admission, deterministic aim-X/ordinal seeding, native movement and offline no-replay aging retain their declared adapter scopes. Inferno held-input/payment/release is separate unfinished work. No class certificate or deployment.'])
    output = ROOT / 'tools/reference/charged-flight-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Charged Bolt:284 native markers/three boots/two reloads, six real inputs/two resumed volleys, four audits/71 Python; installed artifacts preserved; no class certificate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
