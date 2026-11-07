#!/usr/bin/env python3
"""Bind actual saved Shadow intrinsic skills and retained class regressions."""
import argparse,json,sys
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-skills';WIDE=BASE/'shadow-skills-retained';ISO=BASE/'shadow-skills-isolate';SHARED=BASE/'shadow-skills-shared'
PARENT='b9f51a3fb8a5eb1a1f26ecf5898087cba93dc674'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json');sys.path.insert(0,str(ROOT/'tests'))
 from check_shadow_skills import configuration
 parent=e.load(ROOT/'tools/reference/shadow-body-native-evidence.json')
 for previous,stage,count in [(BASE/'shadow-body',STAGE,144),(BASE/'shadow-body-retained',WIDE,452)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p),count)
 e.cohort(ISO,expected(configuration(isolate=True)),51);e.cohort(SHARED,expected(configuration(shared=True)),255)
 itemcounts=[]
 for stage,driver in [(STAGE,'focused-driver-1.log'),(WIDE,'retained-driver-1.log'),(ISO,'isolate-driver-2.log'),(SHARED,'shared-driver-1.log')]:
  assert 'pass across3boots/2reloads'in(STAGE/driver).read_text()
  snapshot(STAGE/'source-snapshot-2.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json')
  assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  saved=e.load(stage/'shadow-body-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==24
  books=[];count=0
  for held in saved['Bodies']:
   state=json.loads(held['State']);book=json.loads(state['Skills']);ranks=json.loads(state['Capture']['Skills']);books.append(book)
   assert [s['Id']for s in ranks]==list(range(251,281))and next(s for s in ranks if s['Id']==251)['Hard']==3
   assert book['Profile']=='shadow-intrinsic-skills-v1'and(book['Left'],book['Right'],book['Used'])==(251,266,251)
   entries={s['Id']:s for s in book['Entries']};assert entries[251]['Bonus']==2 and entries[266]['Mode']==14 and entries[0]['Mode']==5
   assert json.loads(held['Resources'])['Shadow']['Capture']==state['Capture']and held['Random']['ShadowBinding']==state['Weapons']['Identity']
   assert 0<held['Life']<held['Maximum']and len(held['Items'])==len(held['Payloads'])>=2
   count+=len(held['Items'])
  assert sorted(len(b['Entries'])for b in books)==[8]*12+[29]*12;itemcounts.append(count)
 for n in ['content-build-1.log','integration-build-2.log']:
  assert all(s in(STAGE/n).read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 # First compilation was successful but had an unrun incorrect passive-layer
 # test expectation. The corrected I2 is the only integration DLL tested.
 assert 'Build succeeded.'in(STAGE/'integration-build-1.log').read_text()
 assert 'FileNotFoundError'in(STAGE/'isolate-driver-1.log').read_text()and '.local/server'in(STAGE/'isolate-driver-1.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==13 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 proof=ROOT/'tools/reference/shadow-skills-source-evidence.json';source=e.load(proof)
 assert source['compiledMetadataMatches']==3003 and source['masterWholeInitializerContexts']==255 and source['warriorAssignmentSliceContexts']==1530
 assert source['parkedOnly']and not any(source[k]for k in ['handlerEnabled','passiveStatRefreshImplemented','itemGrantCallbacksImplemented','npcSkillExecutionEnabled','currentNpcSkillAssignmentParityVerified'])
 for p,h in source['inputs'].items():assert e.sha(ROOT/p)==h,p
 frozen=STAGE/'frozen-c1-i2'
 for name,h in e.load(frozen/'hashes.json').items():assert e.sha(frozen/name)==h,name
 for kind in ['Content','Integration']:assert e.sha(frozen/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==203 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]+list(frozen.iterdir())
 files+=[Path(__file__).resolve(),ROOT/'tools/review_shadow_skills.py',ROOT/'tools/probe_shadow_skill_assignment.py',ROOT/'tools/build_shadow_skill_metadata.py',ROOT/'tests/check_shadow_skills.py',proof,ROOT/'tools/reference/shadow-skill-assignment-source-evidence.json',ROOT/'tools/reference/shadow-skill-metadata-data-evidence.json']
 files+=[ROOT/'docs'/n for n in ['shadow-skills-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,realShadowBodies=True,parkedOnly=True,learnedShadows=False,currentNpcSkillAssignmentParityVerified=False,
  handlers=203,completedClasses=[],contentBuild=1,integrationBuild=2,sourceSnapshot=2,focusedMarkers=144,retainedMarkers=452,isolatedMarkers=51,sharedMarkers=255,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,compiledMetadataMatches=3003,masterWholeInitializerContexts=255,warriorAssignmentSliceContexts=1530,
  masterNativeDifficultyComparisonsPerBoot=765,warriorNativeAssignmentComparisonsPerBoot=1530,actualBodiesPerCreationCohort=24,originalItemsPerCreationCohort=itemcounts,
  pythonTests=71,verificationCommands=13,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=source['scope'],limits=source['limits'])
 e.write('shadow-skills-native-evidence.json',result,files,check);print('Saved Shadow intrinsic books:255whole older Master/1530Warrior source slices;144focused/452retained/51isolated/255shared;203handlers,no learned Shadow/deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
