#!/usr/bin/env python3
"""Bind actual layered Shadow passive caches and unchanged class regressions."""
import argparse,json,sys
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-passives';WIDE=BASE/'shadow-passives-retained';ISO=BASE/'shadow-passives-isolate';SHARED=BASE/'shadow-passives-shared'
PARENT='2f5759baf9e6364d2f4993e605d48df3bf44f3ed'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-4.json');sys.path.insert(0,str(ROOT/'tests'))
 from check_shadow_passives import configuration
 parent=e.load(ROOT/'tools/reference/shadow-skills-native-evidence.json')
 for prev,stage,count in [(BASE/'shadow-skills',STAGE,144),(BASE/'shadow-skills-retained',WIDE,452)]:
  p=prev/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p),count)
 e.cohort(ISO,expected(configuration(isolate=True)),51);e.cohort(SHARED,expected(configuration(shared=True)),255)
 itemcounts=[];passivecounts=[]
 for stage,driver in [(STAGE,'focused-driver-2.log'),(WIDE,'retained-driver-2.log'),(ISO,'isolate-driver-3.log'),(SHARED,'shared-driver-2.log')]:
  assert 'pass across3boots/2reloads'in(STAGE/driver).read_text();snapshot(STAGE/'source-snapshot-4.json',stage/'birth-inputs.json')
  other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  saved=e.load(stage/'shadow-body-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==24
  books=[];items=lists=0
  for held in saved['Bodies']:
   state=json.loads(held['State']);resources=json.loads(held['Resources']);frame=resources['Passives'];book=json.loads(state['Skills']);books.append(book)
   assert frame['Profile']=='shadow-passive-lists-v1'and frame['Book']==state['Skills']and resources['Shadow']['Capture']==state['Capture']
   assert (book['Left'],book['Right'],book['Used'])==(251,266,251)
   assert [a['Item']for a in frame['Order']if a['Item']] == resources['ActiveItems']
   assert sorted(a['State']for a in frame['Order']if a['State'])==sorted(l['State']for l in frame['Lists'])
   assert len(frame['Lists'])==(2 if len(book['Entries'])==29 else 0)
   for l in frame['Lists']:
    assert(l['Skill'],l['State'])in[(252,152),(263,155)]and 1<=l['Level']<=99
    fields={v['Packed']:v['Value']for v in l['Values']};assert fields[350<<16]==l['Skill']and fields[351<<16]==l['Level']
    assert sorted(p>>16 for p in fields)==([342,343,344,350,351]if l['Skill']==252 else[348,350,351])
    assert all((p&65535)==(0 if p>>16 in[350,351]else 68)for p in fields)
   assert 0<held['Life']<held['Maximum']and len(held['Items'])==len(held['Payloads'])>=2
   items+=len(held['Items']);lists+=len(frame['Lists'])
  assert sorted(len(b['Entries'])for b in books)==[8]*12+[29]*12 and lists==24
  itemcounts.append(items);passivecounts.append(lists)
 for n in ['content-build-2.log','integration-build-4.log']:
  assert all(s in(STAGE/n).read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==13 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 proof=ROOT/'tools/reference/shadow-passives-review-evidence.json';source=e.load(proof)
 assert source['sourceContexts']==1600 and source['passiveStatRefreshImplemented']and source['allAndElementCallbacksImplemented']and source['commandRefreshImplemented']and source['approvedWeaponBlockRank20Percent']==58
 assert source['parkedOnly']and not any(source[k]for k in ['handlerEnabled','currentExecutablePassiveParityVerified','oskillMaterializationImplemented','foreignSkillsImplemented','shadowDefenseOrMeleeEnabled'])
 for p,h in source['inputs'].items():assert e.sha(ROOT/p)==h,p
 frozen=STAGE/'frozen-c2-i4';archived=STAGE/'archived-c1-i1'
 for folder in [frozen,archived]:
  for name,h in e.load(folder/'hashes.json').items():assert e.sha(folder/name)==h,name
 for kind in ['Content','Integration']:assert e.sha(frozen/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 assert sum(markers(archived/'birth-native-checks.log').values())==51 and 'pass across3boots/2reloads'in(STAGE/'isolate-driver-1.log').read_text()
 assert 'cannot bind non-const lvalue reference'in(BASE/'shadow-passives-source/compiler-failed-1.log').read_text()
 failures=STAGE/'failed-c2-i3'
 for n in ['shadow-passives','shadow-passives-retained']:
  log=failures/(n+'-birth-native-checks.log');assert ': failed'in log.read_text()and 'Passive transitions changed original items,wounds or RNG'in log.read_text()
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==203 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]+list(frozen.iterdir())+list(archived.iterdir())+list((STAGE/'failed-c2-i3').iterdir())+list((STAGE/'frozen-c2-i3').iterdir())
 files+=[Path(__file__).resolve(),ROOT/'tools/review_shadow_passives.py',ROOT/'tools/probe_shadow_passives.py',ROOT/'tests/check_shadow_passives.py',proof,ROOT/'tools/reference/shadow-passives-source-evidence.json',ROOT/'tools/reference/weapon-block-rank20-deviation.json']
 files+=[ROOT/'docs'/n for n in ['shadow-passives-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,realShadowBodies=True,passiveListsImplemented=True,parkedOnly=True,learnedShadows=False,currentExecutablePassiveParityVerified=False,
  handlers=203,completedClasses=[],contentBuild=2,integrationBuild=4,sourceSnapshot=4,focusedMarkers=144,retainedMarkers=452,isolatedMarkers=51,sharedMarkers=255,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceContexts=1600,currentPassiveStatMatches=28,currentPassiveCalcPresenceMatches=28,currentParameterMatches=12,
  actualBodiesPerCreationCohort=24,originalItemsPerCreationCohort=itemcounts,savedPassiveListsPerCreationCohort=passivecounts,approvedWeaponBlockRank20Percent=58,
  pythonTests=71,verificationCommands=13,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=source['scope'],limits=source['limits'])
 e.write('shadow-passives-native-evidence.json',result,files,check);print('Actual saved Shadow passive lists:1600whole source controls,144focused/452retained/51isolated/255shared;WeaponBlock20=58approved;no learned Shadow/deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
