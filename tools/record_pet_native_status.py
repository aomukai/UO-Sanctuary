#!/usr/bin/env python3
"""Verify live control-state consumers and original-demon reload coverage."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-native-status';PARENT='3927d9dbd981c5db47bc5c3dbb27bb9c93c2f092'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'src/Sanctuary.Content/GenericPetNativeControlStates.cs').decode()
    assert 'NamedControl.FrozenRemaining(actor)'in source and 'CombatStuns.Remaining(actor)'in source
    assert '(original.Rootb30&~Mask)|Read(actor)'in source and 'selector!=1'in source
    assert all(s not in source for s in ['Timer.StartTimer','SerializationGenerator','Profiles','UntilUtc'])
    for name in ['SourceFrameTimeline.cs','GenericPetAiEntry.cs','GenericPetAiSelection.cs','GenericPetAiPreprocess.cs','GenericPetAiPostTarget.cs','GenericPetAiOverrides.cs','GenericPetOwnerLoss.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetCombat.cs','GenericPetTimedEvents.cs','GenericPetEventDispatch.cs','DeathMarkWorld.cs','WarlockCombat.cs','GenericPetStartupProposal.cs','CombatStuns.cs','NamedControl.cs','FreezeStates.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-native-status-check':1,'pet-native-status-reload-check':2})
    log=e.cohort(STAGE,expected,15)
    assert log.count('Native pet status original:freeze suppresses AI delivery;live stun entry rearms3frames')==9
    assert log.count('AI entry original:live preprocessing stopped before default root')==9
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
    for name in ['docs/pet-native-status-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_native_status.py','tests/Sanctuary.Integration/GenericPetAiEntryChecks.cs']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],reviewOnly=True,automaticAiEnabled=False,learnedEnabled=False,
        sourceStates=dict(freeze=1,stunned=21),selector=1,newTimers=0,newSavedFormats=0,originalSpecies=3,additionalOriginalReviewActors=3,frozenDeliveriesSuppressed=9,stunnedEntriesPaused=9,recoveredRootRuns=9,
        newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Existing freeze/stun providers supply pinned state1/21 bits. Unsupported selectors/state-count/root forms refuse. Preprocessing projection keeps unrelated words,flags,path and movement. Native movement locks and post-freeze AI waiting are not misrepresented as active source freeze/stun.',
            'Real original actors stage existing effect producers. Shared pump/router blocks nine frozen AI events. Nine live stunned full entries pause three frames before root/control publication. Clear permits exactly nine single recovered roots at the scheduled deadline,with no early or replayed action. Actual overrides/initializer and default root retain supplied commands/birth/wounds/mana through two reloads.',
            'Shared evidence protection helper now accepts an explicit current client receipt/patch count and verifies its input manifest. Legacy default receipt/count remain unchanged. Current43patch preservation passes.'],
        limits=['Projection covers only two states consumed by these event/preprocess policies,not the complete source stat root. Caller still supplies qualified flags/mode/path/world leaves. State54/143,special AI,other live state and action/death bindings remain separate.',
            'Tests clear only their own post-freeze waiting fixture before stun coverage;no gameplay wait rule changes. Existing freeze/stun persistence/providers unchanged. No production admission,automatic timer,event persistence,startup default,save migration,learned skill,class certificate,deployment or assets. Separate startup proposal still pending.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-native-status-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Live pet freeze/stun:9suppressed deliveries/9paused entries/9recovered roots;15checks/two reloads,production AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
