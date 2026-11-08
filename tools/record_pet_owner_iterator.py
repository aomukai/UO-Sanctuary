#!/usr/bin/env python3
"""Authenticate player pet-store iterator and original demon initialization."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-owner-iterator'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-owner-iterator-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['sourceBytes']==832 and source['sourceContexts']==33
    assert source['assertionContexts']==7 and source['nullGameDereferenceContexts']==1 and source['growthContexts']==1
    assert source['sourceCodeExecutedInOfflineEmulator'] and not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    captures=[pin(ROOT/'.local/class-skills/warlock-runtime-reference'/name)for name in
              ['callees-pet-owner-iterator-20261109.json','callees-pet-owner-bucket-20261109.json']]
    for capture in captures:
        assert capture['directCallTargetsVerified'] and all(row['allUnwindFragmentsCaptured']for row in capture['records'])
        for name,digest in capture['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind in ['content','integration']:
        path=STAGE/kind/f'Sanctuary.{kind.title()}.dll';assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'pet-command-target-check':1,'pet-command-target-reload-check':2})
    log=e.cohort(STAGE,expected,12);assert log.count('Owner iterator:33complete-current controls/832bytes')==1
    pin(STAGE/'birth-native-checks.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    types=pin(ROOT/'tools/reference/d2r-named-equipment/json/pettype.json')
    assert len(types)==21 and sorted(row['lineNumber']for row in types.values())==list(range(1,22))
    assert types['demon']['lineNumber']==20 and types['binddemon']['lineNumber']==21
    failed=ROOT/'.local/class-skills/pet-owner-iterator-source/failed-probe-1'
    pin(failed/'probe_pet_owner_iterator.py');log=pin(failed/'probe-run.log').decode()
    assert 'UC_ERR_READ_UNMAPPED' in log
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
    for name in ['docs/pet-owner-iterator-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_owner_iterator.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],learnedEnabled=False,automaticAiEnabled=False,
                currentIteratorBytes=832,newSourceContexts=33,freshCaptureBytesRead=2784,oldLargeSourceMatricesRerun=0,
                nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Complete current outer/bucket routines execute offline against explicit pet-store graphs. C# matches visitation and service ordering in33contexts,including duplicates,flags,lookup and callback mutations,bucket21,65-entry growth. Existing registry and initializer reused.',
                          'Original registered three-species demons/Warlock-Witch owners consume supplied store graphs through actual world registry,command/life initialization and finishing. Original births,fractional wounds and typed commands survive two reloads. Graphs are explicit fixtures,recreated after load,not a persisted native pet-store claim.'],
                limits=['Actual live player-pet store membership/insertion/removal/order and compiled type-count supplier remain unbound. Named type IDs1..21 give the fixture its explicit upper bound22;DeathMark approved roster order is not silently generalized.',
                        'Managed list replaces raw temporary vector allocator. Seven original assertion contexts and one distinct null-game dereference are refused;raw player-data/null pointer layouts outside supported context are not reconstructed.',
                        'Persistent AI-control words,timed events,remaining native world/search/path/movement/combat/action services and learned summon/DeathMark casts remain unfinished. No automatic AI or class certificate.',
                        'No new assets/deployment;effects/audio and talent/hotkey icons remain separate.'],
                diagnostics=['First offline probe stopped on original null-game dereference;exact script/log archived. Corrected probe labels that boundary separately from source assertions. No native build/run failure.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-owner-iterator-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Owner iterator:33source/native comparisons,12markers/two reloads;213handlers;automatic AI gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
