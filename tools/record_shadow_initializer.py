#!/usr/bin/env python3
"""Bind scoped Shadow initialization,original numeric loadouts and regressions."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-initializer';WIDE=BASE/'shadow-initializer-retained';ISO=BASE/'shadow-initializer-isolate';SHARED=BASE/'shadow-initializer-shared'
PARENT='2edd0037de37399f05521fef65cd7c22f4e2a7ce'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-4.json');sys.path.insert(0,str(ROOT/'tests'))
 from check_shadow_initializer import configuration
 parent=e.load(ROOT/'tools/reference/shadow-birth-native-evidence.json');extra=Counter({'shadow-initializer-check':1,'shadow-initializer-reload-check':2})
 for previous,stage,count in [(BASE/'shadow-birth',STAGE,141),(BASE/'shadow-birth-retained',WIDE,449)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))]
  e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected(configuration(isolate=True)),48);e.cohort(SHARED,expected(configuration(shared=True)),255)
 itemcounts=[]
 for stage,driver in [(STAGE,'focused-driver-2.log'),(WIDE,'retained-driver-2.log'),(ISO,'isolate-driver-3.log'),(SHARED,'shared-driver-2.log')]:
  assert 'pass across3boots/2reloads'in (STAGE/driver).read_text()
  snapshot(STAGE/'source-snapshot-4.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json')
  assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert (stage/'birth-native-checks.log').read_text().count('Shadow initializer:1344whole older-source contexts with current tables;24actual unit allocations and unfiltered original-item storage fixtures;')==3
  saved=e.load(stage/'shadow-initializer-fixtures.json');assert saved['Generation']==2 and saved['Owner']>0 and len(saved['Holders'])==24
  assert Counter(h['Rank']for h in saved['Holders'])==Counter({1:6,10:6,20:6,40:6})
  for h in saved['Holders']:
   init=h['Initialization'];assert init['Profile']=='source-shadow-initialization-v1' and init['SpawnLife256']==96256 and init['RegenerationBase']in [70,47]
   assert len(h['Items'])==len(h['Payloads'])>=2 and len(set(h['Items']))==len(h['Items'])
   assert h['Writes']['Modifier']==42 and h['Writes']['MaximumLifeWrite256']==h['Writes']['CurrentLifeWrite256']
   assert h['NativeLife']==h['NativeMaximum']-3 and h['WorldAfter']!=init['WorldAfter']
  itemcounts.append(sum(len(h['Items'])for h in saved['Holders']))
 for n in ['content-build-4.log','integration-build-4.log']:assert all(s in (STAGE/n).read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==11 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in (STAGE/'python-tests.log').read_text()and '\nOK\n'in (STAGE/'python-tests.log').read_text()
 proof=ROOT/'tools/reference/shadow-initializer-implementation-evidence.json';source=e.load(proof)
 assert source['sourceInitializerContexts']==1344 and source['compiledSkillSlots']==6008 and source['preparedOnly']and not source['handlerEnabled']and not source['currentInitializerExecutableParity']
 for p,h in source['inputs'].items():assert e.sha(ROOT/p)==h,p
 modifierPath=ROOT/'tools/reference/shadow-modifier-source-evidence.json';modifier=e.load(modifierPath)
 assert modifier['sourceContexts']==1920 and modifier['completeCapturedFunctionBytes']==256 and modifier['completeFragments']==5
 assert modifier['initializerPointer']==0 and modifier['currentModifierRegistrationVerified']and not modifier['deathBehaviorVerified']and not modifier['handlerEnabled']
 for p,h in modifier['artifacts'].items():assert e.sha(ROOT/p)==h,p
 frozen=STAGE/'frozen-c4-i4'
 for name,h in e.load(frozen/'hashes.json').items():assert e.sha(frozen/name)==h,name
 for kind in ['Content','Integration']:assert e.sha(frozen/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==203 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]
 files+=[modifierPath,ROOT/'tools/probe_shadow_modifier.py']
 files+=list(frozen.iterdir())+[Path(__file__).resolve(),ROOT/'tools/review_shadow_initializer.py',ROOT/'tests/check_shadow_initializer.py',proof]
 files+=[ROOT/'tools/reference'/n for n in ['shadow-initializer-data-evidence.json','shadow-initializer-source-evidence.json']]
 files+=[ROOT/'docs'/n for n in ['shadow-initializer-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,preparedOnly=True,learnedShadows=False,currentInitializerExecutableParity=False,handlers=203,completedClasses=[],contentBuild=4,integrationBuild=4,sourceSnapshot=4,focusedMarkers=141,retainedMarkers=449,isolatedMarkers=48,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceInitializerContexts=1344,currentBirthHelperContexts=3456,modifierRegistrationSourceContexts=1920,compiledSkillSlots=6008,originalItemStorageFixturesPerCohort=24,originalItemsPerCreationCohort=itemcounts,pythonTests=71,verificationCommands=11,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=source['scope'],limits=source['limits'])
 e.write('shadow-initializer-native-evidence.json',result,files,check)
 print('Prepared Shadow initialization:1344source contexts/24original numeric loadouts;141focused/449retained/48isolated/255shared;203handlers,no live Shadow/deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
