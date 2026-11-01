#!/usr/bin/env python3
"""Bind Ice Bolt's player flight/cold increment and retained Sorceress scopes."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-ice-bolt'
sha=inventory.sha


def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-8.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==122 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/'full-run-8.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_ice_bolt import configuration
    config=configuration(True)
    assert len(config['extra_families'])==13 and len(config['extra_checks'])==5
    for name in config['extra_families']:
        assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
    for name in config['extra_checks']:assert counts[name]==3
    for name in ('fire-bolt','ice-bolt'):
        assert counts[name+'-reload-result']==2
        for mode in ('right','quick'):
            assert counts[name+'-'+mode+'-check']==counts[name+'-'+mode+'-result']==3
            assert log.count(name+'-'+mode+': windup')==3
    assert '765independent rank/hard-synergy suppliers' in log
    saved=json.loads((STAGE/'ice-bolt-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Cold']['Source']==saved['Owner']
    assert saved['Flight']['ColdFrames']==150 and saved['Flight']['VelocityFp8']==2304
    parent=ROOT/'tools/reference/fire-bolt-native-evidence.json'
    assert parent.read_bytes()==subprocess.check_output(['git','show','e110adf:'+str(parent.relative_to(ROOT))],cwd=ROOT)
    prior=json.loads(parent.read_text())
    legacy=ROOT/'.local/class-skills/sorceress-fire-bolt/fire-bolt-fixtures.json'
    assert sha(legacy)==prior['artifacts'][str(legacy.relative_to(ROOT))]
    assert 'ColdFrames' not in json.loads(legacy.read_text())['Flight']
    inherited=json.loads((ROOT/'.local/class-skills/sorceress-fire-bolt/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert tested['artifacts'][name]==inherited['artifacts'][name]
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-8.log')).read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
    sizes=[]
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        before=json.loads(subprocess.check_output(['git','show','e110adf:tools/reference/'+name],cwd=ROOT))
        identity=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
        assert {k:v for k,v in audit.items() if k not in identity}=={k:v for k,v in before.items() if k not in identity}
        if 'ContentAssemblyPath' in audit:assert (ROOT/audit['ContentAssemblyPath']).resolve()==(STAGE/'content/Sanctuary.Content.dll').resolve()
        sizes.append(len(audit['InputHashes']))
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    checks=(STAGE/'checks-8.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills',
                    'Class handler coverage, blockers, effects and icons recorded separately.'):
        assert message in checks
    assert 'Traceback' not in checks
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and next(x for x in status['skills'] if x['talentId']=='sorceress.icebolt')['verification']=='partial-native-coverage'
    files=[Path(__file__).resolve(),ROOT/'tests/check_ice_bolt.py',ROOT/'tools/build_skill_roadmap.py',parent,legacy,
        *(ROOT/'docs'/name for name in ('sorceress-ice-bolt-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-8.json','birth-inputs.json','birth-native-checks.log','full-run-8.log','ice-bolt-fixtures.json',
            'audit-inputs.json','audit-native-checks.log','audit-run-8.log','python-tests.log','checks-8.log','content-build-8.log','integration-build-8.log')),
        *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Player Ice Bolt saved flight/cold increment',installed=False,
        graphicalClientVerified=False,completedClasses=[],verified=dict(nativeMarkers=122,boots=3,actualReloads=2,saves=3,
            independentRankHardSuppliers=765,iceBoltRightQuickActions=6,retainedFireBoltRightQuickActions=6,
            iceBoltPostLoadFlights=2,fixtureFamilies=13,sharedChecks=5,auditMarkers=4,auditFingerprints=sizes,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Ice Bolt retains its own50frame/12velocity,FP endpoints,three-mana payment and captured cold length while sharing Fire Bolt flight/collision/save services.',
            'Actual Nightwing removal/new-cast,hard synergy,typed potion/cold immunity,resistance,difficulty/length and Slow Missiles/lifecycle controls pass. Cold states and generic Boulder potion helper are reused unchanged.',
            'Six actual Ice Bolt inputs hit14tiles away and apply source-owned cold. Two actual reloads retain exact original flight,wounds,cold source/deadline,mana/bindings and resume independent copies at13tiles. Separate loaded0/80/2000ms controls verify harmless frame aging without movement or replay; the resumed real-time fixture uses the existing next-loop clock guard.',
            'The accepted pre-extension Fire Bolt JSON lacks ColdFrames and loads as zero. This is old-schema component evidence plus current native saves,not an old-DLL world upgrade. Existing Fire Bolt and full Sorceress baseline scopes pass in the same run.'],
        remaining=['Modern executable/initial factory/zero-aim/creator-RNG,room/terrain/candidate order/native footprint and owner-removal parity remain open;host harmless restart/offline aging and cancellation are explicit adapters.',
            'Ordinary-chill shatter,exact modern late-gear/pierce timing,complete recipient/special-stat/PvP defense,old-DLL world upgrade,unheld mid-flight restart and graphical acceptance remain unverified. Effects/audio and talent/hotkey icons stay separately pending.',
            'Ice Blast/Glacial Spike require their own hit/freeze/explosion contracts. Other Sorceress baseline and passive scopes remain partial;Inferno modern held-input/payment/release is individually deferred. No skill or class certificate is inferred.'])
    output=ROOT/'tools/reference/ice-bolt-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Ice Bolt:122native markers/3boots/2reloads,six real cold casts,four audits/71Python;no class certificate or deployment.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
