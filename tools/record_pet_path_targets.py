#!/usr/bin/env python3
"""Bind primary target metadata, actual native resets and retained pet acceptance."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/pet-path-targets'
REFERENCE=ROOT/'.local/class-skills/pet-path-targets-reference-20261103'
PARENT='41b222b06fa68baf3c89655278180ac734e6ead9';sha=inventory.sha

def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-1.json',STAGE/(name+'-inputs.json'))
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    prior=json.loads((ROOT/'.local/class-skills/pet-room-links/birth-inputs.json').read_text())
    assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/generic-pet-path-targets.json'}
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/GenericPetNativePlacement.cs','src/Sanctuary.Content/GenericPetPathTargets.cs',
        'tests/Sanctuary.Integration/PetNativePlacementChecks.cs','tests/Sanctuary.Integration/PetPathTargetChecks.cs'}
    sourcepaths=[ROOT/'tools/reference/pet-path-targets-data-evidence.json',ROOT/'tools/reference/pet-path-targets-source-evidence.json']
    data,source=[json.loads(p.read_text()) for p in sourcepaths]
    for receipt in (data,source):
        assert receipt['referenceVersion']=='3.3.93847' and not receipt['handlerEnabled']
        for name,h in receipt['artifacts'].items():assert sha(ROOT/name)==h,name
    assert data['verified']['rows']==751 and data['verified']['compiledTextFlagMatches']==6008 and data['verified']['resetFlag']=='interact'
    assert source['cases']==4826 and source['unsupportedCases']==48 and source['offlineMachineCodeExecuted']
    assert not source['processMemoryReadDuringProbe'] and not source['targetProcessCodeExecuted'] and not source['worldMovementVerified']
    fixture=source['fixture'];assert native['sourceInputs'][fixture['path']]==fixture['sha256']==sha(ROOT/fixture['path'])
    assert len(json.loads((ROOT/fixture['path']).read_text()))==4826
    assert '4826 complete reset/getter cases; 48 unsupported selector controls.' in (STAGE/'source-check.log').read_text()
    oldReceipt=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/pet-room-links-native-evidence.json'],cwd=ROOT))
    baseline=ROOT/'.local/class-skills/pet-room-links/birth-native-checks.log'
    assert sha(baseline)==oldReceipt['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['pet-path-targets-check']+=3
    assert sum(expected.values())==143 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Pet path targets:4826complete reset/getter cases,48unsupported selectors,4586real metadata calls;')==3
    assert log.count('eight monster-target resets and four missing/mismatched-view refusals retain')==3
    assert all(log.count(f'Native pet placement:generation{g},two original actors;')==1 for g in range(3))
    originals=json.loads((STAGE/'goatman-body-fixtures.json').read_text());placements=json.loads((STAGE/'pet-native-placement-fixtures.json').read_text())
    assert len(originals)==len(placements)==2 and {p['Generation'] for p in placements}=={2}
    assert {p['Body'] for p in placements}=={p['Body'] for p in originals}
    for p in placements:
        o=next(o for o in originals if o['Body']==p['Body']);assert all(p[k]==o[k] for k in ('Owner','Body','Life','Mana','Order'))
    saved=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());obstacle=json.loads((STAGE/'pet-obstacle-fixture.json').read_text())
    assert saved['Generation']==obstacle['Generation']==2 and saved['Body']==obstacle['Body'] and saved['Owner']==obstacle['Owner'] and saved['Seed']==obstacle['Seed']
    assert saved['TotalDraws']==10 and saved['Life']==69247 and saved['Mana']==54272
    for name,count in (('named-item-runtime-audit.json',946),('first-loop-affix-audit.json',21)):
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in current['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    auditlog=(STAGE/'audit-native-checks.log').read_text();assert auditlog.count(': passed')==2 and ': failed' not in auditlog and ' ERR]' not in auditlog
    for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-1.log')).read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    # The known initializer callback remains nonblocking, not a fabricated zero.
    blockedpath=ROOT/'.local/class-skills/pet-owner-reference-20261103/owner-callback.json';blocked=json.loads(blockedpath.read_text())
    assert blocked['callbackReference']['callbackRva']=='0x4b08b0' and blocked['bytesRead']==560
    assert not blocked['records'][0]['allUnwindFragmentsCaptured'] and len(blocked['records'][0]['fragments'])==8
    assert all(not f['captured'] and f['permissions']=='---s' for f in blocked['records'][0]['fragments'])
    assert all(r['kind']!='code' for r in blocked['reads'])
    assert not any(blocked[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
    for name,h in blocked['artifacts'].items():assert sha(ROOT/name)==h,name
    diagnostic=REFERENCE/'diagnostic-case-count-1/probe_pet_path_targets.py'
    assert diagnostic.read_text().replace('assert len(rows)==5146','assert len(rows)==4826')==(ROOT/'tools/probe_pet_path_targets.py').read_text()
    files=[Path(__file__).resolve(),*sourcepaths,blockedpath,diagnostic,ROOT/'tests/check_pet_path_targets.py',baseline,
        REFERENCE/'source-run-2.log',ROOT/'.local/class-skills/pet-owner-reference-20261103/diagnostic-unreadable-1/capture_pet_owner_callback.py',
        *(ROOT/'docs'/n for n in ('pet-path-targets-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','source-check.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-1.log','integration-build-1.log','pet-native-placement-fixtures.json','goatman-body-fixtures.json','pet-reconnect-fixture.json','pet-obstacle-fixture.json'))]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Primary monster path-target metadata and actual native resets',
        verified=dict(currentCodeCasesPerBoot=4826,unsupportedSelectorControlsPerBoot=48,compiledTextFlagMatches=6008,metadataCallsPerBoot=4586,
            nativeMarkers=143,retainedNativeMarkers=140,boots=3,reloads=2,saves=3,originalActorsPerBoot=2,newNativeResetsPerBoot=8,newNativeRefusalsPerBoot=4,
            pythonTests=71,namedDecisions=573,auditFingerprints=[946,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Current reset and primary getter use real owned rows. Native placement accepts explicit caller-supplied metadata views; actual target acquisition/selector identity,global world/controller/path birth/mode/timing and combat remain open.',
            'Owner callback code pages are unreadable;only560anchor bytes read and no permissions changed. Full cfg+0x70 producer remains unresolved. No learned handler,class completion,deployment or final-art claim.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    target=ROOT/'tools/reference/pet-path-targets-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Pet path targets:4826current cases per boot;143markers/3boots/2reloads;original actors/71Python/573named decisions/106installed hashes retain;controller and learned casting remain open.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
