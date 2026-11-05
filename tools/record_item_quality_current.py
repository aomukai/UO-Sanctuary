#!/usr/bin/env python3
"""Bind prepared current quality/automagic to independent source and reloads."""
import argparse
from pathlib import Path
from record_current_item_affixes import expected as previous_expected
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT;STAGE=ROOT/'.local/class-skills/item-quality-current';WIDE=ROOT/'.local/class-skills/item-quality-current-broader';COMPACT=ROOT/'.local/class-skills/item-quality-current-compact'
PARENT='dbfe78a6d0fce7e1e86904215d514c1f7246f4da'
CHANGED={'src/Sanctuary.Content/CurrentItemAffixes.cs','src/Sanctuary.Content/CurrentAutoMagic.cs','src/Sanctuary.Content/CurrentItemQuality.cs','tests/Sanctuary.Integration/CurrentItemQualityChecks.cs'}
def expected(broader=False,compact=False):
 r=previous_expected(broader,compact);r['item-quality-current-check']+=1;r['item-quality-current-reload-check']+=2;return r
def fields(rows):return {r['Packed']:r['Value']for r in rows if r['Value']}
def pairs(rows):return {k:v for k,v in rows if v}
def build(check=False):
 born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-2.json')
 old=evidence.load(ROOT/'.local/class-skills/current-item-affixes/pre-build-2.json')['inputs'];now=evidence.load(STAGE/'pre-build-2.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']
 assert {p for p in old['copiedData'].keys()|now['copiedData'].keys()if old['copiedData'].get(p)!=now['copiedData'].get(p)}=={'Data/Sanctuary/Balance/item-quality-current.json'}
 parent=evidence.load(ROOT/'tools/reference/current-item-affixes-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 controls=evidence.load(ROOT/'.local/class-skills/item-quality-current-source/prepared-cases.json')
 assert len(controls['inputs'])==len(controls['outputs'])==15820 and len(controls['native'])==len(set(controls['native']))==2624
 assert len(controls['qualified'])==516 and sum(controls['inputs'][i][7]==0 for i in controls['native'])==2580
 for stage,wide,compact,count in ((STAGE,False,False,407),(WIDE,True,False,623),(COMPACT,False,True,36)):
  log=evidence.cohort(stage,expected(wide,compact),count)
  assert log.count('Current prepared quality:15820source cases;516bases/44automagic rows;2624original graphs')==3
  saved=evidence.load(stage/'item-quality-current-fixtures.json');assert saved['Generation']==2 and len(saved['Items'])==len({r['Item']for r in saved['Items']})==2624
  assert [r['Case']for r in saved['Items']]==controls['native']
  for r in saved['Items']:
   o=controls['outputs'][r['Case']];m=r['Metadata'];a=m['Affixes'];g=r['Graph']
   assert o[0]==1 and [m[k]for k in ('Quality','DropQuality','StartSeed')]==o[1:4]and m['UnitSeed']==int(o[4])
   assert [m[k]for k in ('Variant','File','AutoAffix','Flags')]==o[5:9]
   assert a['Identified']==bool(o[8]&16)and a['Prefixes']==o[9]and a['Suffixes']==o[10]
   assert a['RarePrefix']==o[11]and a['RareSuffix']==o[12]and a['Applied']==o[13]and m['AutoApplied']==o[14]
   assert g['Seed']==int(o[15])and g['HasProperties']==o[16]and not g['Ethereal']
   assert fields(g['ItemBase'])==pairs(o[17])and fields(g['Properties'])==pairs(o[18])
  for fixture,count2 in (('current-item-affixes',1680),('item-staffmods',414),('item-finishing',288)):
   d=evidence.load(stage/(fixture+'-fixtures.json'));assert d['Generation']==2 and len(d['Items'])==count2
  inv=evidence.load(stage/'birth-inputs.json');assert inv['artifacts']==born['artifacts']and inv['copiedData']==born['copiedData']
  for p,h in inv['sourceInputs'].items():assert evidence.sha(ROOT/p)==h,p
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-2.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1067,'first-loop-affix-audit.json':21}
 evidence.verification(STAGE);assert len(evidence.load(STAGE/'verification-commands.json'))==26;protected=evidence.protection()
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tools/probe_item_quality_current.py',ROOT/'tools/build_item_quality_current.py',ROOT/'tests/check_item_quality_current.py']+[ROOT/p for p in CHANGED]
 sourcepath=ROOT/'tools/reference/item-quality-current-source-evidence.json';source=evidence.load(sourcepath)
 assert source['cases']==15820 and source['originalNativeContexts']==2624 and not source['currentExecutableParity']
 unique=set(source['completeRoutineHashes'].values())|{h for group in source['inheritedRoutineGroups'].values()for h in group.values()};assert len(unique)==63
 assert list(source['reviewedOnlyRoutineHashes'])==['ITEMS_AllocItemData']
 tablepath=ROOT/'tools/reference/item-quality-current-table-evidence.json';table=evidence.load(tablepath)
 assert (table['types'],table['automagicRows'],table['ratioReferenceRows'])==(105,44,6)
 assert evidence.sha(ROOT/'data/Sanctuary/Balance/item-quality-current.json')==table['outputSha256']
 for receipt in (source,table):
  for group in ('inputs','artifacts'):
   for p,h in receipt.get(group,{}).items():assert evidence.sha(ROOT/p)==h,p
   files += [ROOT/p for p in receipt.get(group,{})]
 files += [sourcepath,tablepath,ROOT/'data/Sanctuary/Balance/item-quality-current.json']
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files += [ROOT/'docs'/n for n in ('item-quality-current-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += evidence.stage_files(STAGE,WIDE,COMPACT)
 for folder in STAGE.glob('failed-*'):files += [p for p in folder.rglob('*')if p.is_file()]
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],
  contentBuild=2,integrationBuild=2,sourceSnapshot=2,focusedRun=1,broaderRun=1,compactRun=1,focusedMarkers=407,broaderMarkers=623,compactMarkers=36,
  bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,completeSourceCompositions=15820,distinctCompleteOriginalRoutines=63,qualifiedBases=516,automagicRows=44,
  originalItemsPerCohort=2624,retainedAffixItems=1680,retainedStaffmodItems=414,retainedFinishingItems=288,pythonTests=71,verificationCommands=26,auditFingerprints=audits,protection=protected,
  scope='Complete older base initialization/quality fallback/finishing compose with current magic/rare,staffmods,approved superior/tree delivery and all44owned automagic rows. Separate explicit unit/item RNG,raw fields,reference affix IDs and2624original identities/metadata/graphs survive two reloads and next-property continuation.',
  limits=['Qualified component is format100,516nonquest/nonunique body-equipment bases,requested qualities1/2/3/4/6 and NOSOCKETS+NEVERETH. Explicit initialized seed streams do not establish allocation-seed provenance or modern executable parity.',
   'Allocation initializer is reviewed only,not compiled/executed by the source probe. ABI services supply current immutable metadata,logical type queries,successful list allocation and raw stat/seed transfer. Full outer allocation INIT/INSTORE flags,world services,materialization,NOSSELL/equip/full durability delivery,NPC equipment persistence and actors remain open.',
   'Six current ratio rows are retained reference only. Unforced/unique/set/craft/quest/special-normal contexts refuse explicitly. Reference row IDs are not modern compiled/binary/save identities;war-tab-rand and corrupted-jewel rare-cap remain unresolved outside this domain.',
   'Original world identities are native-saved;graphs and metadata are explicitly restored from the fixture manifest. No installed build/save/config/client changes,new handler,completed class,deployment or art. Effects/audio and icons remain separate identified placeholders.'])
 evidence.write('item-quality-current-native-evidence.json',receipt,files,check)
 print('Prepared quality:15820compositions/63original routines;2624original graphs/two reloads;407focused/623broader/36compact;71Python/26checks/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
