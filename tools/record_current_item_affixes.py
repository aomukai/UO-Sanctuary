#!/usr/bin/env python3
"""Bind composed current affix/raw property and original-item acceptance."""
import argparse
from pathlib import Path
from record_item_staffmods import expected as previous_expected
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT;STAGE=ROOT/'.local/class-skills/current-item-affixes';WIDE=ROOT/'.local/class-skills/current-item-affixes-broader';COMPACT=ROOT/'.local/class-skills/current-item-affixes-compact'
PARENT='38198887e6e5e55af1f63ae998beaece1737ab69'
CHANGED={'src/Sanctuary.Content/CurrentItemAffixes.cs','src/Sanctuary.Content/CurrentTreePropertyWriter.cs','src/Sanctuary.Content/SourceAffixPicker.cs','src/Sanctuary.Content/NpcItemAffixes.cs','tests/Sanctuary.Integration/CurrentItemAffixChecks.cs'}
def expected(broader=False,compact=False):
 r=previous_expected(broader,compact);r['current-item-affixes-check']+=1;r['current-item-affixes-reload-check']+=2;return r
def fields(rows):return {r['Packed']:r['Value']for r in rows if r['Value']}
def pairs(rows):return {k:v for k,v in rows if v}

def build(check=False):
 born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-2.json')
 old=evidence.load(ROOT/'.local/class-skills/item-staffmods/pre-build-3.json')['inputs'];now=evidence.load(STAGE/'pre-build-2.json')['inputs']
 changed={p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)};assert changed==CHANGED,changed
 assert old['projectInputs']==now['projectInputs']
 assert {p for p in old['copiedData'].keys()|now['copiedData'].keys()if old['copiedData'].get(p)!=now['copiedData'].get(p)}=={'Data/Sanctuary/Balance/item-affix-layout.json'}
 parent=evidence.load(ROOT/'tools/reference/item-staffmods-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 controls=evidence.load(ROOT/'.local/class-skills/current-item-affixes-source/cases.json')
 assert len(controls['inputs'])==len(controls['outputs'])==14888 and len(controls['native'])==len(set(controls['native']))==1680
 assert sum(controls['inputs'][i][4]in(0,1)for i in controls['native'])==692
 assert sum(controls['inputs'][i][4]==2 for i in controls['native'])==988
 assert len({a[0]for a in controls['inputs']if a[4]in(0,1)})==692
 for stage,wide,compact,count in ((STAGE,False,False,404),(WIDE,True,False,620),(COMPACT,False,True,33)):
  log=evidence.cohort(stage,expected(wide,compact),count)
  assert log.count('Current item affixes:14888composed source cases;692bases/988affix rows/89property codes/216approved tree controls;1680original graphs')==3
  saved=evidence.load(stage/'current-item-affixes-fixtures.json');assert saved['Generation']==2 and len(saved['Items'])==len({r['Item']for r in saved['Items']})==1680
  assert [r['Case']for r in saved['Items']]==controls['native']
  for r in saved['Items']:
   o=controls['outputs'][r['Case']];m=r['Metadata'];g=r['Graph']
   assert r['Result']==o[0]and m['Identified']==bool(o[1]&16)and m['RarePrefix']==o[2]and m['RareSuffix']==o[3]
   assert m['Prefixes']==o[4]and m['Suffixes']==o[5]and m['Applied']==o[6]
   assert g['Seed']==int(o[7])and g['HasProperties']==o[8]and not g['Ethereal']
   assert fields(g['ItemBase'])==pairs(o[9])and fields(g['Properties'])==pairs(o[10])
  for fixture in ('item-staffmods','item-finishing','item-quality','item-initialization','npc-retained-target','npc-incoming-lethal','npc-incoming-proc'):
   assert evidence.load(stage/(fixture+'-fixtures.json'))['Generation']==2
  assert len(evidence.load(stage/'item-staffmods-fixtures.json')['Items'])==414 and len(evidence.load(stage/'item-finishing-fixtures.json')['Items'])==288
  inv=evidence.load(stage/'birth-inputs.json');assert inv['artifacts']==born['artifacts']and inv['copiedData']==born['copiedData']
  for p,h in inv['sourceInputs'].items():assert evidence.sha(ROOT/p)==h,p
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-2.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1064,'first-loop-affix-audit.json':21}
 evidence.verification(STAGE);assert len(evidence.load(STAGE/'verification-commands.json'))==24;protected=evidence.protection()
 sourcepath=ROOT/'tools/reference/current-item-affixes-source-evidence.json';source=evidence.load(sourcepath)
 assert source['cases']==14888 and source['originalNativeContexts']==1680 and not source['currentExecutableParity']
 unique={h for group in source['completeRoutineGroups'].values()for h in group.values()};assert len(unique)==43
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tools/probe_current_item_affixes.py',ROOT/'tests/check_current_item_affixes.py',sourcepath]+[ROOT/p for p in CHANGED]
 for group in ('inputs','artifacts'):
  for p,h in source[group].items():assert evidence.sha(ROOT/p)==h,p
  files += [ROOT/p for p in source[group]]
 layoutpath=ROOT/'tools/reference/item-affix-layout-evidence.json';layout=evidence.load(layoutpath)
 assert layout['magicAffixIds']==991 and layout['selectableAffixRows']==988 and layout['selectablePropertyCodes']==89
 for p,h in layout['inputs'].items():assert evidence.sha(ROOT/p)==h,p
 files += [layoutpath,ROOT/'data/Sanctuary/Balance/item-affix-layout.json']+[ROOT/p for p in layout['inputs']]
 decision=ROOT/'tools/reference/item-tree-packing-decision.json';assert evidence.load(decision)['status']=='user-approved-sanctuary-adaptation';files.append(decision)
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files += [ROOT/'docs'/n for n in ('current-item-affixes-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += evidence.stage_files(STAGE,WIDE,COMPACT)
 for name in ('source-attempt-1','failed-compact-1','failed-compact-2','failed-compact-3','failed-compact-4'):
  files += [p for p in (STAGE/name).rglob('*')if p.is_file()]
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],
  contentBuild=2,integrationBuild=2,sourceSnapshot=2,focusedRun=1,broaderRun=1,compactRun=5,focusedMarkers=404,broaderMarkers=620,compactMarkers=33,
  bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,completeSourceCompositions=14888,distinctCompleteOriginalRoutines=43,currentBases=692,selectableAffixRows=988,propertyCodes=89,
  approvedTreeControls=216,originalItemsPerCohort=1680,retainedStaffmodItems=414,retainedFinishingItems=288,pythonTests=71,verificationCommands=24,auditFingerprints=audits,protection=protected,
  approvedAdaptations=['tools/reference/item-tree-packing-decision.json','tools/reference/item-staffmods-tier-decision.json'],
  scope='Complete old magic/rare callers,pickers/group/type/rare-name rules,affix dispatch/current wrapper and raw numeric/proc/charge fields compose on existing item RNG/graphs with approved tree/staffmod variants. All14888C#compositions match source;1680original item identities,metadata/raw snapshots and next property rolls survive two reloads.',
  limits=['Current executable parity remains unproved. Supplied namespace services define current immutable metadata,successful list allocation,logical type/class queries and bit-identical seed/raw graph transfers;only format100/affix mode0 is exercised.',
   'Raw row IDs and joined affix offsets are internal reference identities,not proved current compiled/binary/save IDs. Primary-jewel lookup is explicitly symbolic;neutral class sentinel is distinct from valid Warlock7.',
   'The current derived corrupted-jewel cjwl modern rare-cap behavior is uncertain. The qualified reference adapter preserves the older primary-type jewel branch;public materialization must not silently certify that new type.',
   'Old first-socket eligibility excludes three rows;no supported-property filter is added. Ordinary tree packing is explicitly approved;war-tab-rand remains unsupported. Generic fresh numeric201/original projection191 remain unchanged.',
   'Original world identities are native-saved;graphs/metadata are explicitly restored from the fixture manifest. Full quality fallback,automagic/quantity,materialization,NPC equipment persistence/event/skill delivery,actors/learned skills remain required.',
   'No installed build/save/config/client change,new handler,completed class,deployment or generated art. Effects/audio and icons remain separate identified placeholders.'])
 evidence.write('current-item-affixes-native-evidence.json',receipt,files,check)
 print('Current affixes:14888compositions/43original routines;1680original graphs/two reloads;404focused/620broader/33compact;71Python/24checks/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
