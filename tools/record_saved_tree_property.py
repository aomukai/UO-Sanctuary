#!/usr/bin/env python3
"""Bind saved approved skill-tree projection and original NPC equipment."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'saved-tree-property';WIDE=BASE/'saved-tree-property-retained';IRON=BASE/'saved-tree-property-iron'
PARENT='9c93f34d2aa73772b0e6d159f26b3e8c6deac5e0'
def record(check=False):
    snapshot=STAGE/'source-snapshot-2.json';born=e.inventory(STAGE,WIDE,snapshot)
    old=e.load(BASE/'npc-rest-in-peace/source-snapshot-3.json')['inputs'];now=e.load(snapshot)['inputs']
    assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'tests/Sanctuary.Integration/SavedTreePropertyChecks.cs'}
    assert old['projectInputs']==now['projectInputs'] and old['copiedData'].keys()==now['copiedData'].keys()
    assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    first=e.load(STAGE/'source-snapshot-1.json')['inputs']
    assert {p:h for p,h in first['sourceInputs'].items()if p.startswith(('src/','shared/'))}=={p:h for p,h in now['sourceInputs'].items()if p.startswith(('src/','shared/'))}
    assert {p for p,h in old['sourceInputs'].items()if p.startswith(('src/','shared/'))and h!=now['sourceInputs'][p]}=={'src/Sanctuary.Content/EquipmentPropertyWriter.cs'}
    base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    delta=Counter({'saved-tree-property-check':1,'saved-tree-property-reload-check':2});e.cohort(STAGE,base+delta,12)
    prior=BASE/'npc-rest-in-peace-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/npc-rest-in-peace-native-evidence.json')
    assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
    retained=markers(prior);assert sum(retained.values())==176;e.cohort(WIDE,retained+delta,179)
    e.cohort(IRON,base+Counter({'iron-golem-cast-check':1,'iron-golem-cast-reload-check':2}),12)
    for stage,run in [(STAGE,2),(WIDE,2),(IRON,2)]:
        inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData']
        e.snapshot(snapshot,stage/'birth-inputs.json')
        for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        assert 'pass across3boots/2reloads' in(stage/f'run-{run}.log').read_text()
        if stage!=IRON:
            saved=e.load(stage/'saved-tree-property-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==4
            assert len({x['Actor']for x in saved['Actors']})==4
            assert (stage/'birth-native-checks.log').read_text().count('Saved tree properties:144fixed class/tree/value controls')==3
    for kind,run in [('content',1),('integration',2)]:
        text=(STAGE/f'{kind}-build-{run}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    protected['acceptedClientPatches']=41
    decision=ROOT/'tools/reference/item-tree-packing-decision.json';assert e.load(decision)['answer']=='Use the consistent class/tree encoding'
    sunder=ROOT/'tools/reference/necro-sunder-preflight.json';review=e.load(sunder)
    assert not review['currentReducerVerified'] and not review['processMemoryRead'] and review['charmsExcludedByExistingPolicy'] and len(review['charms'])==18 and len(review['passives'])==7
    for p,h in review['inputs'].items():assert e.sha(ROOT/p)==h,p
    source_path=ROOT/'tools/reference/current-item-affixes-source-evidence.json';source=e.load(source_path)
    assert source['cases']==14888 and source['treeControls']==216 and not source['currentExecutableParity']
    for group in ['inputs','artifacts']:
        for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==198 and not status['completedClasses']
    assert 'Unrepresented original was invented/rerolled/transferred: Cutthroat1' in(WIDE/'failed-retained-1/birth-native-checks.log').read_text()
    assert 'That named definition is not available.' in(IRON/'failed-iron-1/birth-native-checks.log').read_text()
    files=e.stage_files(STAGE,WIDE,IRON)+[decision,sunder,source_path]
    for stage,names in [(STAGE,['prior-focused-1']),(WIDE,['failed-retained-1']),(IRON,['failed-iron-1'])]:
        for name in names:
            folder=stage/name;assert folder.is_dir();files += [p for p in folder.rglob('*')if p.is_file()and p.suffix in('.cs','.dll','.log','.json','.py')]
    for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
    files += [ROOT/p for p in ['tests/check_saved_tree_property.py','tests/check_iron_golem_birth.py','tools/record_necro_sunder_preflight.py','tools/reference/inferno-held-input-client-evidence.json']]
    files += [ROOT/'docs'/p for p in ['saved-tree-property-checkpoint.md','necro-sunder-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']];files.append(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=198,completedClasses=[],sourceSnapshot=2,contentBuild=1,integrationBuild=2,focusedRun=2,retainedRun=2,ironRun=2,auditRun=2,
        focusedMarkers=12,retainedMarkers=179,learnedIronMarkers=12,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalActors=4,fixedTreeContexts=144,retainedSourceContexts=14888,retainedSourceTreeControls=216,
        pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Saved ordinary skilltab reuses the approved CurrentTreePropertyWriter and existing fixed-layer function21,without rerolling or changing the original191-plan numeric catalogue.144fixed class/tree/value native controls;all24parameters;invalid-parameter atomicity remains explicit.',
               'Whole Bartuc/Jade items cover actual learned legacy/graph Iron casts and frozen Revive contacts,then retain exact original parents,gear,numeric layers,wounds,resources and seeds across two reloads.',
               'Sunder preflight pins6current stat IDs,7Necromancer passive maps and18current300-valued item definitions;missing reducer and existing charm exclusion remain separate explicit boundaries.'],
        limits=['Random Warlock tree properties and other specialized item/current-engine/PvP/global/class/client scopes remain open. No Sunder consumer enabled or gameplay charm source invented.',
                'Archived broad failure was the obsolete Bartuc refusal;the initial replacement Tomb Reaver was unavailable under current item policy. Available Ethereal Edge retains the original ethereality-projection refusal and original-item/resource assertions. Production remains build1.',
                'No new visual/audio or talent/hotkey identity;existing summon placeholders remain pending. Installed build,saves,configuration and accepted41client patches/shared inputs remain unchanged.'])
    e.write('saved-tree-property-native-evidence.json',result,files,check)
    print('Saved tree properties:12focused/179retained/12learned-Iron,two reloads each;4original actors;144fixed tree contexts/14888retained source controls;71Python;198handlers/no class complete;installed world preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
