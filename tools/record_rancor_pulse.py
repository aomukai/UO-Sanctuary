#!/usr/bin/env python3
"""Bind the complete prepared Rancor candidate without enabling learned input."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/rancor-pulse'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else None if path.suffix=='.dll'else path.read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    snapshots=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in range(1,6)]
    for s in snapshots:assert s['copiedData']==snapshots[0]['copiedData']and s['projectInputs']==snapshots[0]['projectInputs']
    assert {n for n in snapshots[1]['sourceInputs']if snapshots[1]['sourceInputs'][n]!=snapshots[2]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/RancorPulseChecks.cs'}
    assert {n for n in snapshots[2]['sourceInputs']if snapshots[2]['sourceInputs'][n]!=snapshots[4]['sourceInputs'][n]}=={'src/Sanctuary.Content/SigilRancorPulseProposal.cs','tests/Sanctuary.Integration/RancorPulseChecks.cs'}
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,number in [('content',4),('integration',3)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    families=['iron-golem-items','npc-resource-callbacks','rancor-pulse','rancor-mind','sigil-family']
    expected=Counter({'iron-golem-birth-check':3,**{n+'-check':1 for n in families},**{n+'-reload-check':2 for n in families},**{n+'-check-result':1 for n in ['rancor-pulse','sigil-family']},**{n+'-reload-check-result':2 for n in ['rancor-pulse','sigil-family']}})
    log=e.cohort(STAGE,expected,24);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    assert log.count('Rancor pulse candidate:claimed once,74/75boundary')==3
    old=pin(STAGE/'diagnostics-1/birth-native-checks.log');assert 'Shared Sigil replacement failed'in old
    for name in ['birth-inputs.json','native-run-1.log','RancorPulseChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll']:pin(STAGE/'diagnostics-1'/name)
    old=pin(STAGE/'native-2/birth-native-checks.log');assert ': failed'not in old and old.count(': passed')==24
    for name in ['birth-inputs.json','native-run-2.log','rancor-pulse-fixtures.json','RancorPulseChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll']:pin(STAGE/'native-2'/name)
    assert 'CS1503'in pin(STAGE/'content-build-1.log');pin(STAGE/'compile-1/SigilRancorPulseProposal.cs')
    saved=pin(STAGE/'rancor-pulse-fixtures.json');assert saved['Generation']==2 and saved['Actor']['Rank']==200 and saved['Actor']['Frames']==5100 and saved['Actor']['RancorPulse']>=0 and saved['Life256']%256==127
    seed=saved['State']['RancorMind']['Seed'];assert seed>0 and saved['State']['RancorPrimary']['Rank']==200
    assert pin(STAGE/'rancor-mind-fixtures.json')['Generation']==2 and pin(STAGE/'sigil-family-fixtures.json')['Generation']==2
    previous=pin('tools/reference/rancor-mind-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==previous['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==218 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==9
    assert next(r for r in rows if r['talentId']=='warlock.sigilrancor')['implementation']=='blocked'
    helper=pin('src/Sanctuary.Content/SigilRancorPulseProposal.cs');assert 'Approved=>false'in helper and 'SigilActors.Pulse+='not in helper
    states=pin('tools/reference/d2r-named-equipment/json/states.json');states=states.values()if isinstance(states,dict)else states
    assert {(s['state'],s['*ID'])for s in states if s.get('group')==8}=={('confuse',59),('sigilrancor',193)}
    for name in ['src/Sanctuary.Content/SigilActors.cs','src/Sanctuary.Content/SourceMind.cs','src/Sanctuary.Content/SigilRancorMind.cs','tests/Sanctuary.Integration/RancorPulseChecks.cs','tests/check_rancor_pulse.py','tools/record_rancor_pulse.py','docs/rancor-pulse-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='complete prepared Rancor pulse candidate',approved=False,handlers=218,warlockHandlers=9,completedClasses=[],rancorLearnedEnabled=False,automaticPulseEnabled=False,nativeMarkers=24,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=4,integrationBuild=3,sourceSnapshot=5,nativeRun=3,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalNullOmittedMetadata='SigilActor.RancorPulse',optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Chance cadence/stream,recipient admission/priority/refresh/expiry are an explicit unapproved adaptation for protected do61.','Connected fixture owners and shared driver are tested;authenticated learned Rancor input is not enabled or claimed.','Saved recipient deadlines are held;unmapped natives/PvP/complete class/graphical acceptance remain unqualified.'],inputs=inputs)
    path=ROOT/'tools/reference/rancor-pulse-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Rancor pulse candidate:24markers/3boots/2reloads;chance/refresh/expiry/lifecycle/original cursor-stream controls pass;218handlers,automatic Rancor gated;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
