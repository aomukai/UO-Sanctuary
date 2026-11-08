#!/usr/bin/env python3
"""Authenticate command getter/source-survey/native world composition offline."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-command-target'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-command-target-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['sourceBytes']==262
    assert source['sourceCases']==35 and source['assertionContexts']==2
    assert source['sourceCodeExecutedInOfflineEmulator'] and source['unicornVersion']=='2.1.4'
    assert not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    survey=pin(ROOT/'tools/reference/pet-owner-survey-source-evidence.json')
    assert survey['sourceBytes']==725 and survey['cases']==78
    for name,digest in survey['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-2.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind,build in [('content',2),('integration',1)]:
        path=STAGE/kind/f'Sanctuary.{kind.title()}.dll'
        assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        pin(STAGE/f'{kind}-build-{build}.log')
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'pet-owner-survey-check':1,'pet-owner-survey-reload-check':2,
                      'pet-command-target-check':1,'pet-command-target-reload-check':2})
    log=e.cohort(STAGE,expected,15)
    assert log.count('Command target:35complete-current getter contexts')==1
    assert log.count('Owner survey:78complete-current callback contexts')==1
    pin(STAGE/'birth-native-checks.log')
    saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Owners'])==2 and len(saved['Entries'])==3
    assert len({entry['Body'] for entry in saved['Entries']})==3
    assert [entry['Command']['Type4c'] for entry in saved['Entries']]==[2,1,2]
    for index,entry in enumerate(saved['Entries']):
        assert entry['Command']['Handle']==entry['Body']
        assert entry['Command']['Target48']==(saved['Owners'][1] if index==1 else saved['Target'])
    pin(STAGE/'pre-review-1/source-snapshot-1.json')
    pin(STAGE/'pre-review-1/content-build-1.log')
    # This archive predates native testing. It preserves the review-triggering
    # raw-field naming issue;its unaccepted DLL is not the tested package.
    before=pin(STAGE/'pre-review-1/sources/src/Sanctuary.Content/GenericPetOwnerSurvey.cs').decode()
    after=(ROOT/'src/Sanctuary.Content/GenericPetOwnerSurvey.cs').read_text()
    assert 'int ClassId,' in before and 'info.ClassId' in before
    assert 'int Id8,' in after and 'info.Id8' in after
    installed=pin(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert e.sha(Path(installed['runtimeRoot'])/name)==digest,name
    protected=pin(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count
        assert {str(p.relative_to(folder)):e.sha(p) for p in folder.rglob('*') if p.is_file()}==protected[kind]
    client=pin(ROOT/'tools/reference/ring-cast-client-evidence.json');clientInputs=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']
    assert client['patches']==len(clientInputs['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for name,digest in clientInputs[group].items():assert e.sha(ROOT/name)==digest,name
    for name in ['docs/pet-command-target-checkpoint.md','docs/pet-owner-survey-checkpoint.md',
                 'docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_command_target.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],
                learnedEnabled=False,automaticAiEnabled=False,currentGetterBytes=262,newSourceContexts=35,
                assertionContexts=2,retainedSurveyContexts=78,sourceRegistryTraversal='Shared GenericPetRegistry',
                nativeWorldRegistry='GenericPetWorldIdentity',originalRegisteredDemonSpecies=3,
                nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,
                contentBuild=2,integrationBuild=1,sourceSnapshot=2,nativeRun=1,newProcessReads=0,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Whole current command getter guard/type/ID/linked registry semantics;no added predicate or stale-command clearing.',
                          'Existing owner survey reads actual registered original commands through the live native registry. Existing binding observes controller dictionary replacement. Wrong role,missing/unsupported target and unregistered actor cases are checked.',
                          'Original Goatman/Tainted/Defiler bodies,birth snapshots,fractional wounds,mana,Warlock/Witch ownership and typed player/monster commands survive two native reloads. No movement or recapture is required to query saved commands.',
                          'Survey raw+8 field now namedId8 rather thanClassId;all78numeric controls retained. Native composed fixture supplies serial,not monster class. All three defaultSwarmRaw0 profiles bypass the uncertain row lookup.'],
                limits=['Actual owner iterator,live survey unit/row/validity/path services and complete initializer consumption/scratch lifetime remain unfinished. The native composition supplies those remaining services explicitly.',
                        'Why the current callback uses raw+8 for6bc90row lookup remains uncertain. No +4class substitution,correction of source behavior or automatic gameplay admission is inferred.',
                        'Root event scheduling,movement/search/combat/actions,birth paths and learned summon/DeathMark payment/input remain gated. No class certificate follows from these component checks.',
                        'No new effect or final asset was emitted. Existing demon effects/audio and talent/hotkey icons remain separately tracked.'],
                diagnostics=['Content1/snapshot1 were retained in pre-review-1 before any native test. Field naming was corrected and content rebuilt;accepted package isC2/I1/snapshot2/native1. No failed native run occurred.'],inputs=inputs)
    out=ROOT/'tools/reference/pet-command-target-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Command target:35new/78retained source contexts;15native markers,two reloads of original commands/bodies;213handlers;AI still gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
