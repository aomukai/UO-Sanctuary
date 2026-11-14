#!/usr/bin/env python3
"""Authenticate learned Tainted casting and its shared automatic saved host."""
import argparse,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/tainted-cast'
def record(content=1,integration=2,source=2,native=1,check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/f'source-snapshot-{source}.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for n in range(1,source+1):pin(STAGE/f'source-snapshot-{n}.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];second=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert first['projectInputs']==second['projectInputs']and first['copiedData']==second['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=second['sourceInputs'][n]}=={'tests/Sanctuary.Integration/TaintedCastChecks.cs'}
    pin(STAGE/'integration-build-1.log');assert 'CS0618'in (STAGE/'integration-build-1.log').read_text()
    for kind,num in [('content',content),('integration',integration)]:
        log=STAGE/f'{kind}-build-{num}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/tainted-native-decision-native-evidence.json');approval=pin('tools/reference/goatman-native-host-approval.json');assert approval['approved']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'tainted-body-check':1,'tainted-body-reload-check':2,
        'tainted-cast-check':1,'tainted-cast-reload-check':2,'tainted-cast-check-result':1,'tainted-cast-reload-check-result':2})
    for kind in ('right','quick'):
        expected[f'tainted-cast-{kind}-check']=3;expected[f'tainted-cast-{kind}-result']=3
    text=e.cohort(STAGE,expected,66);pin(STAGE/'birth-native-checks.log');pin(STAGE/f'native-run-{native}.log')
    for family in ('goatman','tainted'):
        assert len(re.findall(f'{family}-cast-(?:right|quick): automatic original demon attacked through saved source host',text))==6
    saved=pin(STAGE/'tainted-cast-fixtures.json');assert saved['Generation']==2 and saved['Maximum']-saved['Life']==257
    body=json.loads(saved['Text']);assert body['LearnedCast']and body['Controller']['Profile']=='native-tainted-host-v1'and body['Controller'].get('Fault')is None
    for name in ('goatman-cast','goatman-native-host','tainted-native-decision','tainted-ground-movement'):
        assert pin(STAGE/(name+'-fixtures.json'))['Generation']==2
    # Preserve any build/run diagnostics and earlier successful cohorts. Their
    # hashes remain evidence;they are never counted as the accepted pass.
    for folder in sorted(STAGE.iterdir()):
        if folder.is_dir()and folder.name.startswith(('diagnostics','reviewed-run')):
            for p in sorted(folder.iterdir()):
                if p.is_file():pin(p)
    for name in ('src/Sanctuary.Content/DemonNativeCombat.cs','src/Sanctuary.Content/DemonNativeState.cs',
        'src/Sanctuary.Content/GoatmanNativeHostCandidate.cs','src/Sanctuary.Content/WarlockGoatmanWorld.cs','src/Sanctuary.Content/WarlockGoatmanCasting.cs',
        'src/Sanctuary.Content/WarlockTaintedBody.cs','src/Sanctuary.Content/TaintedFireBallCasting.cs','src/Sanctuary.Content/DeathMarkWorld.cs','src/Sanctuary.Content/WarlockCombat.cs',
        'tests/Sanctuary.Integration/TaintedCastChecks.cs','tests/Sanctuary.Integration/TaintedBodyChecks.cs','tests/check_tainted_cast.py',
        'tools/record_tainted_cast.py','tools/build_skill_roadmap.py','docs/tainted-cast-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json'):pin(name)
    status=pin('data/Sanctuary/Balance/class-skill-status.json');assert any(r['talentId']=='warlock.summontainted'and r['implementation']=='handler-present'for r in status['skills'])
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Learned Summon Tainted and shared automatic saved ground-demon controller',handlers=223,warlockHandlers=14,completedClasses=[],
        learnedTaintedEnabled=True,automaticTaintedHostEnabled=True,nativeMarkers=66,markers=dict(expected),actualInputs=12,authenticatedSessions=24,boots=3,reloads=2,saves=3,
        contentBuild=content,integrationBuild=integration,sourceSnapshot=source,nativeRun=native,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,
        newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Shared complete source AI-entry/preprocess/root/native search/scoring/path/follow/warp/frame/recovery host accepts original Goatman/Tainted with own species inputs.',
            'Tainted actor claims exclusive A2 clock;ordinary cast timer cannot advance it even on a future observation. Existing nonhost A2 path remains qualified in the source-root regression.',
            'Learned right/quick SC input captures own rank/gear,spends fractional mana once and causes actual automatic NPC-owned Fire Ball damage;left binding remains source-forbidden.',
            'Shared hard-mastery caps/oldest replacement and failure roster preservation;no actor allocation/payment during cancelled windup.',
            'Automatic Tainted native source follow/near-owner warp with3x3 registered path;native Fists causes no duplicate damage/RNG.',
            'Optional omitted body fields keep legacy unadopted;original saved learned body/controller/birth/resources/RNG/bindings survive2reloads and actual login without offline healing/replay.',
            'Current Goatman learned/automatic host and Tainted source-root/ground/prepared-body regressions pass;old detached fixtures retain source checks after obsolete handler-absence assertions removed.'],
        limits=['Existing approved native ground-world mapping is reused with pinned Tainted species data;current D2 constructor/global caller equivalence is unverified.',
            'Blood Boil do160,Defiler flying/link host,full incoming/passive/special/queued/saved-follow/uninterruptible/recipient and prior projectile-removal contexts remain unfinished or unsupported.',
            'No class certification;effects/audio/talent/hotkey artwork remains separately identified placeholders;installed build/saves unchanged.'],inputs=inputs)
    path=ROOT/'tools/reference/tainted-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Learned Tainted evidence stale'
    else:path.write_text(encoded)
    print('Learned Tainted:66markers/12actualGoatman-Taintedinputs/2reloads;automatic saved source host/exclusive A2 clock;223/240handlers,Warlock14/30.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--content',type=int,default=1);p.add_argument('--integration',type=int,default=2);p.add_argument('--source',type=int,default=2);p.add_argument('--native',type=int,default=1);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.content,a.integration,a.source,a.native,a.check)
