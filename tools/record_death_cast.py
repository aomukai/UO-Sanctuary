#!/usr/bin/env python3
"""Authenticate approved learned Death, real input/corpse chains and persistence."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/death-cast'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else None if path.suffix=='.dll'else path.read_text()
    approval=pin('tools/reference/sigil-death-rule-approval.json');assert approval['approved']and approval['approval']=='Approve the complete Sigil: Death rule'
    raw=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT);assert hashlib.sha256(raw).hexdigest()==approval['proposalAtApprovalSha256']
    candidate=pin('tools/reference/sigil-death-proposal-native-evidence.json');assert candidate['nativeMarkers']==24 and not candidate['approved']
    for name,h in candidate['inputs'].items():
        if name.startswith(('src/','tests/'))and name.endswith('.cs'):
            assert hashlib.sha256(subprocess.check_output(['git','show',approval['candidateCommit']+':'+name],cwd=ROOT)).hexdigest()==h,name
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    original=pin(STAGE/'source-snapshot-1.json')['inputs'];fixed=pin(STAGE/'source-snapshot-2.json')['inputs'];last=pin(STAGE/'source-snapshot-5.json')['inputs']
    assert {n for n in original['sourceInputs']if original['sourceInputs'][n]!=fixed['sourceInputs'][n]}=={'src/Sanctuary.Content/SigilDeathEvents.cs'}
    assert fixed['projectInputs']==last['projectInputs']and fixed['copiedData']==last['copiedData']
    assert {n for n in fixed['sourceInputs']if fixed['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/SigilDeathChecks.cs','tests/Sanctuary.Integration/SigilFamilyChecks.cs','tests/Sanctuary.Integration/SigilLaunchChecks.cs','tests/Sanctuary.Integration/DeathCastChecks.cs'}
    for kind,number in [('content',2),('integration',3)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for name in ['death-cast','sigil-death','sigil-family','corpse-town']:expected[name+'-check']=1;expected[name+'-reload-check']=2
    for name in ['death-cast','sigil-death','sigil-family']:expected[name+'-check-result']=1;expected[name+'-reload-check-result']=2
    for mode in ['left','right','quick']:
        for suffix in ['check','result']:expected[f'death-cast-{mode}-{suffix}']=3
    log=e.cohort(STAGE,expected,48);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log');assert log.count('Learned Death:shared Warlock SC')==1
    for name in ['death-cast','sigil-death','sigil-family']:assert pin(STAGE/f'{name}-fixtures.json')['Generation']==2
    initial=pin(STAGE/'diagnostics-2/birth-native-checks.log');assert 'began=True killed=True'in initial and 'consumed=True'in initial and 'SigilDeathEvents'in initial
    for run in [1,2]:
        for name in ['birth-inputs.json',f'native-run-{run}.log','birth-native-checks.log','DeathCastChecks.cs','Sanctuary.Integration.dll']:pin(STAGE/f'diagnostics-{run}'/name)
    pin(STAGE/'diagnostics-2/reason.txt');assert 'CS0246'in pin(STAGE/'content-build-1.log');pin(STAGE/'compile-1/SigilDeathEvents.cs')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==candidate['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==221 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==12
    controller=pin('src/Sanctuary.Content/SigilDeathProposal.cs');assert 'Approved=>true'in controller and 'Sigil Death unsupported'in controller
    hooks=pin('src/Sanctuary.Content/SigilDeathEvents.cs');assert 'SigilActors.Pulse+=Pulse'in hooks and 'ClassLootHooks.CreatureDied+=Death'in hooks
    for name in ['src/Sanctuary.Content/WarlockCombat.cs','tests/Sanctuary.Integration/DeathCastChecks.cs','tests/check_death_cast.py','tools/record_death_cast.py','docs/death-cast-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='approved learned Sigil Death',approved=True,handlers=221,warlockHandlers=12,completedClasses=[],learnedDeathEnabled=True,productionPulseEnabled=True,productionDeathHookEnabled=True,nativeMarkers=48,markers=dict(expected),authenticatedInputs=9,boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=3,sourceSnapshot=5,nativeRun=3,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Complete user-approved Sanctuary adaptation,not recovered event33/34/do153 parity.','Native learned-input/corpse/chain/persistence acceptance does not certify complete class,PvP,graphical presentation or every special receiver.','Final effects/audio and talent/hotkey icons remain separately pending.'],inputs=inputs)
    path=ROOT/'tools/reference/death-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Death cast evidence stale'
    else:path.write_text(encoded)
    print('Death learned:48markers/9authenticatedcasts/3boots/2reloads;221handlers,Warlock12;installed build/config/saves preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
