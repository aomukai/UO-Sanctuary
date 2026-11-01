#!/usr/bin/env python3
"""Bind Thunder Storm live pulse, prepared source and connected lifecycle acceptance."""
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
STAGE = ROOT / '.local/class-skills/sorceress-storm-pulse'
BUILD = 4
PARENT = '449ddc9'
sha = inventory.sha


def historical(name):
    return subprocess.check_output(['git', 'show', PARENT + ':' + name], cwd=ROOT)


def record(check=False):
    inventory.STAGE = STAGE
    tested = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    snapshot(STAGE / f'pre-build-{BUILD}.json', STAGE / 'birth-inputs.json')
    counts = markers(STAGE / 'birth-native-checks.log')
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert sum(counts.values()) == 330 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_storm_pulse import configuration
    config = configuration(True)
    assert len(config['extra_families']) == 28 and len(config['extra_checks']) == 6
    for family in config['extra_families']:
        assert counts[family + '-check'] == 1 and counts[family + '-reload-check'] == 2
    for name in config['extra_checks']:
        assert counts[name] == 3
    spells = ('fire-bolt', 'ice-bolt', 'ice-blast', 'glacial-spike', 'fire-ball', 'nova', 'frost-nova', 'static-field', 'lightning-flight', 'charged-flight', 'player-chain', 'storm-pulse')
    for spell in spells:
        assert counts[spell + '-reload-result'] == 2
        for mode in ('right', 'quick'):
            assert counts[spell + '-' + mode + '-check'] == counts[spell + '-' + mode + '-result'] == 3
            assert log.count(' ' + spell + '-' + mode + ': windup') == 3
    for name in ('mage-glacial-live', 'glacial-player-live', 'mage-static-live','mage-chain-live','player-chain-offline','storm-pulse-offline'):
        assert counts[name + '-result'] == 3
    assert counts['sorceress-passives-reload-result'] == 2
    assert 'Thunder Storm:765independent rank/hard goldens' in log
    saved=json.loads((STAGE/'storm-pulse-fixtures.json').read_text());storm=saved['Storm']
    assert saved['Generation']==2 and saved['Orb']>0 and saved['Target']>0 and saved['Life']<12800000 and saved['Mana']==54272
    assert storm['Rank']>1 and storm['RadiusSubtiles']==17 and storm['LastTarget']==saved['Target']
    assert storm['IntervalFrames']==125-min(100,110*storm['Rank']//(storm['Rank']+6))
    source=ROOT/'tools/reference/thunder-storm-source-evidence.json';reference=json.loads(source.read_text())
    assert reference['referenceVersion']=='3.3.93847' and reference['startCases']==4 and reference['periodicCases']==5376 and reference['sanitizers']=='ASan/UBSan clean'
    for path,digest in reference['artifacts'].items():assert sha(ROOT/path)==digest,path
    for path,digest in reference['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/path)==digest,path
    skills=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';missiles=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json'
    row=next(x for x in json.loads(skills.read_text()).values() if x.get('skill')=='Thunder Storm')
    assert (row['*Id'],row['srvstfunc'],row['srvdofunc'],row['Param1'],row['Param2'],row['Param7'])==(57,13,29,3600,600,17)
    missile=next(x for x in json.loads(missiles.read_text()).values() if x.get('Missile')=='thunderstorm1')
    assert missile['*ID']==166 and missile.get('CanSlow',0)==0 and missile['pSrvDoFunc']==3
    for kind in ('content', 'integration'):
        build = (STAGE / f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    parent = ROOT / 'tools/reference/player-chain-native-evidence.json'
    assert parent.read_bytes() == historical(str(parent.relative_to(ROOT)))
    prior = json.loads((ROOT / '.local/class-skills/sorceress-player-chain/birth-inputs.json').read_text())
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert tested['artifacts'][name] == prior['artifacts'][name]
    assert tested['copiedData'].keys() == prior['copiedData'].keys()
    assert {p for p in tested['copiedData'] if tested['copiedData'][p] != prior['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    status = ROOT / 'data/Sanctuary/Balance/class-skill-status.json'
    now = json.loads(status.read_text()); before = json.loads(historical(str(status.relative_to(ROOT))))
    updated = next(x for x in now['skills'] if x['talentId'] == 'sorceress.thunderstorm')
    old = next(x for x in before['skills'] if x['talentId'] == 'sorceress.thunderstorm')
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
    diagnostic=STAGE/'diagnostic-3/birth-native-checks.log';diagnostic_log=diagnostic.read_text()
    focused=json.loads((STAGE/'diagnostic-3/birth-inputs.json').read_text())
    assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==prior['copiedData']
    for path,digest in focused['sourceInputs'].items():assert sha(ROOT/path)==digest,path
    assert sum(markers(diagnostic).values())==41 and ' ERR]' not in diagnostic_log and ': failed' not in diagnostic_log
    assert diagnostic_log.count('Shutting down')==diagnostic_log.count('Writing world save snapshot done')==3
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    files = [Path(__file__).resolve(), ROOT / 'tests/check_storm_pulse.py', ROOT / 'tools/probe_thunder_storm_source.py', source,skills,missiles, ROOT / 'tools/build_skill_roadmap.py', ROOT / 'tools/reference/slow-missiles-observation-evidence.json', ROOT / 'tools/reference/slow-missiles-component-native-evidence.json', parent,
             *(ROOT / 'docs' / name for name in ('sorceress-storm-pulse-checkpoint.md', 'sorceress-thunder-storm-preflight.md', 'sorceress-telekinesis-preflight.md', 'class-skills-resume-checkpoint.md', 'class-skill-roadmap.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in (f'pre-build-{BUILD}.json', 'birth-inputs.json', 'birth-native-checks.log', f'full-run-{BUILD}.log', 'storm-pulse-fixtures.json', 'diagnostic-3/birth-native-checks.log', 'diagnostic-3/birth-inputs.json', 'diagnostic-run-3.log', 'audit-inputs.json', 'audit-native-checks.log', f'audit-run-{BUILD}.log', 'python-tests.log', f'checks-{BUILD}.log', f'content-build-{BUILD}.log', f'integration-build-{BUILD}.log')),
             *(ROOT / 'tools/reference' / name for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'))]
    result = dict(referenceVersion='3.3.93847', component='Thunder Storm live pulse and connected lifecycle', installed=False, graphicalClientVerified=False, completedClasses=[],
                  verified=dict(nativeMarkers=330,focusedMarkers=41,boots=3,actualReloads=2,saves=3,retainedRealInputActions=66,stormRealInputActions=6,postLoadStormPulses=2,actualStormDisconnectControls=3,nativeRankGoldens=765,sourceStartCases=4,sourcePeriodicCases=5376,fixtureFamilies=28,sharedChecks=6,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
                  markers=dict(sorted(counts.items())), artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=['Complete older start13/do29/shared-wrapper4+5376 cases retain buff/recast versus strike behavior,fresh target-position flags1 factory requests,original rank,target memory and town/factory gates. Full damage/interpreter/current executable are explicit external services.',
                               '765rank/hard goldens,actual live mastery/Static synergy/Eschuta removal,retained rank/timing,shared CanSlow0 damage/typed potion and immunity lanes,serial cycling/radius and once19mana pass. Connected natural timing,actual right/quick/disconnect,deadlines/no replay/expiry and two saved owner/original-gear/mana/wound pulses pass.',
                               'T09gameplay recovery is unchanged;only StormPulseSaved uses the existing fixture-only mana eligibility gate. Held inspection deadlines/recipient updates isolate transport without restoring wounds/resources. Initial connected timing/mana diagnostics and binaries are retained separately.',
                               'Only Storm remaining-work metadata changes runtime data. Item/affix decisions,kernel and106 installed artifacts remain unchanged. Effects/audio and icons stay separate;no class certificate/deployment.'],
                  remaining=['Exact modern Slow Missiles damage-consumer order,target filters/geometry/order,factory/global RNG/events/initial cadence,special/PvP/Sunder recipients,staffmod/oskill removal,graphical and actual old-DLL/unheld concurrent saves remain open.',
                             'Connected/internalized no-action/no-replay and target-memory preservation remain explicit host lifecycle policies,not proof of D2R caster-removal rules. Continue remaining Sorceress contracts;Inferno held-input/payment/release is separate unfinished work.'])
    output = ROOT / 'tools/reference/storm-pulse-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Thunder Storm:330 native markers/three boots/two reloads, six real inputs/two resumed pulses/three actual disconnects, four audits/71 Python; installed artifacts preserved; no class certificate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
