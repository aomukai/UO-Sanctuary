#!/usr/bin/env python3
"""Bind recovered owner callback,focused native comparison and preservation."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-owner-survey'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-owner-survey-source-evidence.json')
    assert source['sourceBytes']==725 and source['completeFragments']==8 and source['cases']==78
    assert source['unsupported']==1 and source['allocationContexts']==6
    assert not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    assert source['sourceCodeExecutedInOfflineEmulator'] and source['unicornVersion']=='2.1.4'
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    capture=pin(ROOT/'.local/class-skills/pet-owner-reference-20261109/owner-callback.json')
    prior=pin(ROOT/'.local/class-skills/pet-owner-reference-20261103/owner-callback.json')
    assert capture['process']!=prior['process']
    assert capture['referenceVersion']=='3.3.93847' and capture['bytesRead']==2010
    assert capture['callbackReference']['callbackRva']=='0x4b08b0'
    assert capture['callbackReference']['iteratorRva']=='0x35e6d0'
    assert capture['bytesRead']==2*sum(r['size'] for r in capture['reads'])
    assert all(r['stable'] and 'r' in r['permissions'] and (r['kind']!='code' or 'x' in r['permissions']) for r in capture['reads'])
    assert not any(capture[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
    row=capture['records'][0]
    assert row['rva']=='0x4b08b0' and row['bytes']==725 and row['allUnwindFragmentsCaptured']
    assert len(row['fragments'])==8 and all(f['captured'] and not f['invalidInstructionMarkers'] for f in row['fragments'])
    for name,digest in capture['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind in ['content','integration']:
        path=STAGE/kind/f'Sanctuary.{kind.title()}.dll'
        assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        assert e.sha(path)==e.sha(STAGE/'failed-native-1'/kind/path.name)
        log=(STAGE/(kind+'-build-1.log')).read_text()
        assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        pin(STAGE/(kind+'-build-1.log'))
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'pet-owner-survey-check':1,'pet-owner-survey-reload-check':2})
    log=e.cohort(STAGE,expected,12);assert log.count('Owner survey:78complete-current callback contexts')==1
    pin(STAGE/'birth-native-checks.log')
    failed=(STAGE/'failed-native-1/native-run-1.log').read_text()
    assert 'FileNotFoundError' in failed and 'pet-owner-survey-fixtures.json' in failed
    assert 'pet-owner-survey-check: passed' in (STAGE/'failed-native-1/birth-native-checks.log').read_text()
    pin(STAGE/'failed-native-1/native-run-1.log')
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
    for name in ['docs/pet-owner-survey-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tools/build_skill_roadmap.py',
                 'tests/check_pet_owner_survey.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],
                learnedEnabled=False,automaticAiEnabled=False,callbackLinkedToInitializer=True,
                currentAiDispatcherNewlyCertified=False,currentCallbackBytes=725,completeFragments=8,
                sourceContexts=78,assertionBoundaryContexts=1,sourceAllocationContexts=6,
                nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,
                contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=2,
                newProcessBytesRead=2010,externalStateChange=dict(previous=prior['process'],current=capture['process']),
                previousCompilerMatricesRerun=0,transientSurveyPersisted=False,
                artifacts=born['artifacts'],
                preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Complete current initializer callback semantics over explicit leaf services:count-before-filter,signed wrap,AI identity,command-first/path-fallback validity,refresh/re-read,self-target rejection,first matching duplicate and insertion order.',
                          'Native C# matches all78new source contexts once. Existing follow executor consumes the produced count;existing search wrapper consumes the produced target records. Bindings/raw profiles repeat after reload without rerunning the source matrix.',
                          'All three current demon profiles have SwarmRaw0:every supplied iterator visit counts,but no unit/target service is called. Actual player-pet membership/order remains a caller obligation.'],
                limits=['This is a transient initializer survey,not a persistent actor or automatic controller. Actual owner iterator,live command target/validation/path refresh,root event/movement/search/combat/action and learned spawn/payment/input remain separate.',
                        'Managed vector storage preserves target/count observations;raw heap layout/capacity,allocation failure and malformed source containers are not certified. One source fatal tile transition is explicitly refused by C#.',
                        'The captured callback is directly referenced by the initializer. This does not newly certify current top-level GenericPet dispatcher admission or all leaf services.',
                        'No new visual or final asset was created;existing demon effects/audio and talent/hotkey icons remain separately tracked.'],
                diagnostics=['Native1passed initial source/binding commands,then harness required a nonexistent transient survey manifest. Exact failed sources/inputs/logs/binaries are archived. Wrapper now aliases existing resource manifest for copy step;production and Integration DLLs are identical in accepted native2.'],
                inputs=inputs)
    path=ROOT/'tools/reference/pet-owner-survey-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Owner survey:725current bytes/78contexts,12native markers/two reloads;follow/search producers qualified;automatic demon AI still gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
