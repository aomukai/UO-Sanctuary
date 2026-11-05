#!/usr/bin/env python3
"""Verify actual native serialized generated items against prepared source cases."""
import argparse
import json
from pathlib import Path
from record_item_quality_current import expected as previous_expected
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT;STAGE=ROOT/'.local/class-skills/generated-npc-item';COMPACT=ROOT/'.local/class-skills/generated-npc-item-compact'
PARENT='005657aa9a80e4883e03723f38965be7edfdc3e8'
CHANGED={'src/Sanctuary.Content/GeneratedNpcItem.cs','tests/Sanctuary.Integration/CurrentItemQualityChecks.cs','tests/Sanctuary.Integration/GeneratedNpcItemChecks.cs'}
def expected(broader=False,compact=False):
 r=previous_expected(broader,compact);r['generated-npc-item-check']+=1;r['generated-npc-item-reload-check']+=2;return r
def fields(rows):return {r['Packed']:r['Value']for r in rows if r['Value']}
def pairs(rows):return {k:v for k,v in rows if v}
def build(check=False):
 born=evidence.load(COMPACT/'birth-inputs.json');audit=evidence.load(STAGE/'audit-inputs.json')
 assert born['artifacts']==audit['artifacts']and born['copiedData']==audit['copiedData']
 for path in (COMPACT/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-1.json',path)
  for p,h in evidence.load(path)['sourceInputs'].items():assert evidence.sha(ROOT/p)==h,p
 for kind in ('Content','Integration'):assert evidence.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=evidence.load(ROOT/'.local/class-skills/item-quality-current/pre-build-2.json')['inputs'];now=evidence.load(STAGE/'pre-build-1.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=evidence.load(ROOT/'tools/reference/item-quality-current-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 controls=evidence.load(ROOT/'.local/class-skills/item-quality-current-source/prepared-cases.json');native=[i for i in controls['native']if controls['inputs'][i][7]==0]
 assert len(native)==2580 and len({controls['inputs'][i][0]for i in native})==516
 for stage,wide,compact,count in ((COMPACT,False,True,39),):
  log=evidence.cohort(stage,expected(wide,compact),count)
  assert log.count('Generated NPC items:2580original native serialized items/516bases/five qualities')==3
  saved=evidence.load(stage/'generated-npc-item-fixtures.json');assert saved['Generation']==2 and len(saved['Items'])==len({r['Item']for r in saved['Items']})==2580
  assert [r['Case']for r in saved['Items']]==native
  for r in saved['Items']:
   assert set(r)=={'Case','Item','SavedText'} # no state restoration fields
   a=controls['inputs'][r['Case']];o=controls['outputs'][r['Case']];s=json.loads(r['SavedText']);m=s['Metadata'];f=m['Affixes'];g=s['Graph']
   assert s['Version']==0 and s['ReferenceVersion']=='3.3.93847'
   assert s['Request']==dict(zip(('Code','Level','Quality','Difficulty','UnitSeed','ItemSeed','StartSeed'),[a[0],a[1],a[2],a[3],int(a[4]),int(a[5]),a[6]]))
   assert m['Code']==a[0]and [m[k]for k in ('Quality','DropQuality','StartSeed')]==o[1:4]and m['UnitSeed']==int(o[4])
   assert [m[k]for k in ('Variant','File','AutoAffix','Flags')]==o[5:9]
   assert f['Identified']==bool(o[8]&16)and f['Prefixes']==o[9]and f['Suffixes']==o[10]
   assert f['RarePrefix']==o[11]and f['RareSuffix']==o[12]and f['Applied']==o[13]and m['AutoApplied']==o[14]
   assert g['Seed']==int(o[15])and g['HasProperties']==o[16]and not g['Ethereal']
   assert fields(g['ItemBase'])==pairs(o[17])and fields(g['Properties'])==pairs(o[18])
  for fixture,count2 in (('item-quality-current',2624),('current-item-affixes',1680),('item-staffmods',414),('item-finishing',288)):
   d=evidence.load(stage/(fixture+'-fixtures.json'));assert d['Generation']==2 and len(d['Items'])==count2
  inv=evidence.load(stage/'birth-inputs.json');assert inv['artifacts']==born['artifacts']and inv['copiedData']==born['copiedData']
  for p,h in inv['sourceInputs'].items():assert evidence.sha(ROOT/p)==h,p
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1068,'first-loop-affix-audit.json':21}
 commands=evidence.load(STAGE/'verification-commands.json');assert len(commands)==4 and all(c['exitCode']==0 for c in commands)
 python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in python and '\nOK\n'in python;protected=evidence.protection()
 sourcepath=ROOT/'tools/reference/item-quality-current-source-evidence.json';source=evidence.load(sourcepath)
 assert source['cases']==15820 and source['qualifiedBases']==516 and not source['currentExecutableParity']
 for group in ('inputs','artifacts'):
  for p,h in source[group].items():assert evidence.sha(ROOT/p)==h,p
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_generated_npc_item.py',sourcepath]+[ROOT/p for p in CHANGED]+[ROOT/p for group in ('inputs','artifacts')for p in source[group]]
 files += [ROOT/'docs'/n for n in ('generated-npc-item-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += evidence.stage_files(STAGE,COMPACT)
 for folder in STAGE.glob('failed-*'):files += [p for p in folder.rglob('*')if p.is_file()]
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],
  contentBuild=1,integrationBuild=1,sourceSnapshot=1,compactRun=1,compactMarkers=39,
  bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalNativeSerializedItemsPerCohort=2580,qualifiedBases=516,requestedQualities=[1,2,3,4,6],nativePayloadRestoredFromManifest=False,
  retainedPreparedItems=2624,retainedAffixItems=1680,pythonTests=71,verificationCommands=4,auditFingerprints=audits,protection=protected,
  scope='GeneratedNpcItem creates a native identity from the existing complete prepared component and serializes its request,metadata,unit/item seeds and raw item graph in the item itself. All2580original native items retain exact source fields and next-property continuation through two reloads;manifest equality witnesses never restore item payloads. Invalid contexts allocate no item;decoded snapshots have no mutable alias;player lift/equip/trade routes refuse;deletion removes identity.',
  limits=['Creation remains format100,explicit supplied separate seed streams,516nonquest/nonunique body-equipment bases,requested qualities1/2/3/4/6,NOSOCKETS+NEVERETH. No modern executable or allocation/world RNG parity is claimed.',
   'Native item materialization/serialization is complete for this qualified unattached component. NPC body placement/equip validity/NOSELL/post-equip durability,live stat/hand/skill/event delivery and full summon actors remain required. Read-only restoration requires explicit recipient services and never projects through player GearState.',
   'Raw reference affix IDs retain their pinned3.3.93847 namespace;new versions require explicit migration. Unforced/unique/set/craft/quest/special-normal and unresolved random-tree/corrupted-jewel contexts remain separate. Native art0x14F0 and the item name explicitly identify the placeholder;no skill-effect/audio/icon completion is inferred.',
   'Validation is39compact native markers,71Python tests,four verification commands and fresh audits/protection. Full class combat cohorts are retained parent evidence,not rerun or claimed as fresh for this isolated new item type. No installed build/save/config/client change,new handler,completed class or deployment.'])
 evidence.write('generated-npc-item-native-evidence.json',receipt,files,check)
 print('Generated native items:2580original payloads/two reloads;39compact markers;71Python/4checks/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
