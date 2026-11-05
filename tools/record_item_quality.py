#!/usr/bin/env python3
"""Bind complete quality orchestration and original controlled graph acceptance."""
import argparse,json
from pathlib import Path
from record_item_initialization import expected as previous_expected
import skill_checkpoint_evidence as evidence
ROOT=evidence.ROOT;STAGE=ROOT/'.local/class-skills/item-quality';WIDE=ROOT/'.local/class-skills/item-quality-broader';COMPACT=ROOT/'.local/class-skills/item-quality-compact'
PARENT='4eb78375c976714a9f63775bf605e4d27ca73389'
CHANGED={'src/Sanctuary.Content/ItemPropertyCreationContext.cs','src/Sanctuary.Content/SourceItemQuality.cs','tests/Sanctuary.Integration/ItemQualityChecks.cs'}
def expected(broader=False,compact=False):
 r=previous_expected(broader,compact);r['item-quality-check']+=1;r['item-quality-reload-check']+=2;return r

def build(check=False):
 born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-1.json')
 old=evidence.load(ROOT/'.local/class-skills/item-initialization/pre-build-4.json')['inputs'];now=evidence.load(STAGE/'pre-build-1.json')['inputs']
 changed={p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)};assert changed==CHANGED,changed
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=evidence.load(ROOT/'tools/reference/item-initialization-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 cases=evidence.load(ROOT/'.local/class-skills/item-quality-source/cases.json');assert len(cases['inputs'])==22656
 for stage,wide,compact,count in ((STAGE,False,False,395),(WIDE,True,False,611),(COMPACT,False,True,24)):
  log=evidence.cohort(stage,expected(wide,compact),count);assert log.count('Item quality:22656complete source controls/6entries/11routines;18original controlled native graphs')==3
  saved=evidence.load(stage/'item-quality-fixtures.json');assert saved['Generation']==2 and len(saved['Items'])==18 and len({r['Item']for r in saved['Items']})==18
  assert {cases['inputs'][r['Case']][0]for r in saved['Items']}==set(range(6))
  for r in saved['Items']:
   row=cases['outputs'][r['Case']];assert json.loads(r['State'])==row and r['Graph']['Seed']==int(row[11])and r['Graph']['Ethereal']==bool(row[5]&128)
   assert int(row[12])==int(cases['inputs'][r['Case']][16])^0x123456789ABCDEF0
   base={v['Packed']:v['Value']for v in r['Graph']['ItemBase']};props={v['Packed']:v['Value']for v in r['Graph']['Properties']}
   for stat,value in row[13]:assert base.get(stat<<16,0)+props.get(stat<<16,0)==value
  for fixture in ('item-initialization','npc-retained-target','npc-incoming-lethal','npc-incoming-proc'):assert evidence.load(stage/(fixture+'-fixtures.json'))['Generation']==2
  inv=evidence.load(stage/'birth-inputs.json');assert inv['artifacts']==born['artifacts']and inv['copiedData']==born['copiedData']
  for path,digest in inv['sourceInputs'].items():assert evidence.sha(ROOT/path)==digest,path
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1055,'first-loop-affix-audit.json':21}
 evidence.verification(STAGE);assert len(evidence.load(STAGE/'verification-commands.json'))==18;protected=evidence.protection()
 sourcepath=ROOT/'tools/reference/item-quality-source-evidence.json';source=evidence.load(sourcepath);assert source['cases']==22656 and len(source['completeRoutineHashes'])==11 and not source['currentExecutableParity']
 for group in ('inputs','artifacts'):
  for path,digest in source[group].items():assert evidence.sha(ROOT/path)==digest,path
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tools/probe_item_quality.py',ROOT/'tests/check_item_quality.py',sourcepath]+[ROOT/p for p in CHANGED]
 files+=[ROOT/'docs'/n for n in ('item-quality-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files+=evidence.stage_files(STAGE,WIDE,COMPACT)+[ROOT/p for p in source['inputs']|source['artifacts']]
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=1,integrationBuild=1,sourceSnapshot=1,
  focusedRun=1,broaderRun=1,compactRun=1,focusedMarkers=395,broaderMarkers=611,compactMarkers=24,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,completeSourceControls=22656,sourceRoutines=11,originalControlledItemsPerCohort=18,pythonTests=71,verificationCommands=18,auditFingerprints=audits,protection=protected,
  scope='Complete older quality roll/dispatcher/finalizer/reset/fallback across6entries;18original controlled native graphs match ordered metadata/partial raw contributions/property RNG,unit RNG remains untouched,and original snapshots/metadata persist through2reloads with real HP-property continuation.',
  limits=['Synthetic ratios/unique tables and nested quality/ethereal/normal/automagic/property/world eligibility services remain declared inputs;their complete bodies,current table bindings and executable parity are not established.',
   'Original native identities persist in world saves;controlled metadata/graphs live in the fixture manifest and are explicitly restored. This is not automatic full-factory/NPC equipment persistence or real unique/drop-world admission.',
   'Full current affix pools/names/writers must be composed;valid unsupported properties must refuse explicitly rather than trigger a lower-quality roll.',
   'Nested quality/finalization,staffmods/automagic/ethereality,materialization,NPC graph/actor delivery,Valkyrie/Shadows and other unfinished skills remain required. Stable-item and bounded overflow/candidate contexts are explicit.',
   'No balance data/native/client/protocol/save-layout change,new learned handler,completed class,deployment or generated art. Effects/icons remain separate identified placeholders.'])
 evidence.write('item-quality-native-evidence.json',receipt,files,check)
 print('Item quality:22656source controls/11routines;18original controlled graphs through2reloads;395focused/611broader/24compact markers;71Python/18checks/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
