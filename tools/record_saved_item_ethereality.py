#!/usr/bin/env python3
"""Bind ordered intrinsic ethereality and original NPC item preservation."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'saved-item-ethereality';WIDE=BASE/'saved-item-ethereality-retained';IRON=BASE/'saved-item-ethereality-iron'
PARENT='8b585b9642be9cda150e9024e2baf7ffc5af9e33'
def record(check=False):
    snapshot=STAGE/'source-snapshot-1.json';born=e.inventory(STAGE,WIDE,snapshot)
    old=e.load(BASE/'saved-tree-property/source-snapshot-2.json')['inputs'];now=e.load(snapshot)['inputs']
    assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/SavedItemEthereality.cs','tests/Sanctuary.Integration/SavedItemEtherealityChecks.cs'}
    assert old['projectInputs']==now['projectInputs'] and old['copiedData'].keys()==now['copiedData'].keys()
    assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert {p for p,h in old['sourceInputs'].items()if p.startswith(('src/','shared/'))and h!=now['sourceInputs'][p]}=={'src/Sanctuary.Content/EquipmentPropertyWriter.cs'}
    base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    delta=Counter({'saved-item-ethereality-check':1,'saved-item-ethereality-reload-check':2});e.cohort(STAGE,base+delta,12)
    prior=BASE/'saved-tree-property-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/saved-tree-property-native-evidence.json')
    assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
    retained=markers(prior);assert sum(retained.values())==179;e.cohort(WIDE,retained+delta,182)
    e.cohort(IRON,base+Counter({'iron-golem-cast-check':1,'iron-golem-cast-reload-check':2}),12)
    for stage in [STAGE,WIDE,IRON]:
        inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData']
        e.snapshot(snapshot,stage/'birth-inputs.json')
        for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        assert 'pass across3boots/2reloads' in(stage/'run-1.log').read_text()
        if stage!=IRON:
            saved=e.load(stage/'saved-item-ethereality-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==6
            assert len({x['Actor']for x in saved['Actors']})==6 and sum(x['Parent']!=x['Actor']for x in saved['Actors'])==3
            assert (stage/'birth-native-checks.log').read_text().count('Saved item ethereality:13824complete source contexts/692owned bases/108guard queries')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-1.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    protected['acceptedClientPatches']=41
    source_path=ROOT/'tools/reference/saved-item-ethereality-source-evidence.json';source=e.load(source_path)
    assert source['contexts']==13824 and source['ownedBases']==692 and not source['currentExecutableParity'] and len(source['completeRoutineHashes'])==6
    assert source['constants']['STAT_ITEM_MAXDURABILITY_PERCENT']==75 and source['constants']['IFLAG_ETHEREAL']==0x400000
    for group in ['inputs','artifacts']:
        for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
    probe=BASE/'saved-item-ethereality-source';assert e.sha(probe/'cases.json')==e.sha(probe/'prior-binding-1/cases.json')
    previous_source=e.load(probe/'prior-binding-1/reference.json');assert source['completeRoutineHashes']==previous_source['completeRoutineHashes']
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==198 and not status['completedClasses']
    files=e.stage_files(STAGE,WIDE,IRON,probe)+[source_path]
    for name in ['prior-binding-1','failed-binding-2']:
        folder=probe/name;assert folder.is_dir();files += [p for p in folder.rglob('*')if p.is_file()]
    for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
    files += [ROOT/p for p in ['tests/check_saved_item_ethereality.py','tests/check_iron_golem_birth.py','tools/probe_saved_item_ethereality.py','tools/reference/inferno-held-input-client-evidence.json']]
    files += [ROOT/'docs'/p for p in ['saved-item-ethereality-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']];files.append(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=198,completedClasses=[],sourceSnapshot=1,contentBuild=1,integrationBuild=1,focusedRun=1,retainedRun=1,ironRun=1,auditRun=1,
        focusedMarkers=12,retainedMarkers=182,learnedIronMarkers=12,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalActors=6,frozenRevives=3,callbackSourceContexts=13824,ownedBases=692,guardQueryControls=108,
        pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Complete older property23/durability/ethereal/physical chain,6unchanged bodies,13824sanitized contexts and692owned metadata rows. Flags precede physical writes;existing-ethereal and second applications skip.',
               'Original ordered properties use the existing physical writer and a private op13 durability graph. The live original,item RNG,original combat array layout and191-plan generic numeric catalogue remain unchanged.',
               'All3available intrinsic ethereal weapons cover learned Iron/frozen Revive contacts;Ghostflame also covers saved graph events.6original actors preserve item parents,full gear/numeric graphs,physical endpoints,wounds,resources and RNG through two reloads.',
               'Undead Crown individual-skill22remains an available original-item refusal;no field is stripped to admit it. Existing source/property/kill/slow/tree regressions retain179parent markers.'],
        limits=['Arbitrary random/native ethereal flags missing from saved schemas,full item finishing,other specialized properties,current executable/global/PvP/class/client parity and final item appearance remain open. Generated item finishing keeps its separate graph/flag path.',
                'The source binder was widened to pin stat75and all stat names. Its temporary validator also visited nonstat constants and asserted before any binary/case rewrite;the failed producer and prior artifacts are archived. Final case bytes and all6source bodies are identical to the native consumed inputs.',
                'No new effect/audio or talent/hotkey identity. Installed build,saves,configuration and accepted41client patches/shared inputs remain unchanged.'])
    e.write('saved-item-ethereality-native-evidence.json',result,files,check)
    print('Saved forced ethereality:12focused/182retained/12learned-Iron,two reloads each;6original actors;13824source/692metadata/108guard controls;71Python;198handlers/no class complete;installed world preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
