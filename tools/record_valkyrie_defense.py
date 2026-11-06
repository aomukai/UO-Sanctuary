#!/usr/bin/env python3
"""Bind source dispatch and real original-body defensive consumers."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-defense';WIDE=BASE/'valkyrie-defense-retained';COMBAT=BASE/'valkyrie-defense-combat';TAIL=BASE/'valkyrie-defense-tail';SOURCE=BASE/'source-avoidance-source'
PARENT='16f164bd5067fb6a0487750d8c02ca1d057caf46'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-1.json');old=e.load(ROOT/'tools/reference/valkyrie-damage-native-evidence.json');extra=Counter({'valkyrie-defense-check':1,'valkyrie-defense-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-damage',STAGE,54),(BASE/'valkyrie-damage-retained',WIDE,362)]:
  path=previous/'birth-native-checks.log';assert e.sha(path)==old['files'][str(path.relative_to(ROOT))];e.cohort(stage,markers(path)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Valkyrie defense:16384complete source contexts;57original loadouts/330items;1368bound mode/Command decisions per boot;')==3
  saved=e.load(stage/'valkyrie-defense-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==57 and sum(len(h['Items'])for h in saved['Bodies'])==330
  assert len({i for h in saved['Bodies']for i in h['Items']})==330
  for h in saved['Bodies']:
   body=json.loads(h['Text']);rng=h['Random'];assert body['Owner']==saved['Owner']==rng['Owner']and rng['Actor']==h['Body']
   assert body['EquipmentDefenseProfile']=='valkyrie-inherited-defense-v1'and rng['ValkyrieBinding']==body['EquipmentWeapons']['Identity']and h['Life']>0
   for text in h['Payloads']:
    item=json.loads(text);assert item['Equipment']['Actor']==h['Body']and item['Equipment']['Owner']==saved['Owner']and item['Metadata']['Flags']==0x82010
 # The focused Paladin consumer chain retains its actual left/right/quick inputs.
 paladin=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 for family in ['sacrifice','smite','vengeance']:paladin.update({family+'-check':1,family+'-reload-check':2})
 paladin.update({'smite-result':1})
 for name in ['sacrifice-allocation','sacrifice-contact','poison-dagger-contact','smite-contact','vengeance-contact']:paladin.update({name+'-check':3})
 for skill in ['sacrifice-allocation','smite-contact','vengeance-contact']:
  for mode in ['left','right','quick']:paladin.update({skill+'-'+mode+'-check':3,skill+'-'+mode+'-result':3})
 e.cohort(COMBAT,paladin,88)
 # The original learned-Tail driver/configuration is reused unchanged.
 sys.path.insert(0,str(ROOT/'tests'))
 from check_dragon_tail import configuration
 cfg=configuration(False);wanted=Counter({'iron-golem-birth-check':3})
 for family in ['iron-golem-items','npc-resource-callbacks',*cfg['extra_families']]:wanted.update({family+'-check':1,family+'-reload-check':2})
 for name in cfg.get('extra_checks',()):wanted.update({name:3})
 for family in cfg.get('extra_target_input_commands',()):wanted.update({family+'-check':3,family+'-result':3})
 for key,value in cfg.get('extra_wait_results',{}).items():
  if key.endswith('-reload-check'):wanted.update({value:2})
  elif key.endswith('-check'):wanted.update({value:1 if key.removesuffix('-check')in cfg['extra_families']else 3})
 e.cohort(TAIL,wanted,48)
 for stage in [COMBAT,TAIL]:
  snapshot(STAGE/'source-snapshot-1.json',stage/'birth-inputs.json');inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts']and inputs['copiedData']==born['copiedData']
  for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
 for name in ['content-build-1.log','integration-build-1.log']:
  text=(STAGE/name).read_text();assert all(n in text for n in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 proofpath=ROOT/'tools/reference/source-avoidance-source-evidence.json';proof=e.load(proofpath);assert proof['contexts']==16384 and len(proof['completeRoutineHashes'])==7 and not proof['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 assert not(SOURCE/'sanitizer.log').read_text();cases=e.load(SOURCE/'cases.json');assert len(cases)==16384
 assert any(len(c['Layers'])>32 and c['Layers'][32]==[0,99]for c in cases)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
 files=e.stage_files(STAGE,WIDE,COMBAT,TAIL)+[proofpath,ROOT/'tools/probe_source_avoidance.py',ROOT/'tests/check_valkyrie_defense.py',ROOT/'tests/check_valkyrie_defense_combat.py',Path(__file__).resolve()]
 files.extend(p for p in SOURCE.rglob('*')if p.is_file())
 files += [ROOT/'docs'/n for n in ['valkyrie-defense-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,
  sourceSnapshot=1,contentBuild=1,integrationBuild=1,focusedRun=1,retainedRun=1,combatRun=1,tailRun=1,auditRun=1,focusedMarkers=54,retainedMarkers=362,paladinConsumerMarkers=88,tailMarkers=48,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceContexts=16384,completeSourceRoutines=7,originalBodies=57,originalItems=330,boundModeDecisionsPerBoot=1368,
  pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Complete older block/dodge/weapon-block ordering with actual source seed continuation matches16384contexts;header-bound flags/stats,copy32beforematching,loweruntypedreplacement and after-cap controls;mode/rate/stat/hand/composite are declared services.',
   'Optional original Valkyrie defense profile binds inherited own-item/Command values to persisted defender stream. Actual central guaranteed-accuracy and ordered NPC melee/Dodge flags use one draw;real incoming fire Avoid prevents HP and failedAvoid reachesHP. No phantom ToBlock28shield. Exact original items,payloads,wounds,profile,seed and legacyJSON survive actual reloads/parking.',
   'All359parent broad markers retain;affected learned Sacrifice/Smite/Vengeance/PoisonDagger and DragonTail input/contact consumers pass separate88/48marker cohorts. Other NPCs retain prior shield behavior;player running policy remains unchanged.'],
  limits=['World admission is the explicitly immobile prepared body. Supplied source2/15modes exercise Evade but current moving controller/mode ownership remains unfinished;CantWalkfalse refuses before accuracy/defender RNG. Reaction uses labelled native marker/audio without new recovery/cast-interruption lock.',
   'No current executable/passive-statlist/materialized item-skill/composite equivalence or full incoming event/contact certificate. Original item events/skills,full external offensive stat composition,nonzeroSunder,rate/NecroPet AI/contact,current initializer/world seed and learned casting/placement/payment/replacement/input remain unfinished. Full gameplay/client/assets/effects/audio/talent/hotkey acceptance stays separate;no class complete/deployment.'])
 e.write('valkyrie-defense-native-evidence.json',result,files,check)
 print('Valkyrie defense:54focused/362retained/88Paladin/48Tail,two reloads;16384source cases;57originals330items,actual melee/fire consumers;71Python/573unchanged decisions;202handlers,moving controller/cast gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
