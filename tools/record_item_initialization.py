#!/usr/bin/env python3
"""Bind complete base-stat initialization and original native graph acceptance."""
import argparse
from pathlib import Path
from record_npc_retained_target import expected as previous_expected
import skill_checkpoint_evidence as evidence
ROOT=evidence.ROOT
STAGE=ROOT/'.local/class-skills/item-initialization'
WIDE=ROOT/'.local/class-skills/item-initialization-broader'
COMPACT=ROOT/'.local/class-skills/item-initialization-compact'
PARENT='c73d5c9303338a2913a9c9ffde0814f635e7ec13'
CHANGED={'src/Sanctuary.Content/'+n+'.cs'for n in ('CurrentItemInitialization','SourceItemInitialization')}
CHANGED.add('tests/Sanctuary.Integration/ItemInitializationChecks.cs')
def expected(broader=False,compact=False):
 result=previous_expected(broader,compact);result['item-initialization-check']+=1;result['item-initialization-reload-check']+=2;return result

def build(check=False):
 born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-4.json')
 before=evidence.load(ROOT/'.local/class-skills/npc-retained-target/pre-build-1.json')['inputs'];now=evidence.load(STAGE/'pre-build-4.json')['inputs']
 changed={p for p in before['sourceInputs'].keys()|now['sourceInputs'].keys()if before['sourceInputs'].get(p)!=now['sourceInputs'].get(p)};assert changed==CHANGED,changed
 assert before['projectInputs']==now['projectInputs']
 newdata='Data/Sanctuary/Balance/item-initialization-types.json';assert now['copiedData'].keys()-before['copiedData'].keys()=={newdata}
 assert before['copiedData']=={k:v for k,v in now['copiedData'].items()if k!=newdata}
 cases=evidence.load(ROOT/'.local/class-skills/item-initialization-source/cases.json');assert len(cases['inputs'])==19296 and all(r[1]!=r[12]for r in cases['inputs'])
 parent=evidence.load(ROOT/'tools/reference/npc-retained-target-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 for stage,wide,compact,count in ((STAGE,False,False,392),(WIDE,True,False,608),(COMPACT,False,True,21)):
  log=evidence.cohort(stage,expected(wide,compact),count);assert log.count('Item initialization:19296complete source controls,692current bases/105types;42original unattached native item graphs')==3
  saved=evidence.load(stage/'item-initialization-fixtures.json');assert saved['Generation']==2 and len(saved['Items'])==42
  assert len({r['Item']for r in saved['Items']})==42 and len({r['Case']for r in saved['Items']})==42
  assert sum(r['Extra']!=0 for r in saved['Items'])==6 and {r['Extra']for r in saved['Items']}=={0,70,600}
  for r in saved['Items']:
   assert not r['BaseState']['HasProperties']and not r['BaseState']['Properties']and not r['BaseState']['Ethereal']
   stats={v['Packed']:v['Value']for v in r['BaseState']['ItemBase']};assert stats.get(254<<16,0)==r['Extra']
   output=cases['outputs'][r['Case']];assert r['UnitSeed']==int(output[1])and r['BaseState']['Seed']==int(output[12])
   assert {k:v for k,v in stats.items()if v}=={stat<<16:value for stat,value in output[2]if value}
  for fixture in ('npc-retained-target','npc-incoming-lethal','npc-incoming-proc'):assert evidence.load(stage/(fixture+'-fixtures.json'))['Generation']==2
  inventory=evidence.load(stage/'birth-inputs.json');assert inventory['artifacts']==born['artifacts']and inventory['copiedData']==born['copiedData']
  for path,digest in inventory['sourceInputs'].items():assert evidence.sha(ROOT/path)==digest,path
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-4.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1054,'first-loop-affix-audit.json':21}
 evidence.verification(STAGE);protected=evidence.protection()
 sourcepath=ROOT/'tools/reference/item-initialization-source-evidence.json';source=evidence.load(sourcepath)
 assert source['cases']==19296 and source['currentBases']==692 and source['syntheticBases']==14 and len(source['completeRoutineHashes'])==9 and not source['currentExecutableParity']
 for group in ('inputs','artifacts'):
  for path,digest in source[group].items():assert evidence.sha(ROOT/path)==digest,path
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tools/build_item_initialization.py',ROOT/'tools/probe_item_initialization.py',ROOT/'tests/check_item_initialization.py',sourcepath,ROOT/'data/Sanctuary/Balance/item-initialization-types.json']+[ROOT/p for p in CHANGED]
 files+=[ROOT/'docs'/n for n in ('item-initialization-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files+=evidence.stage_files(STAGE,WIDE,COMPACT)
 files+=[ROOT/p for p in source['inputs']|source['artifacts']]
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=4,integrationBuild=4,sourceSnapshot=4,
  focusedRun=2,broaderRun=2,compactRun=4,focusedMarkers=392,broaderMarkers=608,compactMarkers=21,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,currentBases=692,currentTypes=105,completeSourceControls=19296,originalUnattachedItemsPerCohort=42,pythonTests=71,auditFingerprints=audits,protection=protected,
  scope='Complete older base-stat initializer and stack/seed dependencies,current692base/105type descriptors,42original unattached native graphs;distinct source-exact unit/base and item-data/property streams,base fields/variation/seeds/draws,explicit extra-stack retention and real subsequent HP-property RNG through2reloads.',
  limits=['Quality,gold,elixir and quest-list side effects are declared source-fixture services;the complete bodies and modern executable parity are not proved.',
   'Original native items survive world saves;graph states live in the fixture manifest and are explicitly restored through the existing snapshot API. This is not automatic full-factory/NPC graph persistence.',
   'Full quality/low-seed fallback,automagic/staffmods/ethereality/finalization,native materialization,NPC graph attachment,Valkyrie/Shadows actors and other unfinished class skills remain required.',
   'No existing balance table rewrite,player loot admission,native/client/protocol/save-layout change,new learned handler,completed class,deployment or generated art. Effects/icons remain separate identified placeholders.'])
 evidence.write('item-initialization-native-evidence.json',receipt,files,check)
 print('Item initialization:19296source controls,692bases/105types,42original native graph cases;392focused/608broader/21compact markers through2reloads;71Python/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
