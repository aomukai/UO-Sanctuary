#!/usr/bin/env python3
"""Record actual native placement of retained prepared actors;scope stays explicit."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/pet-native-placement'
PARENT='e33f129c277480668f07bcb7ca45bf715ca4b3af';sha=inventory.sha
def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-3.json',STAGE/(name+'-inputs.json'))
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/pet-collision-writes/birth-inputs.json').read_text());assert native['copiedData']==prior['copiedData']
    assert {n for n,h in native['sourceInputs'].items() if n.startswith(('src/','shared/')) and prior['sourceInputs'].get(n)!=h}=={
        'src/Sanctuary.Content/GenericPetMonsterPlacementHooks.cs','src/Sanctuary.Content/GenericPetRoomLinks.cs','src/Sanctuary.Content/GenericPetNativePlacement.cs'}
    assert {n for n,h in native['sourceInputs'].items() if n.startswith('tests/') and n.endswith('.cs') and prior['sourceInputs'].get(n)!=h}=={'tests/Sanctuary.Integration/PetNativePlacementChecks.cs'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    sourcepath=ROOT/'tools/reference/pet-world-placement-reference-evidence.json';source=json.loads(sourcepath.read_text())
    assert source['referenceVersion']=='3.3.93847' and source['routineBytes']==4406 and source['totalReadBytes']==11052 and len(source['routineRecords'])==8
    assert not any(source[k] for k in ('processMemoryReadDuringCheck','machineCodeExecuted','mechanicalParityVerified','handlerEnabled'))
    for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name
    assert 'new raw-code differential acceptance remains open' in (STAGE/'source-check-2.log').read_text()
    baseline=ROOT/'.local/class-skills/pet-collision-writes/birth-native-checks.log'
    oldReceipt=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/pet-collision-writes-native-evidence.json'],cwd=ROOT));assert sha(baseline)==oldReceipt['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['pet-native-placement-check']+=1;expected['pet-native-placement-reload-check']+=2
    assert sum(expected.values())==134 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert all(log.count(f'Native pet placement:generation{g},two original actors;')==1 for g in range(3))
    originals=json.loads((STAGE/'goatman-body-fixtures.json').read_text());placements=json.loads((STAGE/'pet-native-placement-fixtures.json').read_text())
    assert len(originals)==len(placements)==2 and {p['Generation'] for p in placements}=={2}
    assert {p['Body'] for p in placements}=={p['Body'] for p in originals}
    for p in placements:
        o=next(o for o in originals if o['Body']==p['Body']);assert all(p[k]==o[k] for k in ('Owner','Body','Life','Mana','Order'))
        assert p['LastLanding']['X']>0 and p['LastLanding']['Y']>0
    saved=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());obstacle=json.loads((STAGE/'pet-obstacle-fixture.json').read_text())
    assert saved['Generation']==obstacle['Generation']==2 and saved['Body']==obstacle['Body'] and saved['Owner']==obstacle['Owner'] and saved['Seed']==obstacle['Seed']
    assert saved['TotalDraws']==10 and saved['Life']==69247 and saved['Mana']==54272
    for name,count in (('named-item-runtime-audit.json',944),('first-loop-affix-audit.json',21)):
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in current['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    a=(STAGE/'audit-native-checks.log').read_text();assert a.count(': passed')==2 and ': failed' not in a and ' ERR]' not in a
    for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-3.log')).read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    diagnostic=STAGE/'diagnostic-fixture-build-1'
    assert 'error CS' in (diagnostic/'integration-build-1.log').read_text() and 'AssertionError' in (diagnostic/'source-check.log').read_text()
    assert sha(diagnostic/'Sanctuary.Content.dll')==native['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert (diagnostic/'GenericPetNativePlacement.cs').read_text()==(ROOT/'src/Sanctuary.Content/GenericPetNativePlacement.cs').read_text()
    files=[Path(__file__).resolve(),sourcepath,ROOT/'tests/check_pet_native_placement.py',baseline,
        *(ROOT/'docs'/n for n in ('pet-native-placement-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-3.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-2.log','audit-native-checks.log','audit-run-2.log','source-review-2.log','source-check-2.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-3.log','integration-build-3.log','pet-native-placement-fixtures.json','goatman-body-fixtures.json','pet-reconnect-fixture.json','pet-obstacle-fixture.json')),
        *[p for p in diagnostic.iterdir() if p.is_file()]]
    failed=STAGE/'diagnostic-native-start-floor-1'
    assert 'Prepared placement start floor' in (failed/'birth-native-checks.log').read_text()
    assert sha(failed/'Sanctuary.Content.dll')==native['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert (failed/'GenericPetNativePlacement.cs').read_text()==(ROOT/'src/Sanctuary.Content/GenericPetNativePlacement.cs').read_text()
    files.extend(p for p in failed.iterdir() if p.is_file())
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Actual native ground placement with source collision buffers and operation-local room membership',
        verified=dict(nativeMarkers=134,retainedNativeMarkers=131,boots=3,reloads=2,saves=3,originalActorsPerBoot=2,nativeScenesPerBoot=8,pythonTests=71,namedDecisions=573,auditFingerprints=[944,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Native scenes use supplied live monster paths on the existing PetGroundWorld projection. Own footprint removal/restore,current nearby/path/collision kernels,native walls/height refusals and cross-room/native-sector membership are exercised with original saved actors.',
            'New monster hook/room-link ports are source-reviewed,not a new raw-code differential certificate. Full room-list edge cases,global membership/crowd ordering and exact D2 visibility/network transport remain open;native MoveToWorld supplies UO transport.',
            'The snapshot is single-use/map-bound and currently restricts callers to an owned prepared Goatman,mask0x100,pattern1..5 and no monster reset target. Birth/path initialization,automatic GenericPet controller/combat,learned cast/payment/cap/input/lifecycle remain gated. No deployment,new handler,class completion or final-art claim.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    target=ROOT/'tools/reference/pet-native-placement-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Native pet placement:134markers/3boots/2reloads;two original actors repeat actual terrain/room scenes;71Python/106installed unchanged;source review and remaining differential/controller scope explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
