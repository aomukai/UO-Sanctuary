#!/usr/bin/env python3
"""Authenticate approved learned Purge and existing damage/weapon composition."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/hex-purge-cast'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else None if path.suffix=='.dll'else path.read_text()
    approval=pin('tools/reference/hex-purge-reaction-approval.json');assert approval['approved']and approval['approval']=='Approve all three proposals (Recommended)'
    raw=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT);assert hashlib.sha256(raw).hexdigest()==approval['proposalAtApprovalSha256']
    candidate=pin('tools/reference/hex-purge-reaction-native-evidence.json');assert candidate['nativeMarkers']==15 and not candidate['approved']
    for name,h in candidate['inputs'].items():
        if name.startswith(('src/','tests/'))and name.endswith('.cs'):
            assert hashlib.sha256(subprocess.check_output(['git','show',approval['candidateCommit']+':'+name],cwd=ROOT)).hexdigest()==h,name
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-2.json')['inputs'];assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/HexPurgeCastChecks.cs'}
    for kind,number in [('content',1),('integration',2)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for name in ['purge-cast','purge-reaction','weapon-landed']:expected[name+'-check']=1;expected[name+'-reload-check']=2
    expected['purge-cast-check-result']=1;expected['purge-cast-reload-check-result']=2
    for mode in ['right','quick']:
        for suffix in ['check','result']:expected[f'purge-cast-{mode}-{suffix}']=3
    log=e.cohort(STAGE,expected,33);assert log.count('Purge production:applying Cleave/ranged hit')==6
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    saved=pin(STAGE/'purge-cast-fixtures.json');assert saved['Generation']==2 and [r['Female']for r in saved['Rows']]==[False,True]
    assert all(r['Mark']['Reaction']['RemainingCharges']==2 and r['TargetLife']%256==127 and r['Life']%256==179 and r['Mana']%256==251 for r in saved['Rows'])
    for name in ['purge-reaction','weapon-landed']:assert pin(STAGE/f'{name}-fixtures.json')['Generation']==2
    initial=pin(STAGE/'diagnostics-1/birth-native-checks.log');assert initial.count('Purge production:applying Cleave/ranged hit')==2
    for name in ['birth-inputs.json','native-run-1.log','reason.txt','HexPurgeCastChecks.cs','Sanctuary.Integration.dll']:pin(STAGE/'diagnostics-1'/name)
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==candidate['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==220 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==11
    assert 'Approved=>true'in pin('src/Sanctuary.Content/HexPurgeReactionProposal.cs')
    events=pin('src/Sanctuary.Content/HexPurgeEvents.cs');assert 'WeaponLandedEvents.BeforeDamage+=Landed'in events and 'SkillDamageEvents.BeforeDirectDamage+=Damage'in events
    for name in ['src/Sanctuary.Content/WarlockCombat.cs','src/Sanctuary.Content/HexPurge.cs','tests/Sanctuary.Integration/HexPurgeCastChecks.cs','tests/check_hex_purge_cast.py','tools/record_hex_purge_cast.py','docs/hex-purge-cast-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='approved learned Hex Purge',approved=True,handlers=220,warlockHandlers=11,completedClasses=[],purgeLearnedEnabled=True,automaticAttachmentEnabled=True,automaticReactionEnabled=True,nativeMarkers=33,markers=dict(expected),authenticatedInputs=6,boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Explicit approved callback36/39 adaptation,not recovered protected current source.','Native action/input/persistence checks do not certify complete class,PvP,advanced producers,graphical presentation or unsupported receivers.','Effects/audio and talent/hotkey icons remain separately tracked placeholders.'],inputs=inputs)
    path=ROOT/'tools/reference/hex-purge-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Purge cast receipt stale'
    else:path.write_text(encoded)
    print('Purge learned:33 markers/6 authenticated casts/3 boots/2 reloads;220 handlers,Warlock11;installed build/config/saves preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
