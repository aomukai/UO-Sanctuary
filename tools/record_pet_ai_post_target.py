#!/usr/bin/env python3
"""Authenticate full post-target orchestration and retained special references."""
import argparse,json,subprocess,struct
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from probe_warlock_runtime import Image
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-ai-post-target';PARENT='10437969aeda99226f08dbdde115444ffb9ded7e'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-ai-post-target-source-evidence.json')
    assert source['sourceBytes']==390 and source['sourceContexts']==30 and source['sourceCodeExecutedInOfflineEmulator']and not source['processMemoryRead']and not source['targetProcessCodeExecuted']
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    captures=[]
    for name,sizes,read in [('callees-pet-ai-post-leaves-20261109.json',[378,1219],3754),('pet-ai-overrides-20261109.json',[782,933],3990)]:
        capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference'/name);captures.append(capture)
        assert [r['bytes']for r in capture['records']]==sizes and capture['bytesRead']==read
        assert all(r['allUnwindFragmentsCaptured']and all(f['invalidInstructionMarkers']==0 for f in r['fragments'])for r in capture['records'])
        assert not capture['gameMemoryWritten']and not capture['processInput']and not capture['securitySettingsChanged']and not capture['ptraceAttach']
        for n,h in capture['artifacts'].items():assert e.sha(ROOT/n)==h,n
    assert captures[0]['directCallTargetsVerified']
    image=Image();pin(image.reference);table=image.bytes(0x1d187f0,32)
    assert table==pin(ROOT/'.local/class-skills/warlock-runtime-reference/pet-ai-overrides-table-20261109.bin')
    assert captures[1]['descriptors']==[dict(index=i,flags=0,padding=0,function=hex(rva))for i,rva in enumerate([0x4b63d0,0x4b66e0])]
    for i,rva in enumerate([0x4b63d0,0x4b66e0]):assert struct.unpack_from('<iIQ',table,i*16)==(0,0,image.base+rva)
    for name in ['GenericPetAiEntry.cs','GenericPetAiSelection.cs','GenericPetAiPreprocess.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetMovement.cs','DeathMarkWorld.cs','GenericPetEventDispatch.cs','GenericPetTimedEvents.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    path='tests/Sanctuary.Integration/GenericPetAiEntryChecks.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode()
    assert before.replace('_=>false,(_,_)=>throw new InvalidOperationException("Unexpected queued predicate")','tick=>GenericPetAiPostTargetChecks.Original(body,tick),(_,_)=>throw new InvalidOperationException("Unexpected queued predicate")')==(ROOT/path).read_text()
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-2.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,build in [('content',2),('integration',1)]:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-{build}.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-ai-post-target-check':3})
    log=e.cohort(STAGE,expected,15);assert log.count('AI post-target:30current contexts/390bytes')==3
    assert log.count('AI post-target original:type0 no-target path')==9 and log.count('AI entry original:typed event->full prepared entry')==9 and log.count('Timed event root:original demon callback')==9
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    for i,entry in enumerate(saved['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    reviewed=STAGE/'pre-review-1'
    for name in ['GenericPetAiPostTarget.cs','GenericPetAiPostTargetChecks.cs','source-snapshot-1.json','content-build-1.log','Sanctuary.Content.dll']:pin(reviewed/name)
    assert 'GenericPetMonster2Metadata'in (reviewed/'GenericPetAiPostTarget.cs').read_text()
    assert 'GenericPetMonster2Metadata'not in (ROOT/'src/Sanctuary.Content/GenericPetAiPostTarget.cs').read_text()
    pin(STAGE/'capture-diagnostics-1/incorrect-register-preflight.py')
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
    for name in ['docs/pet-ai-post-target-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_ai_post_target.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],automaticAiEnabled=False,learnedEnabled=False,
        sourceBytes=390,sourceContexts=30,newCapturedReferenceBytes=3312,newCaptureBytesRead=7744,capturedReferencesImplemented=False,oldLargeSourceMatricesRerun=0,
        nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=1,sourceSnapshot=2,nativeRun=1,
        artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
        verified=['Complete390byte post-target source matches30native contexts:early exits,metadata lookup rereads,unsigned distance,special predicate and search/wander writes. Only three initialized descriptor9context fields are modeled.',
            'Original three-species full entry invokes actual no-target post stage before registered root;unqualified leaves refuse if called. Commands/control6updates/births/fractionalwounds retain across two reloads.',
            'Four missing helper bodies fully captured with no invalid instructions. Two direct post helpers and two table-qualified override callbacks are retained references;callback semantics are not certified by the post-stage comparison.'],
        diagnostics=['Initial Content build used an unrelated corpse metadata type to carry the animation walk bit. Review changed the API to shared nullable walk availability before Integration/native run;exact pre-review sources/build retained. No native failure.',
            'Override capture preflight initially named incorrect registers;corrected after checking retained instructions,before any target process access. Initial script retained.'],
        limits=['First/second helper internals,descriptor9search and live world services remain explicit. Capturing a callback does not implement its mechanics.',
            'Field14=2 remains explicit fixture,not authentic birth. Startup/control producer,special callbacks,global chain/clock and remaining live world/path/combat admission remain unfinished.',
            'No new learned route,class certificate,assets or deployment.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-ai-post-target-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('AI post-target:30source/native contexts/390bytes,15markers/two reloads;four references retained;AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
