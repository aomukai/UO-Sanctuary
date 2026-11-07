#!/usr/bin/env python3
"""Bind prepared automatic Valkyrie A1/original contacts and retained checks."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-ground-combat';WIDE=BASE/'valkyrie-ground-combat-retained';ISO=BASE/'valkyrie-ground-combat-isolate';SHARED=BASE/'valkyrie-ground-combat-shared'
PARENT='e1da01a8fe34452569d61545eda863c0610d26cb'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json')
 first=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-3.json')['inputs']
 assert first['copiedData']==final['copiedData']and first['projectInputs']==final['projectInputs']
 assert {p:h for p,h in first['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in final['sourceInputs'].items()if not p.startswith('tests/')}
 assert {p for p,h in first['sourceInputs'].items()if final['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/ValkyrieGroundCombatChecks.cs'}
 sys.path.insert(0,str(ROOT/'tests'));from check_valkyrie_ground_combat import configuration
 parent=e.load(ROOT/'tools/reference/valkyrie-initializer-native-evidence.json');extra=Counter({'valkyrie-ground-combat-check':1,'valkyrie-ground-combat-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-initializer',STAGE,111),(BASE/'valkyrie-initializer-retained',WIDE,419)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected(configuration(isolate=True)),18);e.cohort(SHARED,expected(configuration(shared=True)),255)
 phrase='Valkyrie ground combat:57unfiltered original loadouts;automatic AI67 transformed A1 hit/miss,pre-action RNG/HP silence,departed targets/interruption,end mode;original items/resources/wounds/RNG and actual pre-action windup through two reloads;supplied timing bases and learned cast remain separate'
 itemcounts={}
 for stage,run in [(STAGE,1),(WIDE,1),(ISO,3),(SHARED,1)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  s=e.load(stage/'valkyrie-ground-combat-fixtures.json');assert s['Generation']==2 and len(s['Bodies'])==57 and len({h['Body']for h in s['Bodies']})==57
  items=0
  for i,h in enumerate(s['Bodies']):
   b=json.loads(h['Text']);motion=b['GroundMotion'];controller=motion['World']['Controller'];resources=json.loads(h['Resources'])
   assert b['Owner']==s['Owner']==resources['Owner']and resources['Actor']==h['Body']==h['Random']['Actor']
   assert motion['AttackRate']==100 and motion['WalkVelocityPercent']==75 and motion['RunVelocityPercent']==125
   assert b['EquipmentContactProfile']=='valkyrie-original-proc-contact-v1'and resources['ItemEvents']is not None
   assert len(h['Items'])==sum(a['Success']for a in b['Equipment']['Attempts']);items+=len(h['Items'])
   assert controller['Mode']==(4 if i==0 else 1)and controller['Attempts']>=3 and controller['Hits']>=1
   if i==0:assert controller['Target']==s['Target'] and controller['ActionAt']==controller['Frame']+1
   else:assert controller['ActionAt']==controller['EndAt']==0
  assert items==330;itemcounts[stage.name]=items
  initialized=e.load(stage/'valkyrie-initializer-fixtures.json');assert initialized['WorldAtSave'] is not None
  if (stage/'npc-world-allocation-fixtures.json').exists():assert e.load(stage/'npc-world-allocation-fixtures.json')['WorldAtSave']==initialized['WorldAtSave']
 for n in ['content-build-1.log','integration-build-1.log','integration-build-2.log','integration-build-3.log']:assert all(x in(STAGE/n).read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 from review_valkyrie_ground_contact import review
 review(True)
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]
 for n in [1,2]:files+=list((ISO/f'failed-native-{n}').iterdir())
 files += [Path(__file__).resolve(),ROOT/'tests/check_valkyrie_ground_combat.py',ROOT/'tools/review_valkyrie_ground_contact.py',ROOT/'tools/reference/valkyrie-ground-contact-source-review.json',STAGE/'content/Sanctuary.Content.dll',STAGE/'integration/Sanctuary.Integration.dll']
 files += [ROOT/'docs'/n for n in ['valkyrie-ground-combat-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,outerModeDispatcherExecuted=False,preparedAutomaticGroundCombat=True,sourceSnapshot=3,contentBuild=1,integrationBuild=3,focusedRun=1,retainedRun=1,isolatedRun=3,sharedRun=1,auditRun=1,focusedMarkers=111,retainedMarkers=419,isolatedMarkers=18,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,preSaveSealsPerCreationCohort=3,sourceReviewCompleteBodies=8,priorAnimationOracleContexts=1368,originalLoadoutsPerCombatCohort=57,originalItemsByCohort=itemcounts,pythonTests=71,verificationCommands=8,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=['Explicit original-equipment melee capability reuses the shared AI67/movement/frame conductor. Optional saved attack rate grants combat alongside original contact only;legacy motion-only snapshots remain unchanged and refuse offense.', 'Mode entry schedules the existing transformed A1 event plan. Ordinary original-item contact allocates/releases at the action marker with fresh target/range checks;windup consumes only actual AI decisions. Hit/miss,pre-action silence,departed targets,freeze interruption and end-to-neutral are driven naturally across all57unfiltered loadouts.', 'Original330items,resource/event ledgers,actual wounds/RNG and a genuine pre-action world windup cross three saves/two reloads. Existing minion restoration parks bodies and cancels obsolete actions without replay. Later proc draws use the persisted world service and the final pre-save seal covers the last checked family.'],limits=['Attack/walk/run base inputs remain explicitly supplied;100/75/125fixture values do not establish modern startup/run-state ownership. Eight complete older bodies are statically reviewed,not an executed outer dispatcher or current executable certificate. The prior independent animation oracle remains separate.', 'Learned payment/placement/replacement,town admission,nonzero inherited sunder and unsupported external elemental/monster-type adapters remain gated. Final rendered effects,body/equipment/audio and icons remain separate placeholders. No deployment or completed class.', 'I1failed a fixture expectation that saved active windup survive normal restore parking. I2passed that check but attempted new placement beside an offline player restored internally. I3compares the exact canonical parked controller,returns only the test owner to its saved world and verifies cancellation/no HP/RNG replay. All failed logs/manifests/inputs/source/I1-I2DLLs are archived;productionContent1unchanged.'])
 e.write('valkyrie-ground-combat-native-evidence.json',result,files,check)
 print('Valkyrie automatic ground contact:57original loadouts;111focused/419retained/18isolated/255shared;71Python/eightchecks;202handlers,learned cast still gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
