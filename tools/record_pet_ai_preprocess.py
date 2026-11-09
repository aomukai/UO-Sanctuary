#!/usr/bin/env python3
"""Authenticate current preprocessing composition and preserved native cohort."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-ai-preprocess';PARENT='9793d029846b62feab54cec8f548197f462ec356'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-ai-preprocess-source-evidence.json')
    assert source['preprocessBytes']==544 and source['savedTargetBytes']==464 and source['sourceContexts']==51
    assert source['sourceCodeExecutedInOfflineEmulator']and not source['processMemoryRead']and not source['targetProcessCodeExecuted']
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/callees-pet-ai-preprocess-leaf-20261109.json')
    assert capture['directCallTargetsVerified']and capture['bytesRead']==1488 and capture['records'][0]['bytes']==464 and capture['records'][0]['allUnwindFragmentsCaptured']
    assert not capture['gameMemoryWritten']and not capture['processInput']and not capture['securitySettingsChanged']
    for name,h in capture['artifacts'].items():assert e.sha(ROOT/name)==h,name
    for name in ['GenericPetAiEntry.cs','GenericPetAiSelection.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetMovement.cs','DeathMarkWorld.cs','GenericPetEventDispatch.cs','GenericPetTimedEvents.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    path='tests/Sanctuary.Integration/GenericPetAiEntryChecks.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode()
    assert before.replace('_=>throw new InvalidOperationException("Fixture has no special override"),_=>false,(_,state)=>','_=>throw new InvalidOperationException("Fixture has no special override"),tick=>GenericPetAiPreprocessChecks.Original(body,control,tick),(_,state)=>')==(ROOT/path).read_text()
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-ai-preprocess-check':3})
    log=e.cohort(STAGE,expected,15);assert log.count('AI preprocessing:51current contexts/1008bytes')==3
    assert log.count('AI entry original:typed event->full prepared entry')==9 and log.count('Timed event root:original demon callback')==9
    assert log.count('AI control:explicit three-word registration')==3
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    for i,entry in enumerate(saved['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    reviewed=STAGE/'oracle-cookie-review-1';original=pin(reviewed/'birth-inputs.json')
    for name in ['Sanctuary.Content.dll','Sanctuary.Integration.dll']:
        assert e.sha(reviewed/name)==born['artifacts']['Assemblies/'+name]==original['artifacts']['Assemblies/'+name]
    old=pin(reviewed/'sources/probe_pet_ai_preprocess.py').decode();now=(ROOT/'tools/probe_pet_ai_preprocess.py').read_text()
    assert old.replace("if name=='cookie':self.ret();return","if name=='cookie':self.ret(u.reg_read(UC_X86_REG_RAX));return # Compiler cookie epilogue preserves the result register.")==now
    before=pin(reviewed/'cases.json');after=json.loads((ROOT/'.local/class-skills/pet-ai-preprocess-source/cases.json').read_text());changed=0
    assert len(before)==len(after)==51
    for a,b in zip(before,after):
        assert a['input']==b['input'];ao=dict(a['output']);bo=dict(b['output']);changed+=ao.pop('result')!=bo.pop('result');assert ao==bo
    assert changed>0
    assert 'AI preprocessing result differs at1'in pin(reviewed/'birth-native-checks.log').decode()
    pin(reviewed/'native-run-1.log');pin(reviewed/'sources/pet-ai-preprocess-source-evidence.json')
    pin(STAGE/'probe-diagnostics-1/probe-before-cookie-map.py')
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
    for name in ['docs/pet-ai-preprocess-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_ai_preprocess.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],automaticAiEnabled=False,learnedEnabled=False,
        preprocessBytes=544,savedTargetBytes=464,sourceContexts=51,newCaptureBytesRead=1488,oldLargeSourceMatricesRerun=0,
        nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=2,
        artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
        verified=['Complete preprocessing and saved-target source match51native contexts for returns,writes,lookup order,stun/count/status boundaries,door collision/search/interaction,target validity,distance thresholds and movement arguments.',
            'Existing coordinate decoder and movement state reused. Original three-species full-entry fixture reads actual compiled primary flags and invokes real preprocessing before registered root;commands/control6updates/births/fractionalwounds retain across two reloads.'],
        diagnostics=dict(initialNativeRun='Reference cookie stub incorrectly overwrote result register. Corrected preservation only;all51inputs,traces and other state writes unchanged. Native2 passes identical Content/Integration DLLs;exact first-run artifacts retained.',changedOracleReturns=changed,initialProbe='Missing emulator cookie-data map corrected before native build;pre-correction probe retained.'),
        limits=['Native status,path/collision,registry/object/search,distance and movement execution are explicit leaves;automatic world admission is not established.',
            'Control+3c/+40 birth defaults and persistence remain unknown;separate from the qualified persisted three words. Original no-status/no-door/no-saved-target input is explicitly a fixture.',
            'Post-target/special callbacks/control producer/global chain/clock and remaining world/action bindings remain unfinished. No new learned route,class certificate,assets or deployment.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-ai-preprocess-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('AI preprocessing:51source/native contexts/1008bytes,15markers/two reloads;preserved build/saves/client;AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
