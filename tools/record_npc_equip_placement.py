#!/usr/bin/env python3
"""Bind complete placement classifiers to native physical NPC delivery."""
import argparse,json
from pathlib import Path
from record_generated_npc_item import expected as previous_expected
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT;STAGE=ROOT/'.local/class-skills/npc-equip-placement';COMPACT=ROOT/'.local/class-skills/npc-equip-placement-compact';PARENT='20ffe652142c5ca4052907a484c58817cffbc627'
CHANGED={'src/Sanctuary.Content/SourceEquipPlacement.cs','src/Sanctuary.Content/GeneratedNpcEquipment.cs','src/Sanctuary.Content/GeneratedNpcItem.cs','tests/Sanctuary.Integration/NpcEquipPlacementChecks.cs'}
def expected(broader=False,compact=False):
 r=previous_expected(broader,compact);r['npc-equip-placement-check']+=1;r['npc-equip-placement-reload-check']+=2;return r
def fields(rows):return {r['Packed']:r['Value']for r in rows if r['Value']}
def pairs(rows):return {k:v for k,v in rows if v}
def build(check=False):
 born=evidence.load(COMPACT/'birth-inputs.json');audit=evidence.load(STAGE/'audit-inputs.json');assert born['artifacts']==audit['artifacts']and born['copiedData']==audit['copiedData']
 for path in (COMPACT/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-3.json',path)
  for p,h in evidence.load(path)['sourceInputs'].items():assert evidence.sha(ROOT/p)==h,p
 for kind in ('Content','Integration'):assert evidence.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=evidence.load(ROOT/'.local/class-skills/generated-npc-item/pre-build-1.json')['inputs'];now=evidence.load(STAGE/'pre-build-3.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']
 assert {p for p in old['copiedData'].keys()|now['copiedData'].keys()if old['copiedData'].get(p)!=now['copiedData'].get(p)}=={'Data/Sanctuary/Balance/npc-equip-types.json'}
 parent=evidence.load(ROOT/'tools/reference/generated-npc-item-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 sourcepath=ROOT/'tools/reference/npc-equip-placement-source-evidence.json';source=evidence.load(sourcepath);controls=evidence.load(ROOT/'.local/class-skills/npc-equip-placement-source/cases.json')
 assert source['cases']==len(controls['inputs'])==len(controls['outputs'])==140364 and source['bodyControls']==26988 and len(source['completeRoutineHashes'])==8 and not source['currentExecutableParity']
 assert source['noSellFlag']==4096 and len(source['reviewedOnlyRoutineHashes'])==5
 base=evidence.load(ROOT/'.local/class-skills/item-quality-current-source/prepared-cases.json');native=[i for i in base['native']if base['inputs'][i][7]==0 and base['inputs'][i][2]in(1,3,6)];assert len(native)==1548
 log=evidence.cohort(COMPACT,expected(False,True),42);assert log.count('NPC equipment placement:140364complete source classifier controls/26988current body contexts;1548original equipped items/516bases')==3
 saved=evidence.load(COMPACT/'npc-equip-placement-fixtures.json');assert saved['Generation']==2 and len(saved['Items'])==1548 and len(saved['Shadows'])==2
 assert [r['Case']for r in saved['Items']]==native and len({r['Actor']for r in saved['Items']})==len({r['Item']for r in saved['Items']})==1548
 for r in saved['Items']:
  o=base['outputs'][r['Case']];s=json.loads(r['SavedText']);m=s['Metadata'];a=m['Affixes'];g=s['Graph'];e=s['Equipment']
  assert s['Version']==1 and e['Actor']==r['Actor']and e['Owner']==saved['Owner']and e['Kind']==0 and e['Flags']==4096
  assert [m[k]for k in ('Quality','DropQuality','StartSeed')]==o[1:4]and m['UnitSeed']==int(o[4])and [m[k]for k in ('Variant','File','AutoAffix','Flags')]==o[5:9]
  assert a['Prefixes']==o[9]and a['Suffixes']==o[10]and a['RarePrefix']==o[11]and a['RareSuffix']==o[12]and a['Applied']==o[13]and m['AutoApplied']==o[14]and a['Identified']
  assert g['Seed']==int(o[15])and g['HasProperties']==o[16]and not g['Ethereal']and fields(g['Properties'])==pairs(o[18])
  before=pairs(o[17]);after=fields(g['ItemBase']);props=fields(g['Properties']);maximum=after.get(73<<16,0)+props.get(73<<16,0)+int(after.get(73<<16,0)*(props.get(75<<16,0)/100.0))
  before.pop(72<<16,None);rest=dict(after);assert rest.pop(72<<16,0)==maximum;assert rest==before
 assert len({i for r in saved['Shadows']for i in r['Items']})==4
 for r in saved['Shadows']:
  assert len(r['Items'])==len(r['SavedText'])==2
  for text in r['SavedText']:
   state=json.loads(text);assert state['Version']==1 and state['Equipment']['Actor']==r['Actor']and state['Equipment']['Owner']==saved['Owner']and state['Equipment']['Kind']in(1,2)
  assert {json.loads(t)['Equipment']['Body']for t in r['SavedText']}=={4,5}
 for fixture,count in (('generated-npc-item',2580),('item-quality-current',2624),('current-item-affixes',1680)):
  d=evidence.load(COMPACT/(fixture+'-fixtures.json'));assert d['Generation']==2 and len(d['Items'])==count
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-3.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1071,'first-loop-affix-audit.json':21}
 commands=evidence.load(STAGE/'verification-commands.json');assert len(commands)==6 and all(c['exitCode']==0 for c in commands)
 python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in python and '\nOK\n'in python;protected=evidence.protection()
 tablepath=ROOT/'tools/reference/npc-equip-types-evidence.json';table=evidence.load(tablepath);assert (table['typeRows'],table['bodyRows'],table['baseCrossChecks'])==(105,13,692)
 assert evidence.sha(ROOT/'data/Sanctuary/Balance/npc-equip-types.json')==table['outputSha256']
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_npc_equip_placement.py',sourcepath,tablepath,ROOT/'data/Sanctuary/Balance/npc-equip-types.json']+[ROOT/p for p in CHANGED]
 for receipt in (source,table):
  for group in ('inputs','artifacts'):
   for p,h in receipt.get(group,{}).items():assert evidence.sha(ROOT/p)==h,p
   files += [ROOT/p for p in receipt.get(group,{})]
 files += [ROOT/'docs'/n for n in ('npc-equip-placement-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += evidence.stage_files(STAGE,COMPACT)
 for folder in [*STAGE.glob('failed-*'),STAGE/'pre-reader-alignment']:files += [p for p in folder.rglob('*')if p.is_file()]
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=3,integrationBuild=3,sourceSnapshot=3,compactRun=3,compactMarkers=42,
  boots=3,actualReloads=2,saves=3,completeClassifierControls=140364,completeOriginalRoutines=8,inlineBodyValidator=True,currentBodyControls=26988,equippedOriginals=1548,qualifiedBases=516,dualClawOriginals=4,dualClawHolders=2,nativePayloadRestoredFromManifest=False,
  pythonTests=71,verificationCommands=6,auditFingerprints=audits,protection=protected,
  scope='Eight complete older classifier bodies plus inline body validation retain all body/hand/stack outcomes and lazy gates. Native physical generated-item delivery accepts outcome1 only,skips requirements,binds canonical body/owner/kind/NOSELL,repairs operative durability after attachment and deletes failed new items.1548original item/actor pairs and four original dual claws retain native payloads and parent identities through two reloads.',
  limits=['Native storage is a controlled-summon private body inventory with no exchange grid;it does not implement the complete source equip dispatcher or its stat/client/animation/skill callbacks. Actual recipient stat/hand/skill/event consumers and learned Valkyrie/Shadow actors remain required.',
   'The outer NPC creation caller and cached durability-reader chain are pinned reviewed-only routines,not executed dispatcher proof. Source monster labels and type/affix IDs are logical reference identities,not current compiled/save IDs. Old player classifiers do not establish modern Warlock hand behavior;the existing Warlock-specific player adapter remains separate.',
   'The native adapter admits primary slots1..10 and existing qualified format100/requested quality/no-sockets/no-ethereal generation. Switch slots,stack/replacement/exchange outcomes remain classifier proof,not admitted NPC operations. Current executable/world/allocation RNG parity remains unproved.',
   'Unattached v0 payloads retain their encoding;equipped bindings use v1 with explicit structural validation. Source field contributions remain item-owned;no player GearState projection or reroll occurs. Stock art0x14F0/Layer.Invalid remain identified presentation placeholders.',
   '42compact markers,71Python tests,six checks and fresh audits/protection are current validation. Full class combat cohorts are prior parent evidence. No installed build/save/config/client change,new handler,completed class or deployment.'])
 evidence.write('npc-equip-placement-native-evidence.json',receipt,files,check)
 print('NPC placement:140364source controls;1548equipped originals+four dual claws/two reloads;42compact markers;71Python/6checks/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
