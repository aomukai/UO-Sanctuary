#!/usr/bin/env python3
"""Pin explicit flying-mask motion evidence without admitting a native flight host."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/defiler-flying-motion'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'src/Sanctuary.Content/SourceActorMotion.cs'}
    for kind in ('content','integration'):
        log=STAGE/f'{kind}-build-2.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/defiler-health-link-native-evidence.json');source=pin('tools/reference/defiler-flying-motion-source-evidence.json')
    for kind in ('inputs','artifacts'):
        for name,h in source[kind].items():assert e.sha(ROOT/name)==h,name;pin(name)
    assert source['cases']==192 and source['frames']==3840 and source['source']['Flying']
    earlier=pin('tools/reference/actor-motion-engine-evidence.json');assert source['compiledRoutineHashes']==earlier['compiledRoutineHashes']
    preflight=pin('tools/reference/pet-birth-path-preflight.json');assert preflight['species'][2]['flying']and preflight['species'][2]['legacyAllocatorMoveMask']=='0x1804'
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'defiler-flying-motion-check':1,'defiler-flying-motion-reload-check':2})
    text=e.cohort(STAGE,expected,81);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('Defiler flying motion:192sourcecontexts/3840exactframes,')==3
    assert text.count('Client: 127.0.0.1: Connected.')==27
    assert pin(STAGE/'defiler-flying-motion-fixtures.json')['Generation']==2
    assert pin(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
    for path in sorted((STAGE/'reviewed-build-1').iterdir()):pin(path)
    for name in ('src/Sanctuary.Content/SourceActorMotion.cs','src/Sanctuary.Content/SourceActorPath.cs',
        'src/Sanctuary.Content/DefilerFlyingMovement.cs','tests/Sanctuary.Integration/ActorMotionChecks.cs',
        'tests/Sanctuary.Integration/DefilerFlyingMotionChecks.cs','tests/check_defiler_flying_motion.py',
        'tools/probe_defiler_flying_motion.py','tools/record_defiler_flying_motion.py','data/Sanctuary/Balance/defiler-flying-motion.json',
        'docs/defiler-flying-motion-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md',STAGE/'native/Projects/UOContent/Engines/Pathing/Movement.cs'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Defiler species suppliers and explicit flying-mask shared motion',handlers=223,warlockHandlers=14,
        completedClasses=[],learnedDefilerEnabled=False,automaticDefilerHostEnabled=False,nativeFlyingTerrainEnabled=False,
        sourceCases=192,sourceFrames=3840,nativeMarkers=81,markers=dict(expected),actualInputs=12,authenticatedSessions=27,
        boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=1,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Qualified masks3c01/3401/1804 feed shared planning/step helpers;default callers unchanged;source step3401to3c01door substitution retained.',
            '192unchanged-source oracle contexts/3840exact native frames compare position,vectors,flags,queries,collisions and saved crossings with clean ASan/UBSan.',
            'Current Defiler744,flying2x2,6/256walk suppliers read original captured rank bonuses;parked body identities/wounds and component DTO survive two reloads.',
            'Accepted Health Link original groups and learned Goatman/Tainted actual-input/automatic host/source/mana/pool/save regressions retain.'],
        limits=['Pattern0 source grid does not prove2x2 occupancy or native flying terrain/height semantics.',
            'Legacy flying mask is not proof of the current fresh constructor;native flight transport/world mapping remains unfinished.',
            'Own S1event/type2AI,automatic Defiler host and learned summon remain unfinished;no new handler or class certification.',
            'Existing separate effects/audio/icons remain pending;no final assets produced.'],inputs=inputs)
    path=ROOT/'tools/reference/defiler-flying-motion-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Defiler flying motion evidence stale'
    else:path.write_text(encoded)
    print('Defiler flying motion:192sourcecases/3840exactframes;81native markers/12currentpetinputs/2reloads;native flight host still gated,installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
