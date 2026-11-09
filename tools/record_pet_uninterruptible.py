#!/usr/bin/env python3
"""Authenticate current completion reuse and native healthy scheduling guard."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-uninterruptible';PARENT='a79c15d23d3b7b41a2ea3a5e1df315eb4f46362a'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-uninterruptible-source-evidence.json')
    assert source['sourceContexts']==37 and source['newRoutineBytes']==649 and source['composedMachineCodeBytes']==2445
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/callees-pet-scheduling-state54-20261109.json')
    assert capture['bytesRead']==1858 and capture['directCallTargetsVerified']and capture['records'][0]['allUnwindFragmentsCaptured']and capture['records'][0]['bytes']==649
    for name,h in capture['artifacts'].items():assert e.sha(ROOT/name)==h,name
    adapter=pin(ROOT/'src/Sanctuary.Content/GenericPetUninterruptible.cs').decode()
    assert 'MonsterUninterruptible.Change(enabled!=0'in adapter and 'GenericPetOwnerLoss.Run(actor,killer,detach,loss)'in adapter
    for name in ['SourceFrameTimeline.cs','GenericPetAiEntry.cs','GenericPetAiSelection.cs','GenericPetAiPreprocess.cs','GenericPetAiPostTarget.cs','GenericPetAiOverrides.cs','GenericPetOwnerLoss.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetCombat.cs','GenericPetTimedEvents.cs','GenericPetEventDispatch.cs','DeathMarkWorld.cs','WarlockCombat.cs','GenericPetStartupProposal.cs','MonsterUninterruptibles.cs','CombatStuns.cs','NamedControl.cs','FreezeStates.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').read_text()
    old=pin(STAGE/'compile-diagnostics-1/source-snapshot-1.json')['inputs'];now=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
    assert [n for n in old['sourceInputs']if old['sourceInputs'][n]!=now['sourceInputs'][n]]==['tests/Sanctuary.Integration/GenericPetSchedulingStateChecks.cs']
    before=pin(STAGE/'compile-diagnostics-1/GenericPetSchedulingStateChecks.cs').decode()
    assert before.replace('foreach(var(kind,frame)','foreach(var(eventType,frame)').replace('events.Schedule(101,kind,frame','events.Schedule(101,eventType,frame')==(ROOT/'tests/Sanctuary.Integration/GenericPetSchedulingStateChecks.cs').read_text()
    assert 'CS0136'in pin(STAGE/'compile-diagnostics-1/integration-build-1.log').decode()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,build in [('content',1),('integration',2)]:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-{build}.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-scheduling-state-check':3,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-native-status-check':1,'pet-native-status-reload-check':2})
    log=e.cohort(STAGE,expected,18)
    assert log.count('Uninterruptible completion:37current contexts/649new bytes')==3
    assert log.count('Uninterruptible original:actual saved mode provider drives scheduling guard and AI switch')==9
    assert log.count('Native pet status original:freeze suppresses AI delivery;live stun entry rearms3frames')==9
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    statuses=pin(STAGE/'pet-native-status-fixtures.json');original=pin(STAGE/'pet-command-target-fixtures.json')
    assert statuses['Generation']==original['Generation']==2 and len(statuses['Entries'])==len(original['Entries'])==3 and len(statuses['Owners'])==2
    for i,entry in enumerate(statuses['Entries']):
        assert entry['Command']['Control']==dict(Value0=-2147483648+i+3,Value1=-1-i-21,Value2=(2147483647-i)^0x5a5a5a5a)
        assert entry['Command']['Type4c']==1 and entry['Command']['Target48']==statuses['Owners'][i%2]
        assert entry['Life']>0 and entry['Birth']
    for i,entry in enumerate(original['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    protected=e.protection('tools/reference/ring-cast-client-evidence.json',43)
    for name in ['tools/skill_checkpoint_evidence.py','tools/reference/creeper-frame-installation-evidence.json','.local/class-skills/creeper-frame-installation.json','tools/reference/ring-cast-client-evidence.json']:
        value=pin(ROOT/name)
        if name.endswith('ring-cast-client-evidence.json'):pin(ROOT/value['inputManifest']['path'])
    for name in ['docs/pet-uninterruptible-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_uninterruptible.py','src/Sanctuary.Content/GenericPetNativeControlStates.cs']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],reviewOnly=True,automaticAiEnabled=False,learnedEnabled=False,
        sourceStates=dict(uninterruptable=54,deathdelay=92),sourceContexts=37,newRoutineBytes=649,composedMachineCodeBytes=2445,newReferenceReadBytes=1858,oldLargeOfflineMatricesRerun=0,
        nativeModeSchedulingChecks=9,originalSpecies=3,newTimers=0,newSavedFormats=0,nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Complete current callback reuses shared MonsterUninterruptible.Change.37source/native contexts match state54first,cached game,kind/selector/root/count guards,state92clear-before-resolve,fresh flags2/zero-damage dispatch,owner-loss fallback and final kind-dependent AI-only removal including processing nodes. Qualified old event code reused.',
            'Existing native mode provider supplies scheduling/switch state54. Actual activation/completion on three original demon types removes only old AI,retains non-AI,blocks switching while active,clears state on scheduler callback and admits exactly one future AI event. Nine healthy native mode checks across two reloads;no second mode store/timer.',
            'Existing real freeze/stun event/entry checks still pass on the same cohort. Original commands,births,wounds,mana and control values retain across reloads. Native delayed-death effects remain mandatory unqualified leaves.'],
        diagnostics=['Integration1 local deconstruction variable shadowed later kind variable. Test-only rename fixed;C1reused. Exact source/snapshot/log retainedcompile-diagnostics-1. First actual native cohort passes.'],
        limits=['State setter,registry,fresh-return and owner-loss internals are explicit source services. Fresh callback keeps empty inline result/storage;arbitrary callback mutation/destructor ABI and allocator branch not certified.',
            'Native verification admits healthy no-delayed-death mode completion only. Full native delayed-death return/owner-loss side effects,live flags/mode/search/world/actions,state143 and other statuses remain incomplete. No blanket Kill/Delete substitute.',
            'No production automatic pump/startup admission/event persistence/save migration/learned skill/class certificate/deployment or assets. Startup proposal still pending.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-uninterruptible-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Current completion:37contexts/649bytes and9live native scheduling checks;18markers/two reloads,production AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
