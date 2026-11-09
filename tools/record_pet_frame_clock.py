#!/usr/bin/env python3
"""Authenticate prepared frame delivery without claiming production AI admission."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-frame-clock';PARENT='38055be5d3bc969fef730d314b4fbfc7c34ac5ff'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    timeline=pin(ROOT/'src/Sanctuary.Content/SourceFrameTimeline.cs').decode()
    assert '40*TimeSpan.TicksPerMillisecond'in timeline and 'events.Dispatch(bucket[^1],frame,true'in timeline
    users=[]
    for path in (ROOT/'src/Sanctuary.Content').glob('*.cs'):
        if path.name!='SourceFrameTimeline.cs'and 'GenericPetFramePump'in path.read_text():users.append(path.name)
    assert not users,'Prepared pump acquired production caller'
    path='src/Sanctuary.Content/IronGolemWorld.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode()
    old='long frames=(at-s.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;\n        var next=Park(s with{Frame=checked(s.Frame+(int)frames),NextUtc=s.NextUtc.AddMilliseconds(frames*40)});'
    new='var skipped=SourceFrameTimeline.Skip(new(s.Frame,s.NextUtc),at);\n        var next=Park(s with{Frame=skipped.Frame,NextUtc=skipped.NextUtc});'
    assert old in before and pin(ROOT/path).decode()==before.replace(old,new)
    for name in ['GenericPetStartupProposal.cs','GenericPetAiEntry.cs','GenericPetAiSelection.cs','GenericPetAiPreprocess.cs','GenericPetAiPostTarget.cs','GenericPetAiOverrides.cs','GenericPetOwnerLoss.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetCombat.cs','GenericPetOwnerSurvey.cs','GenericPetOwnerIterator.cs','GenericPetTimedEvents.cs','DeathMarkWorld.cs','WarlockCombat.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').read_text()
    pre=pin(STAGE/'source-snapshot-1.json')['inputs'];built=pin(STAGE/'source-snapshot-5.json')['inputs']
    assert pre['projectInputs']==built['projectInputs']and pre['copiedData']==built['copiedData']
    assert [n for n in pre['sourceInputs']if pre['sourceInputs'][n]!=built['sourceInputs'][n]]==['tests/Sanctuary.Integration/GenericPetFrameClockChecks.cs']
    old=pin(STAGE/'fixture-diagnostics-1/source-snapshot-2.json')['inputs']
    assert old['projectInputs']==built['projectInputs']and old['copiedData']==built['copiedData']
    assert [n for n in old['sourceInputs']if old['sourceInputs'][n]!=built['sourceInputs'][n]]==['tests/Sanctuary.Integration/GenericPetFrameClockChecks.cs']
    early=pin(STAGE/'fixture-diagnostics-1/birth-native-checks.log').decode();assert 'Existing ground consumer changed phase'in early
    pin(STAGE/'fixture-diagnostics-1/GenericPetFrameClockChecks.cs');pin(STAGE/'fixture-diagnostics-1/native-run-1.log')
    second=pin(STAGE/'fixture-diagnostics-2/birth-native-checks.log').decode();assert 'Original root finishing rejected typed target'in second
    pin(STAGE/'fixture-diagnostics-2/GenericPetFrameClockChecks.cs');pin(STAGE/'fixture-diagnostics-2/source-snapshot-3.json');pin(STAGE/'fixture-diagnostics-2/native-run-2.log')
    pin(STAGE/'compile-diagnostics-3/GenericPetFrameClockChecks.cs');pin(STAGE/'compile-diagnostics-3/source-snapshot-4.json')
    assert 'CS0117'in pin(STAGE/'compile-diagnostics-3/integration-build-3.log').decode()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,build in [('content',1),('integration',4)]:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-{build}.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-frame-clock-check':1,'pet-frame-clock-reload-check':2,'pet-frame-clock-result':3})
    log=e.cohort(STAGE,expected,18)
    assert log.count('Frame clock original:live40ms timer->shared bucket dispatcher')==3
    assert log.count('Frame clock ground:actual original Iron Golem inactive Advance')==3
    assert log.count('Frame clock:32existing skip-phase comparisons')==3
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    clock=pin(STAGE/'pet-frame-clock-fixtures.json');original=pin(STAGE/'pet-command-target-fixtures.json')
    assert clock['Generation']==original['Generation']==2 and len(clock['Entries'])==len(original['Entries'])==3 and len(clock['Owners'])==2
    assert clock['Clock']['Frame']>=9
    for i,entry in enumerate(clock['Entries']):
        assert entry['Command']['Control']==dict(Value0=-2147483648+i+9,Value1=-1-i-63,Value2=(2147483647-i)^0x5a5a5a5a)
        assert entry['Life']>0 and entry['Birth']
    for i,entry in enumerate(original['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    installed=pin(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert e.sha(Path(installed['runtimeRoot'])/name)==h,name
    protected=pin(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count and {str(p.relative_to(folder)):e.sha(p)for p in folder.rglob('*')if p.is_file()}==protected[kind]
    client=pin(ROOT/'tools/reference/ring-cast-client-evidence.json');clientInputs=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']and client['patches']==len(clientInputs['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for name,h in clientInputs[group].items():assert e.sha(ROOT/name)==h,name
    for name in ['docs/pet-frame-clock-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_frame_clock.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],reviewOnly=True,productionPumpCallers=[],automaticAiEnabled=False,learnedEnabled=False,
        nativeHostMapping='40ms explicit observations;oldest-to-newest bucket start;exact-frame due filtering',originalSpecies=3,additionalOriginalReviewActors=3,liveTimerCallbacks=27,groundConsumerChecks=3,phaseComparisonsPerBoot=32,
        newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=4,sourceSnapshot=5,nativeRun=3,
        artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
        verified=['Existing ground Skip alone adopts shared phase math. Actual original Iron Golem inactive Advance preserves wounds,mana,RNG,equipment and releases no action. Active ground conductor and saved schema unchanged.',
            'Prepared pump routes 27 full original demon entries through actual native timers,shared dispatcher/typed router/overrides/default root across three boots. Commands,births,wounds,mana,control retain through two reloads. FIFO,exact due,bucket wrap,callback insertion/cancellation,reentry and repeated observation checks pass.',
            'No production pump caller or timer registration. Startup proposal and source AI policies unchanged. Test-only reload explicitly skips elapsed phase and reconstructs transient event store before future AI rearming.'],
        diagnostics=['First native regression incorrectly expected held equipment to retain its former map while its parent parks internally. Corrected only test expectation to follow the original parent. Exact failed log,source,snapshot retainedfixture-diagnostics-1. Second run exposed the unknown registration command in the reused root fixture; supplied each real owner as an explicit typed command. No startup behavior inferred. Exact second failure retainedfixture-diagnostics-2. Integration3 used the wrong fixture capture method name; test-only fix to existing Capture. Compile diagnostic retained. Content1reused;Integration4/snapshot5/native3accepted.'],
        limits=['Starting-chain choice is a native host mapping,not recovered D2R global caller behavior. Live world/search/path/combat/death bindings and typed suspension/expiry/rearm remain unqualified.',
            'No production event persistence,header save format,startup admission,save migration,learned route,class certificate,deployment or assets. Separate startup approval remains pending.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-frame-clock-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Prepared frame clock:27timer callbacks,18checks/two reloads;existing ground phase preserved,production AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
