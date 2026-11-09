#!/usr/bin/env python3
"""Pin shared Confuse reuse, independent Rancor state and native scan guard."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/rancor-mind'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else None if path.suffix=='.dll'else path.read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-6.json',STAGE/'birth-inputs.json')
    snapshots=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in range(1,7)]
    for s in snapshots:assert s['copiedData']==snapshots[0]['copiedData']and s['projectInputs']==snapshots[0]['projectInputs']
    assert {n for n in snapshots[0]['sourceInputs']if snapshots[0]['sourceInputs'][n]!=snapshots[-1]['sourceInputs'][n]}=={'src/Sanctuary.Content/SigilRancorMind.cs','src/Sanctuary.Content/SourceMind.cs','src/Sanctuary.Content/LeashedMeleeAI.cs','tests/Sanctuary.Integration/RancorMindChecks.cs'}
    assert {n for n in snapshots[4]['sourceInputs']if snapshots[4]['sourceInputs'][n]!=snapshots[5]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/RancorMindChecks.cs'}
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,number in [('content',3),('integration',6)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    families=['iron-golem-items','npc-resource-callbacks','rancor-mind','rancor-primary','confuse-cast']
    expected=Counter({'iron-golem-birth-check':3,**{n+'-check':1 for n in families},**{n+'-reload-check':2 for n in families}})
    log=e.cohort(STAGE,expected,18);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-6.log')
    assert log.count('Rancor mind:shared Confuse monster/player targeting and RNG')==3
    for n in range(1,6):
        folder=STAGE/f'diagnostics-{n}';old=pin(folder/'birth-native-checks.log');assert 'rancor-mind-check: failed'in old and 'Shared player-faction selection missing'in old
        pin(folder/'birth-inputs.json');pin(folder/f'native-run-{n}.log');pin(folder/'RancorMindChecks.cs');pin(folder/'Sanctuary.Integration.dll')
        if n in [1,4,5]:pin(folder/'Sanctuary.Content.dll')
    assert 'inScan=1'in pin(STAGE/'diagnostics-4/birth-native-checks.log')
    saved=pin(STAGE/'rancor-mind-fixtures.json');assert saved['Generation']==2 and saved['State']['RancorMind']['Draws']==3 and saved['State']['RancorMind']['Seed']>0 and saved['Life256']%256==127
    assert saved['State']['Curse']['Rank']==1 and 'Mind'not in saved['State']
    assert pin(STAGE/'rancor-primary-fixtures.json')['Generation']==2
    assert pin(STAGE/'confuse-cast-fixtures.json')['Generation']==2
    previous=pin('tools/reference/rancor-primary-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==previous['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==218 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==9
    assert next(r for r in rows if r['talentId']=='warlock.sigilrancor')['implementation']=='blocked'
    helper=pin('src/Sanctuary.Content/SigilRancorMind.cs');assert 'SigilActors.Pulse+='not in helper
    for name in ['src/Sanctuary.Content/SourceMind.cs','src/Sanctuary.Content/LeashedMeleeAI.cs','src/Sanctuary.Content/SigilRancorPrimary.cs','src/Sanctuary.Content/NecromancerEffects.cs','tests/Sanctuary.Integration/RancorMindChecks.cs','tests/check_rancor_mind.py','tools/record_rancor_mind.py','docs/rancor-mind-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','tools/reference/sigil-rancor-reference-gap.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='prepared Rancor reuse of generic Confuse selector',handlers=218,warlockHandlers=9,completedClasses=[],rancorLearnedEnabled=False,automaticPulseEnabled=False,aiSelectorImplemented=True,rancorCurrentCallbackParity=False,nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=6,sourceSnapshot=6,nativeRun=6,retainedFailedRuns=5,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalNullOmittedMetadata='NecromancerState.RancorMind',optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Eligibility,expiry and initial stream are supplied;priority behavior is a prepared candidate,not recovered do61 behavior.','Automatic75percent chance/pulse/refresh/group8/lifecycle and learned casting remain unimplemented.','Persistence deadlines are held;no authenticated Rancor input or full class/graphical acceptance is claimed.'],inputs=inputs)
    path=ROOT/'tools/reference/rancor-mind-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Rancor shared Confuse selector:18markers/3boots/2reloads;native scan gate,independent state/saved RNG and legacy Confuse pass;218handlers,automatic Rancor gated;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
