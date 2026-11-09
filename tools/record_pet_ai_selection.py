#!/usr/bin/env python3
"""Authenticate current AI descriptor selection,metadata tables and switch predicate."""
import argparse,json,subprocess,hashlib
from probe_warlock_runtime import Image
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-ai-selection'
PARENT='fd34772d0a0bdbc021f7fe7d860c2c0d19bab663'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-ai-selection-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['selectorBytes']==187 and source['switchBytes']==499 and source['sourceContexts']==66
    assert source['sourceCodeExecutedInOfflineEmulator'] and not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    data=pin(ROOT/'tools/reference/pet-ai-tables-data-evidence.json')
    assert data['referenceVersion']=='3.3.93847' and data['monsterRows']==751 and data['normalDescriptors']==155
    assert data['aiOffset']==82 and data['aiSigned'] and data['sourceBinaryRecoveredByHash'] and not data['specialDescriptorsPublished']
    for name,digest in data['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/callees-pet-ai-switch-20261109.json')
    assert capture['bytesRead']==1558 and capture['directCallTargetsVerified'] and capture['records'][0]['bytes']==499
    assert capture['records'][0]['allUnwindFragmentsCaptured']
    for name,digest in capture['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    for name in ['GenericPetAiEntry.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','DeathMarkWorld.cs','GenericPetEventDispatch.cs','GenericPetTimedEvents.cs']:
        path='src/Sanctuary.Content/'+name
        assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    descriptor=pin(ROOT/'.local/class-skills/warlock-runtime-reference/generic-pet-candidate.json')
    image=Image();pin(image.reference)
    assert descriptor['genericPetIndex']==148 and descriptor['aiTableRva']=='0x1d18810' and descriptor['recordStride']==32
    raw=image.bytes(0x1d18810+148*32,32);descriptorHash=hashlib.sha256(raw).hexdigest()
    assert descriptorHash==next(row['sha256']for row in descriptor['reads']if row.get('index')==148)
    assert int.from_bytes(raw[:4],'little')==0 and int.from_bytes(raw[16:24],'little')==image.base+0x4b60c0
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-2.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind in ['content','integration']:
        path=STAGE/kind/f'Sanctuary.{kind.title()}.dll';assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        log=pin(STAGE/f'{kind}-build-2.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-ai-selection-check':3})
    log=e.cohort(STAGE,expected,15);assert log.count('AI selection:66current contexts/686bytes')==3
    pin(STAGE/'birth-native-checks.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    assert log.count('AI control:explicit three-word registration')==3
    for i,entry in enumerate(saved['Entries']):
        control=entry['Command']['Control']
        assert control==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    assert log.count('Timed event root:original demon callback')==9 and log.count('AI entry original:typed event->full prepared entry')==9
    reviewed=STAGE/'pre-review-1'
    e.cohort(reviewed,expected,15);pin(reviewed/'birth-native-checks.log')
    before=pin(reviewed/'sources/src/Sanctuary.Content/GenericPetAiSelection.cs').decode()
    assert before.replace('int Initialize,int Think,int Finish','int Function8,int Think,int Function24').replace('row.Initialize<0','row.Function8<0').replace('row.Finish<0','row.Function24<0')==(ROOT/'src/Sanctuary.Content/GenericPetAiSelection.cs').read_text()
    before=pin(reviewed/'sources/data/Sanctuary/Balance/generic-pet-ai-descriptors.json')
    after=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-ai-descriptors.json').read_text())
    for row in before:row['function8']=row.pop('initialize');row['function24']=row.pop('finish')
    assert before==after
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
    for name in ['docs/pet-ai-selection-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_ai_selection.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],learnedEnabled=False,automaticAiEnabled=False,
                selectorBytes=187,switchBytes=499,sourceContexts=66,monsterAiIndices=751,normalDescriptors=155,newPredicateCaptureBytesRead=1558,oldLargeSourceMatricesRerun=0,
                nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Complete187-byte selector and499-byte switch predicate match66native contexts with actual nested predicate calls for states10..12. Signed bounds,cached primary pointer/late field read,normal/special arithmetic,status/selector/options/metadata guards and late unit flags/mode are covered.',
                          'All751compiled monster signed AI indices match pinned text names/155ordinals;all155normal descriptors retain type,padding and function identities. Older binary/text recovered by exact recorded hash without re-extraction. Runtime catalog loads all rows and refuses unpublished special descriptors.',
                          'Original three-species prepared entries now read real source class AI metadata and selector1view instead of constant type. Descriptor148/type0/think4b60c0verified. Original event/root/control/command/birth/wound cohort survives two real reloads with six control updates per actor. Existing AI-entry/root/controller/event production code is identical to parent.'],
                diagnostics=['Initial cohort passed;review renamed unqualified function slots to offsets8/24 and regenerated matching JSON fields. Numeric data and selection source match archivedpre-review-1 after exact label replacements. Accepted C2/I2/snapshot2/native2;no failed mechanical/build run.'],
                limits=['Live native status projection and special descriptor contents remain unqualified;selector Original calls require explicitly supplied eligibility only for states10..12. No implicit native selector alias or unqualified special state is admitted.',
                        'Existing learned curse receiver includes separate native restrictions and is unchanged. Low-level exact switch predicate is not an extension of that receiver admission. Control/startup defaults,pre/post helpers,special callbacks,global chain/clock and remaining world/services remain unfinished.',
                        'No new learned route,automatic timer,class certificate,deployment or effect/audio/icon assets.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-ai-selection-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('AI selection:66source/native contexts,751AI fields/155descriptors,15markers/two reloads;213handlers;AI gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
