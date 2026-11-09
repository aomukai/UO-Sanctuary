#!/usr/bin/env python3
"""Authenticate full current AI-entry orchestration and original event-to-root composition."""
import argparse,json,subprocess,hashlib
from probe_warlock_runtime import Image
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-ai-entry'
PARENT='01e9926cab923f2e685a3af69a34cd2f04ed82f8'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-ai-entry-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['sourceBytes']==1096 and source['sourceContexts']==57
    assert source['sourceCodeExecutedInOfflineEmulator'] and not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    helpers=pin(ROOT/'.local/class-skills/warlock-runtime-reference/callees-pet-ai-entry-core-20261109.json')
    assert helpers['bytesRead']==4476 and helpers['directCallTargetsVerified'] and helpers['inlineData']['bytes']==28
    assert [row['bytes']for row in helpers['records']]==[544,187,454,383,390]
    assert all(row['allUnwindFragmentsCaptured']for row in helpers['records'])
    for name,digest in helpers['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    for name in ['GenericPetInitialization.cs','GenericPetDecisions.cs','DeathMarkWorld.cs','GenericPetEventDispatch.cs','GenericPetTimedEvents.cs']:
        path='src/Sanctuary.Content/'+name
        assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    descriptor=pin(ROOT/'.local/class-skills/warlock-runtime-reference/generic-pet-candidate.json')
    image=Image();pin(image.reference)
    assert descriptor['genericPetIndex']==148 and descriptor['aiTableRva']=='0x1d18810' and descriptor['recordStride']==32
    raw=image.bytes(0x1d18810+148*32,32);descriptorHash=hashlib.sha256(raw).hexdigest()
    assert descriptorHash==next(row['sha256']for row in descriptor['reads']if row.get('index')==148)
    assert int.from_bytes(raw[:4],'little')==0 and int.from_bytes(raw[16:24],'little')==image.base+0x4b60c0
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind in ['content','integration']:
        path=STAGE/kind/f'Sanctuary.{kind.title()}.dll';assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-ai-entry-check':3})
    log=e.cohort(STAGE,expected,15);assert log.count('AI entry:57complete-current contexts/1096bytes')==3
    pin(STAGE/'birth-native-checks.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    assert log.count('AI control:explicit three-word registration')==3
    for i,entry in enumerate(saved['Entries']):
        control=entry['Command']['Control']
        assert control==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    assert log.count('Timed event root:original demon callback')==9 and log.count('AI entry original:typed event->full prepared entry')==9
    diagnostic=ROOT/'.local/class-skills/pet-ai-entry-source/probe-diagnostics-1'
    pin(diagnostic/'probe_pet_ai_entry.py');failed=pin(diagnostic/'probe-run.log').decode()
    assert 'AssertionError' in failed
    installed=pin(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert e.sha(Path(installed['runtimeRoot'])/name)==digest,name
    protected=pin(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count
        assert {str(p.relative_to(folder)):e.sha(p)for p in folder.rglob('*')if p.is_file()}==protected[kind]
    client=pin(ROOT/'tools/reference/ring-cast-client-evidence.json');clientInputs=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']
    assert client['patches']==len(clientInputs['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for name,digest in clientInputs[group].items():assert e.sha(ROOT/name)==digest,name
    for name in ['docs/pet-ai-entry-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_ai_entry.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],learnedEnabled=False,automaticAiEnabled=False,
                aiEntryBytes=1096,sourceContexts=57,genericPetDescriptor=dict(index=148,type=0,functionRva='0x4b60c0',sha256=descriptorHash,primaryMatchesCaptured=True),newHelperCaptureBytesRead=4476,capturedHelperBytes=1958,oldLargeSourceMatricesRerun=0,
                nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Complete1096-byte entry matches57native comparisons with explicit helper and stable inline-functor services. Snapshot timing/unwritten tail,metadata order,all target policies,early exits,original/replaced overrides,queue FIFO/predicate/release/append and updated default function are covered. Missing-control/null-function boundaries refuse;the missing-control frame is not returned/compared.',
                          'Original3-speciesWarlock/Witch event tests now pass typed event->full prepared entry->registered root on same store. Processing/upcoming cancellation,non-AI retention,commands,births,wounds and six control updates per actor survive two actual reloads.',
                          'Five direct helpers captured completely for future binding. Inline28-byte jump data is validated as seven actual targets and excluded from instruction parsing. Existing root/controller/event producer/executor sources are text-identical to parent;automatic admission is unchanged.'],
                limits=['Metadata/preprocessing/target/special/queued helper and world internals remain explicit dependencies. Raw functor heap/move/destructor ABI,exceptions,unstable-head mutation and reentrancy are not certified. Game snapshot unwritten byte remains an explicit input with no inferred gameplay meaning.',
                        'Native entry control/metadata are explicit fixtures;field14=2 is not an authentic birth claim. Actual helper/special-callback binding,AI-control producer,live roster,global event-chain/host clock,suspension/persistence and remaining world/actions still need completion.',
                        'No learned route,automatic timer,class certificate,deployment or new assets. Effects/audio and talent/hotkey icons remain separate.'],
                diagnostics=['Initial oracle assumed queued helper takes tick context;source call actually passes AI control. Exact initial script/log archived;C#service signature corrected before builds/native checks. Accepted native1 has no failed build/run.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-ai-entry-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('AI entry:57source/native contexts,15markers/two reloads;213handlers;automatic AI gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
