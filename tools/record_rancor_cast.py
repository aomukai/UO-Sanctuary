#!/usr/bin/env python3
"""Bind approved learned Rancor to its tested candidate and native cohort."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/rancor-cast'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else None if path.suffix=='.dll'else path.read_text()
    approval=pin('tools/reference/rancor-pulse-approval.json');assert approval['approved']and approval['approval']=='Approve all three proposals (Recommended)'
    raw=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT)
    assert hashlib.sha256(raw).hexdigest()==approval['proposalAtApprovalSha256']
    candidate=pin('tools/reference/rancor-pulse-native-evidence.json');assert candidate['nativeMarkers']==24 and not candidate['approved']
    for name,h in candidate['inputs'].items():
        if name.startswith(('src/','tests/'))and name.endswith('.cs'):
            assert hashlib.sha256(subprocess.check_output(['git','show',approval['candidateCommit']+':'+name],cwd=ROOT)).hexdigest()==h,name
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-3.json')['inputs']
    assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/RancorMindChecks.cs','tests/Sanctuary.Integration/SigilFamilyChecks.cs'}
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,number in [('content',1),('integration',4)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for name in ['rancor-cast','rancor-pulse','rancor-mind','sigil-family']:
        expected[name+'-check']=1;expected[name+'-reload-check']=2
        if name!='rancor-mind':expected[name+'-check-result']=1;expected[name+'-reload-check-result']=2
    for mode in ['left','right','quick']:
        for suffix in ['check','result']:expected[f'rancor-cast-{mode}-{suffix}']=3
    log=e.cohort(STAGE,expected,48);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    for name in ['rancor-cast','rancor-pulse','sigil-family']:assert pin(STAGE/f'{name}-fixtures.json')['Generation']==2
    assert 'Sigil input timeout'in pin(STAGE/'diagnostics-1/birth-native-checks.log')
    pin(STAGE/'diagnostics-1/reason.txt');pin(STAGE/'integration-build-1.log')
    assert 'Original saved secondary stream/focus/curse/wounds changed'in pin(STAGE/'diagnostics-2/birth-native-checks.log')
    for name in ['birth-inputs.json','native-run-2.log','RancorMindChecks.cs','Sanctuary.Integration.dll']:pin(STAGE/'diagnostics-2'/name)
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert born['artifacts'][name]==candidate['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    assert 'Original family/marker/source context changed after reload'in pin(STAGE/'diagnostics-3/birth-native-checks.log')
    for name in ['birth-inputs.json','native-run-3.log','SigilFamilyChecks.cs','Sanctuary.Integration.dll']:pin(STAGE/'diagnostics-3'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==219
    assert sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==10
    helper=pin('src/Sanctuary.Content/SigilRancorPulseProposal.cs');assert 'Approved=>true'in helper and 'SigilActors.Pulse+=Pulse'in helper
    for name in ['src/Sanctuary.Content/WarlockCombat.cs','tests/Sanctuary.Integration/RancorCastChecks.cs','tests/check_rancor_cast.py','tools/record_rancor_cast.py','docs/rancor-cast-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='approved learned Rancor',approved=True,handlers=219,warlockHandlers=10,completedClasses=[],rancorLearnedEnabled=True,automaticPulseEnabled=True,nativeMarkers=48,markers=dict(expected),authenticatedInputs=9,boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=4,sourceSnapshot=3,nativeRun=4,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Pulse/callback rule is a user-approved Sanctuary adaptation, not recovered protected do61.','Native packet/recipient/persistence acceptance does not certify PvP, unmapped native creatures, graphical client behavior or the complete class.','Effects/audio and talent/hotkey icons remain explicitly tracked placeholders.'],inputs=inputs)
    path=ROOT/'tools/reference/rancor-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Rancor cast evidence stale'
    else:path.write_text(encoded)
    print('Rancor learned:48 native markers/9 authenticated inputs/3 boots/2 reloads;219 handlers,Warlock10;installed build/config/saves preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
