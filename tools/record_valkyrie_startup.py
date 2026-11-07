#!/usr/bin/env python3
"""Bind derived startup and expanded original-item spell routes to native saves."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-startup';WIDE=BASE/'valkyrie-startup-retained';ISO=BASE/'valkyrie-startup-isolate';SHARED=BASE/'valkyrie-startup-shared'
PARENT='fa47afd820a4324a7bcde279dcbf9c70eb1a7a43'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json')
 sys.path.insert(0,str(ROOT/'tests'));from check_valkyrie_startup import configuration
 parent=e.load(ROOT/'tools/reference/valkyrie-ground-combat-native-evidence.json');extra=Counter({'valkyrie-startup-check':1,'valkyrie-startup-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-ground-combat',STAGE,114),(BASE/'valkyrie-ground-combat-retained',WIDE,422)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected(configuration(isolate=True)),21);e.cohort(SHARED,expected(configuration(shared=True)),255)
 phrase='Valkyrie startup:24complete source bodies/5016transitions/4104native rate-event comparisons;derived100/75/125from original initialization/current Amazon run state;12actual initialized rank/difficulty creations,unfiltered gear,walk-run/contact/provenance guards;original resources/items/wounds/RNG through two reloads;town/sunder/remaining contact adapters and learned cast remain separate'
 itemcounts={}
 for stage,run in [(STAGE,1),(WIDE,1),(ISO,3),(SHARED,1)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  s=e.load(stage/'valkyrie-startup-fixtures.json');assert s['Generation']==2 and len(s['Bodies'])==12
  ranks=Counter();diffs=Counter();items=0
  for h in s['Bodies']:
   b=json.loads(h['Text']);motion=b['GroundMotion'];controller=motion['World']['Controller'];resources=json.loads(h['Resources']);initial=b['Initialization']
   assert b['Owner']==h['Owner']==resources['Owner']and resources['Actor']==h['Body']==h['Random']['Actor']
   assert motion['StartupProfile']=='source-valkyrie-mode-entry-v1' and motion['AttackRate']==100 and motion['WalkVelocityPercent']==75 and motion['RunVelocityPercent']==125
   assert b['EquipmentContactProfile']=='valkyrie-original-proc-contact-v1' and resources['ItemEvents']is not None
   assert initial['SpawnLife256']==b['Birth']['SpawnLife256']and b['Equipment']['PetBefore']==initial['AfterInitialization']and b['Equipment']['GameBefore']==initial['WorldAfter']
   assert h['Life']>=256 and resources['Valkyrie']['LifeMaximum256']>=256
   assert len(h['Items'])==sum(a['Success']for a in b['Equipment']['Attempts']);items+=len(h['Items'])
   assert controller['Mode']==1 and controller['ActionAt']==controller['EndAt']==0 and controller['Attempts']>=3 and controller['Hits']>=1
   ranks[b['Birth']['Rank']]+=1;diffs[b['Difficulty']]+=1
  assert ranks==Counter({1:3,5:3,10:3,20:3})and diffs==Counter({0:4,1:4,2:4})and items==57;itemcounts[stage.name]=items
 for n in ['content-build-1.log','content-build-2.log','content-build-3.log','integration-build-2.log','integration-build-3.log','integration-build-4.log']:assert all(x in(STAGE/n).read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==10 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 sources=[]
 for name in ['valkyrie-startup','npc-expanded-spells','npc-expanded-origins']:
  p=ROOT/'tools/reference'/f'{name}-source-evidence.json';d=e.load(p);sources.append(p)
  for group in ['inputs','artifacts','currentTable']:
   for path,h in d.get(group,{}).items():assert e.sha(ROOT/path)==h,path
  for path,h in d.get('sourceHashes',{}).items():assert e.sha(BASE/'d2moo-pinned-source'/path)==h,path
 startup,spells,origins=map(e.load,sources)
 assert startup['transitions']==5016 and len(startup['completeRoutineHashes'])==24 and startup['rates']==dict(attack=100,walk=75,run=125,temporaryRunBonus=50)
 assert spells['cases']==3566 and spells['numericMatrix']==3528 and len(spells['routineHashes'])==16 and origins['cases']==21168 and origins['completeRoutines']==7
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]
 for n in [1,2]:files+=list((ISO/f'failed-native-{n}').iterdir())
 files+=list((STAGE/'failed-build-1').iterdir())
 for n in ['valkyrie-startup-source','npc-expanded-spells-source','npc-expanded-origins-source']:
  folder=BASE/n;files+=e.stage_files(folder)
  if n=='valkyrie-startup-source':
   for failed in ['failed-compile-1','failed-run-2','failed-instrumentation-4','failed-metadata-5']:files+=list((folder/failed).iterdir())
 files += [Path(__file__).resolve(),ROOT/'tests/check_valkyrie_startup.py',ROOT/'tools/probe_valkyrie_startup.py',ROOT/'tools/probe_npc_expanded_spells.py',ROOT/'tools/probe_npc_expanded_origins.py',ROOT/'global.json',*sources,STAGE/'content/Sanctuary.Content.dll',STAGE/'integration/Sanctuary.Integration.dll']
 files += [ROOT/'docs'/n for n in ['valkyrie-startup-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,outerMonsterModeDispatcherExecuted=False,completeRateDispatcherExecuted=True,derivedInitializedStartup=True,derivedRates=dict(attack=100,walk=75,run=125),sourceSnapshot=3,contentBuild=3,integrationBuild=4,focusedRun=1,retainedRun=1,isolatedRun=3,sharedRun=1,auditRun=1,focusedMarkers=114,retainedMarkers=422,isolatedMarkers=21,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,preSaveSealsPerCreationCohort=3,completeStartupBodies=24,startupSourceTransitions=5016,nativeRateEventComparisons=4104,expandedSpellSourceContexts=3566,expandedOriginSourceContexts=21168,addedProcSkills=[39,49,66],initializedControllersPerCohort=12,originalItemsByCohort=itemcounts,pythonTests=71,verificationCommands=10,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=['Complete older combat/start/mode/rate helpers establish converted Amazon temporary+50run contribution to initial100/75,retained through game reset and removed on a mode change. Saved derived startup requires original initializer witness;legacy supplied motion/contact remains unchanged.', 'Source-initialized native creation composes actual world unit/own HP/unfiltered gear/original contact and derived default controller. Twelve rank/difficulty creations exercise actual movement/ordinary contacts,atomic provenance guards,and57original items/resources/exact257FP wounds/RNG through two reloads.', 'A real unfiltered Ice Bolt roll exposed the incomplete proc whitelist. New39Ice Bolt/49Lightning reuse existing protected direct-emission/travel/typed-HP and cold paths;66Amplify reuses existing protected do30. Separate complete numeric/origin source probes and natural native effects verify own mastery,cold,continuing line,outgoing origins,real world allocation,no owner mana and source controller preservation.'],limits=['Older complete bodies with pinned current data supplement the mechanical reference;current executable and full outer monster dispatcher parity remain unverified. Sequences,missing dynamic paths and unrelated animation modes are outside this source trace.', 'Town/safe-area contact admission,nonzero inherited sunder,external elemental stat lists,monster-type data,learned payment/placement/replacement/input and client acceptance remain unfinished. Hydra62amulet/other spell/ground-curse/targetless-path services remain explicit refusals. No deployment or complete class;assets stay separate placeholders.', 'First Integration failed a candidate output self-link,corrected without source change. Isolate1failed real unsupported unfiltered proc39;C2added39/49/66routes. Isolate2then exposed the world host direct-field selector andC3extends its shared36/39/49selector. Both failed sources/inputs/binaries/logs and source-probe diagnostic versions remain archived. FinalC3/I4/S3checks bind the same tested binaries;all final cohorts,71Python and10verification commands pass.'])
 e.write('valkyrie-startup-native-evidence.json',result,files,check)
 print('Valkyrie derived startup:12actual initialized controllers/3difficulties,39/49/66item routes;114focused/422retained/21isolated/255shared,71Python/10checks;202handlers/learned still gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
