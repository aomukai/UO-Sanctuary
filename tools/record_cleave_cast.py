#!/usr/bin/env python3
"""Authenticate approved learned Cleave and actual left/right/quick inputs."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/cleave-cast'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    approval=pin('tools/reference/cleave-action-approval.json');assert approval['approved']and approval['approval']=='Approve the complete Cleave rule'
    raw=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT);assert hashlib.sha256(raw).hexdigest()==approval['proposalAtApprovalSha256']
    candidate=pin('tools/reference/cleave-action-native-evidence.json');assert not candidate['approved']and candidate['nativeMarkers']==21
    for name,h in candidate['inputs'].items():
        if name.startswith(('src/','tests/'))and name.endswith('.cs'):
            assert hashlib.sha256(subprocess.check_output(['git','show',approval['candidateCommit']+':'+name],cwd=ROOT)).hexdigest()==h,name
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-4.json')['inputs'];assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/CleaveCastChecks.cs'}
    for kind,number in [('content',1),('integration',3)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,**{name+'-check':1 for name in ['cleave-sweep','cleave-direction','cleave-action','cleave-cast']},**{name+'-reload-check':2 for name in ['cleave-sweep','cleave-direction','cleave-action','cleave-cast']},'cleave-cast-presave-check':3,**{f'cleave-cast-{mode}-{suffix}':3 for mode in ['left','right','quick']for suffix in ['check','result']}})
    log=e.cohort(STAGE,expected,42);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    assert log.count('Cleave learned:approved route/physical clock/range metadata')==3
    saved=pin(STAGE/'cleave-cast-fixtures.json');assert saved['Generation']==2 and [r['Female']for r in saved['Rows']]==[False,True]and all(r['Life256']%256==127 and r['Mana256']%256==239 for r in saved['Rows'])
    for name in ['cleave-action-fixtures.json','cleave-direction-fixtures.json','cleave-sweep-fixtures.json']:assert pin(STAGE/name)['Generation']==2
    old=pin(STAGE/'diagnostics-1/birth-native-checks.log');assert 'Original learned Cleave wounds/mana changed'in old and 'cleave-cast-reload-check: failed'in old and old.count('-result: passed')==6
    for name in ['birth-inputs.json','native-run-1.log','cleave-cast-fixtures.json','CleaveCastChecks.cs']:pin(STAGE/'diagnostics-1'/name)
    old=pin(STAGE/'native-2/birth-native-checks.log');assert ': failed'not in old and old.count('-result: passed')==9
    for name in ['birth-inputs.json','native-run-2.log','cleave-cast-fixtures.json','CleaveCastChecks.cs']:pin(STAGE/'native-2'/name)
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==candidate['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==218 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==9
    assert next(r for r in rows if r['talentId']=='warlock.cleave')['implementation']=='handler-present'
    assert 'Approved=>true'in pin('src/Sanctuary.Content/CleaveActionProposal.cs')
    for name in ['src/Sanctuary.Content/WarlockCombat.cs','src/Sanctuary.Content/ActionTiming.cs','tests/Sanctuary.Integration/CleaveCastChecks.cs','tests/check_cleave_cast.py','tools/record_cleave_cast.py','tools/build_skill_roadmap.py','docs/cleave-cast-checkpoint.md','docs/cleave-action-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',approved=True,learnedEnabled=True,currentStartRecipientMathParity=False,handlers=218,warlockHandlers=9,completedClasses=[],nativeMarkers=42,markers=dict(expected),boots=3,reloads=2,saves=3,actualLearnedWireInputs=9,inputModes=['left unit','right ground','quick unit'],savedOriginalOwners=2,preSaveResourceChecks=3,contentBuild=1,integrationBuild=3,sourceSnapshot=4,nativeRun=3,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Approved Sanctuary adaptation,not recovered current start68/math/recipient450780 parity.','First resource discrepancy remains untraced;diagnostic rerun passed unchanged content,final controls isolate enemies and assert resources before save.','Full PvP,advanced item combinations,unclassified creature exactness and graphical acceptance remain unqualified.','Purge approval and typed ordered-health/landed-event bridges remain separate.'],inputs=inputs)
    path=ROOT/'tools/reference/cleave-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Cleave learned:218handlers/Warlock9,42markers/9real left-right-quick casts/2reloads;original bindings/weapons/resources and pre-save controls pass;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
