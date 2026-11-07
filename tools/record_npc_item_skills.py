#!/usr/bin/env python3
"""Accept source NPC item-skill ownership with explicitly scoped producers."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-skills';WIDE=BASE/'npc-item-skills-retained';ISO=BASE/'npc-item-skills-isolate';SHARED=BASE/'npc-item-skills-shared'
PARENT='b1a2a72eaebb0ef32345df53f3fe99889bcbd44f'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json');sys.path.insert(0,str(ROOT/'tests'))
 from check_npc_item_skills import configuration
 parent=e.load(ROOT/'tools/reference/shadow-passives-native-evidence.json');extra=Counter({'npc-item-skills-check':1,'npc-item-skills-reload-check':2})
 for prev,stage,count in [(BASE/'shadow-passives',STAGE,147),(BASE/'shadow-passives-retained',WIDE,455)]:
  p=prev/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected(configuration(isolate=True)),54);e.cohort(SHARED,expected(configuration(shared=True)),255)
 itemcounts=[]
 for stage,driver in [(STAGE,'focused-driver-1.log'),(WIDE,'retained-driver-1.log'),(ISO,'isolate-driver-2.log'),(SHARED,'shared-driver-1.log')]:
  assert 'pass across3boots/2reloads'in(STAGE/driver).read_text();snapshot(STAGE/'source-snapshot-2.json',stage/'birth-inputs.json')
  other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  saved=e.load(stage/'npc-item-skills-fixtures.json');assert saved['Generation']==2 and saved['Actor']!=saved['Owner']
  frame=saved['Frame'];book=json.loads(frame['Book']);assert frame['ItemSkillCallbacks']and len(book['Entries'])==2
  assert(book['Left'],book['Right'],book['Used'])==(36,36,36)and next(r for r in book['Entries']if r['Id']==36)['Base']==0
  assert frame['Order']==[dict(Item=saved['Item'],State=0)]and not frame['Lists']
  payload=json.loads(saved['Payload']);assert payload['Metadata']['Code']=='gsc'and payload['Metadata']['Quality']==2
  # The oskill source is explicitly supplied,not manufactured in the item.
  numeric={r['Packed']:r['Value']for r in saved['Numeric']};assert numeric[(97<<16)|36]==5
  bodies=e.load(stage/'shadow-body-fixtures.json');assert bodies['Generation']==2 and len(bodies['Bodies'])==24
  count=0
  for h in bodies['Bodies']:
   state=json.loads(h['State']);resources=json.loads(h['Resources']);f=resources['Passives'];assert f['ItemSkillCallbacks']and f['Book']==state['Skills']
   assert sum(o.get('Birth',False)for o in f['Order'])==1
   assert [o['Item']for o in f['Order']if o['Item']]==resources['ActiveItems']
   assert 0<h['Life']<h['Maximum'];count+=len(h['Items'])
  itemcounts.append(count)
 for n in ['content-build-1.log','integration-build-2.log']:
  assert all(s in(STAGE/n).read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==13 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 proof=ROOT/'tools/reference/npc-item-skills-review-evidence.json';source=e.load(proof)
 assert source['sourceContexts']==4096 and source['npcMetadataRows']==429 and source['oskillMaterializationImplemented']and source['foreignNonpassiveEntriesSupported']
 assert not any(source[k]for k in ['originalUniqueOskillProducerVerified','foreignPassiveEffectsSupported','activeAuraOrChannelFallbackImplemented','chargedSkillEntriesImplemented','currentExecutableParityVerified','handlerEnabled'])
 for p,h in source['inputs'].items():assert e.sha(ROOT/p)==h,p
 frozen=STAGE/'frozen-c1-i2'
 for n,h in e.load(frozen/'hashes.json').items():assert e.sha(frozen/n)==h,n
 for kind in ['Content','Integration']:assert e.sha(frozen/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 failed=STAGE/'failed-unique-producer-c1-i1';assert 'supported requested quality'in(failed/'birth-native-checks.log').read_text()
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==203 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]+list(frozen.iterdir())+list(failed.iterdir())
 files+=[Path(__file__).resolve(),ROOT/'tools/review_npc_item_skills.py',ROOT/'tools/probe_npc_item_skill_grants.py',ROOT/'tests/check_npc_item_skills.py',proof,ROOT/'tools/reference/npc-item-skill-grants-source-evidence.json',ROOT/'tools/reference/shadow-pre-equipment-skills-order.json',ROOT/'tools/reference/shadow-skill-metadata-data-evidence.json']
 files+=[ROOT/'docs'/n for n in ['npc-item-skills-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,oskillOwnershipImplemented=True,originalUniqueOskillProducerVerified=False,explicitNativeSource97Input=True,
  currentExecutableParityVerified=False,parkedOnly=True,learnedShadows=False,handlers=203,completedClasses=[],contentBuild=1,integrationBuild=2,sourceSnapshot=2,
  focusedMarkers=147,retainedMarkers=455,isolatedMarkers=54,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
  sourceContexts=4096,npcMetadataRows=429,compiledFieldMatches=3003,actualShadowBodiesPerCreationCohort=24,originalItemsPerCreationCohort=itemcounts,
  pythonTests=71,verificationCommands=13,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=source['scope'],limits=source['limits'])
 e.write('npc-item-skills-native-evidence.json',result,files,check);print('NPC grant ownership:4096source contexts,147focused/455retained/54isolated/255shared;unique producer remains unsupported,no learned Shadow/deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
