#!/usr/bin/env python3
"""Authenticate current direction-wrapper arithmetic and prepared world binding."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/cleave-direction'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        if path.suffix=='.bin':return None
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    proof=pin('tools/reference/cleave-direction-current-evidence.json');assert proof['sourceBytes']==346 and proof['cases']==48 and proof['newProcessReads']==0 and not proof['protectedMathExecuted']
    for name,h in proof['artifacts'].items():assert e.sha(ROOT/name)==h,name;pin(name)
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-2.json')['inputs'];assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'src/Sanctuary.Content/CleaveSweep.cs'}
    for kind,number in [('content',2),('integration',1)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    assert 'CS0029'in pin(STAGE/'content-build-1.log');pin(STAGE/'diagnostics-1/CleaveSweep.cs')
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'cleave-sweep-check':1,'cleave-sweep-reload-check':2,'cleave-direction-check':1,'cleave-direction-reload-check':2})
    log=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert log.count('Cleave direction:48current wrapper contexts;unsigned packed deltas,float32 normalization/trace bits')==3
    fixture=pin(STAGE/'cleave-direction-fixtures.json');assert fixture['Generation']==2 and fixture['Request']['Skill']==391 and fixture['Request']['Rank']==1 and fixture['Request']['Radius']>=6
    assert pin(STAGE/'cleave-sweep-fixtures.json')['Generation']==2
    parent=pin('tools/reference/hex-purge-reaction-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==217
    assert sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==8
    assert next(r for r in rows if r['talentId']=='warlock.cleave')['implementation']=='blocked'
    for name in ['src/Sanctuary.Content/SourceDirection64.cs','src/Sanctuary.Content/CleaveSweep.cs','src/Sanctuary.Content/SourcePlayerMeleeReach.cs','src/Sanctuary.Content/PlayerMeleeGeometry.cs','tests/Sanctuary.Integration/CleaveDirectionChecks.cs','tests/check_cleave_direction.py','tools/record_cleave_direction.py','tools/reference/cleave-sweep-current-evidence.json','docs/cleave-direction-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',currentWrapperParity=True,currentMathContractParity=False,learnedEnabled=False,actualRecipientMembershipQualified=False,handlers=217,warlockHandlers=8,completedClasses=[],sourceCases=48,sourceBytes=346,nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=1,sourceSnapshot=2,nativeRun=1,reusedPlayerReach=True,newPersistenceIds=0,newTimers=0,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Three protected math contracts remain supplied;not inferred from adapter names.','Prepared world request only;start68,target/angle membership/contact/durability/payment/cancellation/input remain unqualified.','No final effects/icons or class certificate.'],inputs=inputs)
    path=ROOT/'tools/reference/cleave-direction-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Cleave direction:48current wrapper cases,15native markers,2reloads;original mapped reach/request retained;math contracts supplied,217handlers,installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
