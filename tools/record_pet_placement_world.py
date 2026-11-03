#!/usr/bin/env python3
"""Bind the opt-in persistent placement registry to actual saved actor checks."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/pet-placement-world'
PARENT='c7c34825f2d332a57ad114106d26f8a9275d97e2';sha=inventory.sha

def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-4.json',STAGE/(name+'-inputs.json'))
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/pet-path-targets/birth-inputs.json').read_text());assert native['copiedData']==prior['copiedData']
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/GenericPetNativePlacement.cs','src/Sanctuary.Content/GenericPetPlacementWorld.cs',
        'src/Sanctuary.Content/GenericPetPlacementStore.cs','tests/Sanctuary.Integration/PetPlacementWorldChecks.cs'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    baseline=ROOT/'.local/class-skills/pet-path-targets/birth-native-checks.log'
    old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/pet-path-targets-native-evidence.json'],cwd=ROOT))
    assert sha(baseline)==old['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['pet-placement-world-check']+=1;expected['pet-placement-world-reload-check']+=2
    assert sum(expected.values())==146 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert all(log.count(f'Pet placement world:generation{g},same saved registry/two original pets;')==1 for g in range(3))
    assert log.count('stale/duplicate/foreign-owner/corrupt guards and removal pass;')==3
    assert log.count('Pet path targets:4826complete reset/getter cases,48unsupported selectors,4586real metadata calls;')==3
    assert log.count('Pet room links:5952complete current-code cases,3222unsupported controls;')==3
    assert log.count('Pet monster placement hooks:9780complete current-code cases,12unsupported controls;')==3
    originals=json.loads((STAGE/'goatman-body-fixtures.json').read_text());fixture=json.loads((STAGE/'pet-placement-world-fixtures.json').read_text());image=json.loads(fixture['State'])
    assert fixture['Generation']==2 and len(originals)==len(fixture['Bodies'])==len(image['Members'])==2 and len(fixture['Positions'])==2
    assert image['Profile']=='prepared-generic-pet-placement-v1' and image['Revision']==11
    assert fixture['Bodies']==[o['Body'] for o in originals] and {m['Handle'] for m in image['Members']}==set(fixture['Bodies'])
    for member in image['Members']:
        o=next(o for o in originals if o['Body']==member['Handle']);p=member['Path'];point=fixture['Positions'][fixture['Bodies'].index(member['Handle'])]
        assert member['Owner']==o['Owner'] and p['ActorHandle']==member['Handle'] and p['ActorKind']==1
        assert p['Raw96']==p['Word30']==p['Word34']==member['PathWord34']==0
        assert p['Pattern58']==3 and p['Size5c']==1 and p['Mask60']==0x100 and p['Mask64']==0x1c09
        assert p['Word200']==69 and p['Point204']==0xfedcba98 and p['Collision68']==0x9876
        assert ((p['X']>>16)+1)//2==point['X'] and ((p['Y']>>16)+1)//2==point['Y']
        assert p['Target']==dict(Kind=1,Class=148,Variant=1,Flags124=0)
        assert member['Flags128']==member['PetFlags'] and member['Flags124']&0x2000 and member['PetFlags']&0x10000
    assert sum(r['FriendlyCount'] for r in image['Rooms'])==2
    saved=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());obstacle=json.loads((STAGE/'pet-obstacle-fixture.json').read_text())
    assert saved['Generation']==obstacle['Generation']==2 and saved['Body']==obstacle['Body'] and saved['Owner']==obstacle['Owner'] and saved['Seed']==obstacle['Seed']
    assert saved['TotalDraws']==10 and saved['Life']==69247 and saved['Mana']==54272
    for name,count in (('named-item-runtime-audit.json',948),('first-loop-affix-audit.json',21)):
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in current['InputHashes'].items():assert sha(ROOT/path)==h,path
        previous=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in previous.items() if k not in skip}
    a=(STAGE/'audit-native-checks.log').read_text();assert a.count(': passed')==2 and ': failed' not in a and ' ERR]' not in a
    for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-4.log')).read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    diagnostic=STAGE/'diagnostic-corridor-1';intermediate=STAGE/'intermediate-build-2'
    assert 'No independent fixture corridor near 6080,3300' in (diagnostic/'birth-native-checks.log').read_text()
    assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(intermediate/'content/Sanctuary.Content.dll')
    assert (diagnostic/'PetPlacementWorldChecks.cs').read_text().replace('6080,64,3300','6200,26,3500')==(intermediate/'PetPlacementWorldChecks.cs').read_text()
    assert markers(intermediate/'birth-native-checks.log')==expected and ': failed' not in (intermediate/'birth-native-checks.log').read_text()
    files=[Path(__file__).resolve(),ROOT/'tests/check_pet_placement_world.py',baseline,
        *(ROOT/'docs'/n for n in ('pet-placement-world-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-4.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-3.log','audit-native-checks.log','audit-run-3.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-4.log','integration-build-4.log','pet-placement-world-fixtures.json','pet-native-placement-fixtures.json','goatman-body-fixtures.json','pet-reconnect-fixture.json','pet-obstacle-fixture.json')),
        *(p for folder in (diagnostic,intermediate) for p in folder.rglob('*') if p.is_file())]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Opt-in saved shared room/update/path registry for actual prepared pets',
        verified=dict(nativeMarkers=146,retainedNativeMarkers=143,boots=3,reloads=2,saves=3,originalActorsPerBoot=2,originalSavedRegistry=True,
            actualCrossRoomMovesPerBoot=2,repeatedPlacementsPerBoot=1,finalRevision=11,pythonTests=71,namedDecisions=573,auditFingerprints=[948,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Registered paths are supplied,not invented. Room/update graph,path/cache/target data persist without replaying Add. Native geometry/collision buffers remain operation-local projections;full global crowd/visibility and actual target/controller/path birth/mode/speed/timing/combat remain open.',
            'This store is explicitly created and operated. Existing saves do not auto-enroll,move,heal,retarget or resummon pets. Actual AI,parking/facet/death ownership transport and learned summon/payment/cap/input remain unfinished. No handler,class completion,deployment or final-art claim.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    target=ROOT/'tools/reference/pet-placement-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Saved pet placement world:146markers/3boots/2reloads;same registry/two original pets,path/list order/owner guards persist;71Python/573named/106installed hashes retain;automatic AI and learned casting remain open.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
