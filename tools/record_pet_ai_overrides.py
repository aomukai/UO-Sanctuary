#!/usr/bin/env python3
"""Authenticate both current override callbacks,default initialization and native admission scope."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-ai-overrides';PARENT='ea96abe123650d6fb594949060adf5bf3ce9f562'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-ai-overrides-source-evidence.json')
    assert source['overrideBytes']==1715 and source['initializerBytes']==360 and source['composedBytes']==3410 and source['sourceContexts']==74
    assert source['sourceCodeExecutedInOfflineEmulator']and not source['processMemoryRead']and not source['targetProcessCodeExecuted']
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    data=pin(ROOT/'tools/reference/pet-ai-overrides-data-evidence.json')
    assert data['rows']==2 and data['stride']==16 and not data['controlProducerVerified']and not data['birthDefaultsPublished']
    for name,h in data['artifacts'].items():assert e.sha(ROOT/name)==h,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/callees-pet-override-initialize-20261109.json')
    assert capture['directCallTargetsVerified']and capture['bytesRead']==1280 and capture['records'][0]['bytes']==360 and capture['records'][0]['allUnwindFragmentsCaptured']
    assert all(f['invalidInstructionMarkers']==0 for f in capture['records'][0]['fragments'])
    assert not capture['gameMemoryWritten']and not capture['processInput']and not capture['securitySettingsChanged']and not capture['ptraceAttach']
    for name,h in capture['artifacts'].items():assert e.sha(ROOT/name)==h,name
    for name in ['GenericPetAiEntry.cs','GenericPetAiSelection.cs','GenericPetAiPreprocess.cs','GenericPetAiPostTarget.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetMovement.cs','GenericPetOwnerSurvey.cs','GenericPetOwnerIterator.cs','GenericPetSearch.cs','DeathMarkWorld.cs','GenericPetEventDispatch.cs','GenericPetTimedEvents.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    path='tests/Sanctuary.Integration/GenericPetAiEntryChecks.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode();after=(ROOT/path).read_text()
    assert before.split('    internal static void Original')[0]==after.split('    internal static void Original')[0]
    assert 'GenericPetAiOverrideTables.At' in after and 'GenericPetAiOverrideChecks.Original' in after and 'int index=sourceClass%2;'in after
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-ai-overrides-check':3})
    log=e.cohort(STAGE,expected,15);assert log.count('AI overrides:74current contexts/3410composed bytes')==3
    assert log.count('AI override original:index0 actual table callback')==6 and log.count('AI override original:index1 actual table callback')==3
    assert log.count('AI entry original:typed event->full prepared entry')==9 and log.count('Timed event root:original demon callback')==9
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    for i,entry in enumerate(saved['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
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
    for name in ['docs/pet-ai-overrides-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_ai_overrides.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],automaticAiEnabled=False,learnedEnabled=False,
        overrideBytes=1715,initializerBytes=360,composedBytes=3410,sourceContexts=74,newCaptureBytesRead=1280,oldLargeOfflineMatricesRerun=0,
        nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
        artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
        verified=['Both complete current override callbacks,default initializer,copy,survey(SwarmRaw0)and squared distance match74native contexts for guards,registry/validity,initialization/iteration,coordinates/leash,fallback/moving rereads,0/0search,high48,event removal/follow/idle and logical cleanup.',
            'Pinned two-row override table loaded at runtime without publishing startup defaults. Original3species full-entry selects both actual table callbacks with explicit header/mode/search fixtures;native owner registry,shared survey and registered default root are used. Commands/control6updates/births/fractionalwounds retain through two reloads.',
            'Pure-case event removal uses actual prepared timed store and preserves unrelated events. Existing AI entry,pre/post,default initialization,root,store and search production files unchanged;only new override composition and native entry fixture added.'],
        diagnostics=[],limits=['Control State0/Mode10=1/Field14indices are supplied fixtures;authentic producer/startup unknown. No source-header mapping from native animation mode is implied.',
            'Default override initializer does not consume commands,query life or persist control. Unwritten source frame tails remain unknown. Owner membership is explicit;non-default survey branches retain prior separate qualification.',
            'Follow/default/search/moving/missing-owner actions remain service boundaries. Native prepared fixture runs actual registered root but supplies high48=1 search result. Raw allocator ABI,exceptions and arbitrary heap/frame contexts are not certified.',
            'Global event chain/clock,live owner-store production and remaining status/world/path/combat admission are unfinished. No new learned route,class certificate,assets or deployment.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-ai-overrides-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('AI overrides:74source/native contexts/3410bytes,two actual callbacks,15markers/two reloads;AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
