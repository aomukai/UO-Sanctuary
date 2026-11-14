#!/usr/bin/env python3
"""Authenticate approved new-demon startup and unchanged legacy controllers."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-birth-startup'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else None if path.suffix=='.dll'else path.read_text()
    approval=pin('tools/reference/pet-startup-approval.json');assert approval['approved']and approval['approval']=='Approve all three proposals (Recommended)'
    raw=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT);assert hashlib.sha256(raw).hexdigest()==approval['proposalAtApprovalSha256']
    candidate=pin('tools/reference/pet-startup-proposal-native-evidence.json');assert candidate['nativeMarkers']==15
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-3.json')['inputs'];assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/GenericPetBirthStartupChecks.cs'}
    for kind,number in [('content',1),('integration',2)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-birth-startup-check':1,'pet-birth-startup-reload-check':2,'pet-startup-proposal-check':1,'pet-startup-proposal-reload-check':2})
    log=e.cohort(STAGE,expected,15);assert log.count('New demon startup:actual three birth factories')==3
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    saved=pin(STAGE/'pet-birth-startup-fixtures.json');assert saved['Generation']==2 and len(saved['Born'])==len(saved['Legacy'])==3
    assert [r['State']['Header']['SourceClass']for r in saved['Born']]==[742,743,744]
    for row in saved['Born']:
        assert row['State']['Header']['Mode10']==7 and row['State']['Header']['Flags38']==16 and row['State']['Header']['Ai90']==17 and row['State']['Control']==dict(Value0=-1,Value1=0,Value2=0)and row['Life']%256!=0
    assert all('Header'not in row['State']and 'Control'not in row['State']for row in saved['Legacy'])
    assert pin(STAGE/'pet-startup-proposal-fixtures.json')['Generation']==2
    old=pin(STAGE/'diagnostics-1/birth-native-checks.log');assert 'pet-birth-startup-check: passed'in old and 'Goatman needs a legal world point'in old
    for name in ['birth-inputs.json','native-run-1.log','reason.txt','GenericPetBirthStartupChecks.cs','Sanctuary.Integration.dll']:pin(STAGE/'diagnostics-1'/name)
    parent=pin('tools/reference/hex-purge-cast-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==220 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==11
    assert 'Approved=>true'in pin('src/Sanctuary.Content/GenericPetStartupProposal.cs')
    for name in ['src/Sanctuary.Content/GenericPetBirthStartup.cs','src/Sanctuary.Content/DeathMarkWorld.cs','src/Sanctuary.Content/GenericPetPlacementWorld.cs','src/Sanctuary.Content/WarlockGoatmanBody.cs','src/Sanctuary.Content/WarlockTaintedBody.cs','src/Sanctuary.Content/WarlockDefilerBody.cs','tests/Sanctuary.Integration/GenericPetBirthStartupChecks.cs','tests/check_pet_birth_startup.py','tools/record_pet_birth_startup.py','docs/pet-birth-startup-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='approved normal demon new-birth startup binding',approved=True,handlers=220,warlockHandlers=11,completedClasses=[],learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=3,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalNullOmittedMetadata='DeathMarkSavedActor.Header',optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Approved initial AI values apply to new births only;explicit native path input remains required.','Normal default entry/root is exercised;live path/search/follow services in the fixture are explicit inputs,not automatic production host acceptance.','Non-normal or executing/queued native header context remains unqualified and cannot be silently serialized.','Remaining clock/rearm/movement/search/combat/action/death bindings and learned summons remain unfinished.','No additional assets for this backend binding;skill effect/audio and icon registers remain separate.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-birth-startup-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Pet startup receipt stale'
    else:path.write_text(encoded)
    print('Demon new-birth startup:15 markers/3 boots/2 reloads;live normal headers persist,legacy controllers preserved;220 handlers,learned summons/automatic AI still gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
