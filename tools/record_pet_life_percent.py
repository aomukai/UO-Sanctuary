#!/usr/bin/env python3
"""Authenticate current life percent/native initializer/scoring checkpoint."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-life-percent'
PARENT='17c151a70d64eb134285c4d84eae70429209f401'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-life-percent-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['sourceBytes']==216 and source['sourceContexts']==57
    assert source['sourceCodeExecutedInOfflineEmulator'] and not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-2.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind,build in [('content',1),('integration',2)]:
        path=STAGE/kind/f'Sanctuary.{kind.title()}.dll';assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        log=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        pin(STAGE/f'{kind}-build-{build}.log')
    assert e.sha(STAGE/'content/Sanctuary.Content.dll')==e.sha(STAGE/'failed-native-1/content/Sanctuary.Content.dll')
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'pet-command-target-check':1,'pet-command-target-reload-check':2,
                      'pet-life-percent-check':1,'pet-life-percent-reload-check':2})
    log=e.cohort(STAGE,expected,15);assert log.count('Life percent:57complete-current arithmetic controls')==1
    pin(STAGE/'birth-native-checks.log')
    saved=pin(STAGE/'pet-life-percent-fixtures.json')
    assert saved['Generation']==2 and saved['Percent']==49
    assert saved['Life256']%256==255 and saved['Maximum256']>saved['Life256']
    commands=pin(STAGE/'pet-command-target-fixtures.json')
    assert commands['Generation']==2 and len(commands['Entries'])==3
    # These policy changes are field/parameter renames only. Their arithmetic
    # stays byte-for-byte text-identical after the explicit replacements.
    substitutions={
        'src/Sanctuary.Content/GenericPetTargetScoring.cs':{'Frame100':'PetLifePercent','Frame104':'OwnerLifePercent','Frame108':'GatherValue108'},
        'src/Sanctuary.Content/GenericPetRatingRouting.cs':{'frame100':'petLifePercent'},
        'tests/Sanctuary.Integration/GenericPetTargetScoringChecks.cs':{'context.Frame100':'context.PetLifePercent'}}
    for name,replacements in substitutions.items():
        before=subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT,text=True)
        for a,b in replacements.items():before=before.replace(a,b)
        assert (ROOT/name).read_text()==before,name
    failed=pin(STAGE/'failed-native-1/birth-native-checks.log').decode()
    assert 'No independent fixture corridor near 6400,3800' in failed and 'pet-command-target-check: passed' in failed
    before=pin(STAGE/'failed-native-1/sources/tests/Sanctuary.Integration/GenericPetLifePercentChecks.cs').decode()
    after=(ROOT/'tests/Sanctuary.Integration/GenericPetLifePercentChecks.cs').read_text()
    assert before.replace('6400,30,3800','6400,30,3400').replace('new(6401,3800,0)','new(owner.X+1,owner.Y,owner.Z)')==after
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
    for name in ['docs/pet-life-percent-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_life_percent.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],learnedEnabled=False,automaticAiEnabled=False,
                currentLifeHelperBytes=216,newSourceContexts=57,retainedGetterContexts=35,newProcessReads=0,
                oldLargeSourceMatricesRerun=0,scoringPoliciesChanged='Names only;exact replacement comparison against parent',
                nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Complete captured216-byte life/max arithmetic matches57native comparisons with available stat6/7 services and null-unit case. Each FP8operand shifts separately before signed division;zero whole max returns0;no0..100clamp.',
                          'Original registered demon initializer now reads real existing SkillHealth fields for pet/owner. A49percent FP wound appears as50percent from rounded player hits. Existing type8scorer/type13router consume the actual produced fields;captured earlier life remains frozen.',
                          'New original wounded Goatman/birth/max/fraction/percentage survive two reloads. Existing three-species typed-command/Warlock-Witch body tests use the new native property binding after load. Player life is explicitly restored for fixture cleanup;injured-player persistence/regen suppression is not claimed.'],
                limits=['Native health projection supports class players and original prepared Warlock demons;unclassified live units are explicitly refused. It reuses existing life accounting,not raw D2heap/stat-root reconstruction or missing metadata equivalence.',
                        'Owner iteration/order,persistent AI-control words,remaining live predicates/path/event/movement/search/action services and automatic/learned casts remain gated. No class certificate follows.',
                        'Life-field renaming leaves existing scorer/router arithmetic unchanged. Historical fixture JSONkeys frame100/104/108 remain as recorded source inputs.',
                        'No new visual/assets/deployment;existing effects/audio and talent/hotkey icons remain separate.'],
                diagnostics=['Native1passed arithmetic controls and original command cohort,then failed because fixturefirstRow3800equaled the corridor loop upper bound. Exact failed source/inputs/logs/binaries archived. Fixture starts3400and uses actual placed owner coordinates;productionDLLidentical in accepted native2.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-life-percent-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Life percent:57source/native controls,actual health->initializer->scoring,15native markers/two reloads;213handlers;AI gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
