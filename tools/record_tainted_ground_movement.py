#!/usr/bin/env python3
"""Authenticate shared ground movement with Tainted's owned species inputs."""
import argparse,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/tainted-ground-movement'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    snaps=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in (1,2,3)]
    assert all(s['projectInputs']==snaps[0]['projectInputs']and s['copiedData']==snaps[0]['copiedData']for s in snaps)
    assert {n for n in snaps[0]['sourceInputs']if snaps[0]['sourceInputs'][n]!=snaps[1]['sourceInputs'][n]}=={'src/Sanctuary.Content/DeathMarkWorld.cs','src/Sanctuary.Content/ReviveMeleeGeometry.cs'}
    assert {n for n in snaps[1]['sourceInputs']if snaps[1]['sourceInputs'][n]!=snaps[2]['sourceInputs'][n]}=={'src/Sanctuary.Content/DeathMarkWorld.cs','tests/Sanctuary.Integration/TaintedGroundMovementChecks.cs'}
    for kind,num in [('content',3),('integration',1)]:
        p=STAGE/f'{kind}-build-{num}.log';pin(p);assert all(s in p.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/goatman-cast-native-evidence.json');source=pin('tools/reference/tainted-ground-motion-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847'and source['liveReferenceReads']==0 and source['source']['Size']==3 and source['source']['WalkSpeed']==176
    for name,h in source['inputs'].items():assert e.sha(ROOT/name)==h,name
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'tainted-ground-movement-check':1,'tainted-ground-movement-reload-check':2})
    text=e.cohort(STAGE,expected,39);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    rows=[tuple(map(int,r))for r in re.findall(r'Tainted shared ground movement:(\d+) updates,(\d+) room crossings,(\d+) subtile samples',text)]
    assert len(rows)==3 and all(0<f<1000 and rooms>0 and subtiles>0 for f,rooms,subtiles in rows)
    assert len(re.findall(r'goatman-cast-(?:right|quick): automatic original demon attacked through saved source host',text))==6
    saved=pin(STAGE/'tainted-ground-movement-fixtures.json');assert saved['Generation']==2 and saved['Maximum']-saved['Life']==257 and saved['Path']['Size5c']==3
    assert pin(STAGE/'goatman-cast-fixtures.json')['Generation']==pin(STAGE/'goatman-native-host-fixtures.json')['Generation']==2
    for name in ('content-build-1.log','GoatmanNativeMotion.cs','GoatmanNativeWalking.cs','reason.txt'):pin(STAGE/'diagnostics-build-1'/name)
    assert 'CS1503'in (STAGE/'diagnostics-build-1/content-build-1.log').read_text();pin(STAGE/'content-build-2.log')
    for name in ('src/Sanctuary.Content/DemonGroundMovement.cs','src/Sanctuary.Content/GoatmanNativeMotion.cs','src/Sanctuary.Content/GoatmanNativeWalking.cs',
        'src/Sanctuary.Content/DeathMarkWorld.cs','src/Sanctuary.Content/ReviveMeleeGeometry.cs','tests/Sanctuary.Integration/TaintedGroundMovementChecks.cs',
        'tests/check_tainted_ground_movement.py','tools/record_tainted_ground_source.py','tools/record_tainted_ground_movement.py',
        'docs/tainted-ground-movement-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'data/Sanctuary/Balance/tainted-ground-motion.json'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Shared source ground path/walking/follow/warp with Tainted species inputs',handlers=222,warlockHandlers=13,completedClasses=[],
        learnedGoatmanEnabled=True,learnedTaintedEnabled=False,automaticTaintedAiEnabled=False,nativeMarkers=39,markers=dict(expected),
        authenticatedSessions=12,actualInputs=6,boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=1,sourceSnapshot=3,nativeRun=1,
        nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,
        activityPerBoot=[dict(updates=f,roomCrossings=r,subtileSamples=s)for f,r,s in rows],artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Existing source planner/walk/follow/warp services accept original registered Tainted with own3x3footprint,6velocity and176walk speed.',
            'Path crosses native/source rooms and publishes fractional cells into combat geometry;existing Goatman automatic combat/real casting remains passing.',
            'Owner mastery changes do not rewrite birth;movement and nearby-owner warp preserve private RNG and wounds.',
            'Wrong Goatman-sized walking/warp context refuses atomically;flying Defiler remains outside the ground service.',
            'Original parked Tainted body/owner/birth/path/source header/resources/private RNG survive2actualreloads;fresh native routes pass after each load.'],
        limits=['This component supplies movement context explicitly;Tainted automatic source root,skill/admission and learned summon remain unfinished.',
            'UO terrain/room/crowd/near-owner projection is an adapter context,not proof of D2 constructor/global caller defaults.',
            'Blood Boil do160 remains separately unsupported;existing Fire Ball/A2/aura can proceed independently.',
            'Full class certification and separate final effects/audio/talent/hotkey artwork remain outstanding.'],inputs=inputs)
    path=ROOT/'tools/reference/tainted-ground-movement-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Tainted ground native evidence stale'
    else:path.write_text(encoded)
    print('Tainted shared ground movement:39markers/6realGoatmaninputs/2reloads;own3x3/6/176species inputs,wounds/RNG retained;222handlers unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
