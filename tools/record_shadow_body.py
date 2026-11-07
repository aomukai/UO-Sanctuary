#!/usr/bin/env python3
"""Bind actual parked Shadow resource bodies and unchanged learned regressions."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-body';WIDE=BASE/'shadow-body-retained';ISO=BASE/'shadow-body-isolate';SHARED=BASE/'shadow-body-shared'
PARENT='73f41bc7cee507d6f60facf10ca1572d75775538'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json');sys.path.insert(0,str(ROOT/'tests'))
 from check_shadow_body import configuration
 parent=e.load(ROOT/'tools/reference/shadow-initializer-native-evidence.json');extra=Counter({'shadow-body-check':1,'shadow-body-reload-check':2})
 for previous,stage,count in [(BASE/'shadow-initializer',STAGE,144),(BASE/'shadow-initializer-retained',WIDE,452)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))]
  e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected(configuration(isolate=True)),51);e.cohort(SHARED,expected(configuration(shared=True)),255)
 itemcounts=[]
 for stage,driver in [(STAGE,'focused-driver-1.log'),(WIDE,'retained-driver-1.log'),(ISO,'isolate-driver-1.log'),(SHARED,'shared-driver-1.log')]:
  assert 'pass across3boots/2reloads'in (STAGE/driver).read_text()
  snapshot(STAGE/'source-snapshot-2.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json')
  assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert (stage/'birth-native-checks.log').read_text().count('Shadow bodies:24actual initialized species/owners and unfiltered original inventories;')==3
  saved=e.load(stage/'shadow-body-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==24
  captures=[];count=0
  for h in saved['Bodies']:
   state=json.loads(h['State']);resources=json.loads(h['Resources']);capture=state['Capture'];captures.append(capture)
   assert state['Profile']=='parked-shadow-resources-v1'and state['Owner']>0 and state['Equipment']['Actor']==h['Actor']
   assert resources['Shadow']['Capture']==capture and resources['Shadow']['Initialization']==state['Initialization']
   assert resources.get('Valkyrie')is None and resources.get('ItemEvents')is None
   assert h['Random']['ShadowBinding']==state['Weapons']['Identity']and h['Random'].get('ValkyrieBinding')is None
   seed=state['Equipment']['PetAfter'];assert h['Random']['Seed']==0x6ac690c5*(seed&0xffffffff)+(seed>>32)
   assert len(h['Items'])==len(h['Payloads'])>=2 and len(set(h['Items']))==len(h['Items'])
   assert 0<h['Life']<h['Maximum']and h['Mana']==h['Stamina']==0
   count+=len(h['Items'])
  assert Counter(c['Rank']for c in captures)==Counter({1:6,2:6,10:6,20:6})
  assert sum(c['Head']is not None and c['Armor']is not None for c in captures)==18
  itemcounts.append(count)
 for n in ['content-build-2.log','integration-build-1.log']:assert all(s in (STAGE/n).read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==12 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in (STAGE/'python-tests.log').read_text()and '\nOK\n'in (STAGE/'python-tests.log').read_text()
 proof=ROOT/'tools/reference/shadow-body-source-evidence.json';source=e.load(proof)
 assert source['sourceResourceContexts']==510 and source['realShadowBodies']and source['parkedOnly']and not source['handlerEnabled']and not source['currentSetterAndCallbackParityVerified']
 for p,h in source['inputs'].items():assert e.sha(ROOT/p)==h,p
 edgesPath=ROOT/'tools/reference/shadow-stat-setter-edges-source-evidence.json';edges=e.load(edgesPath)
 assert edges['allMissingFragmentsUnreadable']and edges['missingRecalculationBytes']==852 and not edges['protectedReadAttempted']and not edges['currentSetterAndCallbackParityVerified']
 for p,h in edges['inputs'].items():assert e.sha(ROOT/p)==h,p
 frozen=STAGE/'frozen-c2-i1'
 for name,h in e.load(frozen/'hashes.json').items():assert e.sha(frozen/name)==h,name
 for kind in ['Content','Integration']:assert e.sha(frozen/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==203 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]
 files+=list(frozen.iterdir())+[Path(__file__).resolve(),ROOT/'tools/review_shadow_body.py',ROOT/'tests/check_shadow_body.py',proof,edgesPath,ROOT/'tools/record_shadow_stat_setter_edges.py']
 files+=[ROOT/'tools/reference/shadow-resource-birth-source-evidence.json',ROOT/'tools/probe_shadow_resource_birth.py']
 files+=[ROOT/'docs'/n for n in ['shadow-body-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,realShadowBodies=True,parkedOnly=True,learnedShadows=False,currentSetterAndCallbackParityVerified=False,handlers=203,completedClasses=[],contentBuild=2,integrationBuild=1,sourceSnapshot=2,focusedMarkers=144,retainedMarkers=452,isolatedMarkers=51,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceResourceContexts=510,actualBodiesPerCreationCohort=24,originalItemsPerCreationCohort=itemcounts,pythonTests=71,verificationCommands=12,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=source['scope'],limits=source['limits'])
 e.write('shadow-body-native-evidence.json',result,files,check)
 print('Real parked Shadow bodies/resources:24original bodies/510source contexts;144focused/452retained/51isolated/255shared;203handlers,no learned Shadow/deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
