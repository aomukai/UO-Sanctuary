#!/usr/bin/env python3
"""Authenticate the native flight-world candidate;never imply automatic admission."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/defiler-flying-world'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-6.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ('content','integration'):
        log=STAGE/f'{kind}-build-4.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/defiler-flying-motion-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'defiler-flying-world-check':1,'defiler-flying-world-reload-check':2,
        'defiler-flying-world-check-result':1,'defiler-flying-world-reload-check-result':2})
    text=e.cohort(STAGE,expected,87);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    assert text.count('Defiler flight world:real gap/wet/wall/door/occupancy,')==3
    assert text.count('Client: 127.0.0.1: Connected.')==30
    assert pin(STAGE/'defiler-flying-world-fixtures.json')['Generation']==2
    for folder in ('diagnostics-build-1','reviewed-build-2','diagnostics-1','diagnostics-2'):
        for path in sorted((STAGE/folder).iterdir()):pin(path)
    snaps=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in (2,3,4,5,6)]
    assert all(s['projectInputs']==snaps[0]['projectInputs']and s['copiedData']==snaps[0]['copiedData']for s in snaps)
    assert {n for n in snaps[0]['sourceInputs']if snaps[0]['sourceInputs'][n]!=snaps[1]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/DefilerFlyingWorldChecks.cs'}
    assert {n for n in snaps[3]['sourceInputs']if snaps[3]['sourceInputs'][n]!=snaps[4]['sourceInputs'][n]}=={'src/Sanctuary.Content/DefilerFlyingWorld.cs'}
    for name in ('src/Sanctuary.Content/DefilerFlyingWorld.cs','src/Sanctuary.Content/DemonPathWorld.cs',
        'src/Sanctuary.Content/DeathMarkWorld.cs','src/Sanctuary.Content/GenericPetNativePlacement.cs',
        'src/Sanctuary.Content/GoatmanNativeMotion.cs','src/Sanctuary.Content/GoatmanNativeWalking.cs',
        'src/Sanctuary.Content/ReviveMeleeGeometry.cs','src/Sanctuary.Content/Teleportation.cs','src/Sanctuary.Content/WarlockDefilerBody.cs',
        'tests/Sanctuary.Integration/DefilerFlyingWorldChecks.cs','tests/check_defiler_flying_world.py','tools/record_defiler_flying_world.py',
        'docs/defiler-flight-world-proposal.md','docs/defiler-flying-world-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Defiler native flight-world candidate',handlers=223,warlockHandlers=14,completedClasses=[],
        candidateOnly=True,flightWorldApproved=False,learnedDefilerEnabled=False,automaticDefilerHostEnabled=False,
        proposal='docs/defiler-flight-world-proposal.md',proposalSha256=e.sha(ROOT/'docs/defiler-flight-world-proposal.md'),
        nativeMarkers=87,markers=dict(expected),actualInputs=12,authenticatedSessions=30,boots=3,reloads=2,saves=3,
        contentBuild=4,integrationBuild=4,sourceSnapshot=6,nativeRun=3,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Explicit size2/mask1804 candidate maps actual native gap/wet/land surface volume,wall/door/overhead and hostile occupancy.',
            'Existing shared motion/walking/follow/warp/placement/room graph and source geometry transport reused;fractional movement crosses actual source rooms.',
            'Malformed shape refuses atomically;owner warp retains shape/flag,wounds,snapshot and random stream.',
            'Original saved candidate body/graph/header/path survives two reloads and actual roster/rebind without healing or recapture.',
            'Existing ground-only supplier still rejects Defiler;learned Goatman/Tainted and accepted Health Link regressions remain green.'],
        limits=['Native terrain/height/footprint and fresh constructor defaults are proposed adaptations,not verified current D2R behavior.',
            'Candidate has no automatic Defiler AI/learned cast;NPC405 S1/type2/group capacity/action host remains unfinished.',
            'Final effects/audio and talent/hotkey icons remain in separate registers;no final assets produced.'],inputs=inputs)
    path=ROOT/'tools/reference/defiler-flying-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Defiler flight-world candidate evidence stale'
    else:path.write_text(encoded)
    print('Defiler flight-world candidate:87markers/12currentpetinputs/30connections/2reloads;original graph/warp/terrain tested;approval/automatichost still pending;installed files protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
