#!/usr/bin/env python3
"""Bind source finishing, approved current superior delivery and original graphs."""
import argparse
from collections import Counter
import json
from pathlib import Path
from record_item_quality import expected as previous_expected
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT
STAGE=ROOT/'.local/class-skills/item-finishing'
WIDE=ROOT/'.local/class-skills/item-finishing-broader'
COMPACT=ROOT/'.local/class-skills/item-finishing-compact'
PARENT='86784239c2644785115c4f7354c095e32e34b2f3'
CHANGED={'src/Sanctuary.Content/SourceItemFinishing.cs','src/Sanctuary.Content/CurrentItemFinishing.cs',
 'src/Sanctuary.Content/ItemDurabilityProperties.cs','src/Sanctuary.Content/ItemPropertyCreationContext.cs',
 'tests/Sanctuary.Integration/ItemFinishingChecks.cs'}

def expected(broader=False,compact=False):
 r=previous_expected(broader,compact)
 r['item-finishing-check']+=1;r['item-finishing-reload-check']+=2
 return r

def fields(rows):return {r['Packed']:r['Value']for r in rows if r['Value']}
def pairs(rows,packed=True):return {(k if packed else k<<16):v for k,v in rows if v}

def build(check=False):
 born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-2.json')
 old=evidence.load(ROOT/'.local/class-skills/item-quality/pre-build-1.json')['inputs']
 now=evidence.load(STAGE/'pre-build-2.json')['inputs']
 changed={p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
 assert changed==CHANGED,changed
 assert old['projectInputs']==now['projectInputs']
 assert {p for p in old['copiedData'].keys()|now['copiedData'].keys()if old['copiedData'].get(p)!=now['copiedData'].get(p)}=={'Data/Sanctuary/Balance/item-finishing-catalogue.json'}
 parent=evidence.load(ROOT/'tools/reference/item-quality-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 controls=evidence.load(ROOT/'.local/class-skills/item-finishing-source/cases.json')
 numeric=evidence.load(ROOT/'.local/class-skills/item-finishing-properties-source/cases.json')
 assert len(controls['inputs'])==20548 and len(controls['nativeInputs'])==96
 assert len(numeric['cases'])==192 and len(numeric['selected'])==20
 selected={r[0]:r for r in numeric['selected']}
 for stage,wide,compact,count in ((STAGE,False,False,398),(WIDE,True,False,614),(COMPACT,False,True,27)):
  log=evidence.cohort(stage,expected(wide,compact),count)
  assert log.count('Item finishing:20548source controls/14routines,5536current eligibility pairs;192approved numeric compositions and96current physical contexts on288original items')==3
  saved=evidence.load(stage/'item-finishing-fixtures.json')
  assert saved['Generation']==2 and len(saved['Items'])==len({r['Item']for r in saved['Items']})==288
  assert Counter(r['Kind']for r in saved['Items'])=={'property':192,'current':96}
  for r in saved['Items']:
   g=r['Graph'];index=r['Case']
   if r['Kind']=='property':
    out=numeric['cases'][index]
    assert g['Seed']==int(out[4]) and fields(g['ItemBase'])==pairs(out[5])and fields(g['Properties'])==pairs(out[6])
   else:
    out=controls['nativeOutputs'][index];meta=r['Metadata']
    assert meta['Parameters']['Code']==controls['nativeCodes'][index]and meta['Parameters']['UnitSeed']==int(out[9])
    assert meta['File']==out[1]and meta['ItemFlags']==out[7]
    if index in selected:
     row=selected[index];assert g['Seed']==int(row[2])and fields(g['ItemBase'])==pairs(row[3])and fields(g['Properties'])==pairs(row[4])
    else:assert g['Seed']==int(out[8])and fields(g['ItemBase'])==pairs(out[10],False)and not g['Properties']
  for fixture in ('item-quality','item-initialization','npc-retained-target','npc-incoming-lethal','npc-incoming-proc'):
   assert evidence.load(stage/(fixture+'-fixtures.json'))['Generation']==2
  inv=evidence.load(stage/'birth-inputs.json')
  assert inv['artifacts']==born['artifacts']and inv['copiedData']==born['copiedData']
  for path,digest in inv['sourceInputs'].items():assert evidence.sha(ROOT/path)==digest,path
 for kind in ('content','integration'):
  assert all(t in (STAGE/(kind+'-build-2.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts'])
 assert audits=={'named-item-runtime-audit.json':1059,'first-loop-affix-audit.json':21}
 evidence.verification(STAGE);assert len(evidence.load(STAGE/'verification-commands.json'))==21
 protected=evidence.protection()
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_item_finishing.py']+[ROOT/p for p in CHANGED]
 for name,count,routines in (('item-finishing-source-evidence.json',20548,14),('item-finishing-properties-source-evidence.json',192,21)):
  path=ROOT/'tools/reference'/name;source=evidence.load(path)
  assert source['cases']==count and len(source['completeRoutineHashes'])==routines and not source['currentExecutableParity']
  files.append(path)
  for group in ('inputs','artifacts'):
   for p,digest in source[group].items():assert evidence.sha(ROOT/p)==digest,p
   files += [ROOT/p for p in source[group]]
 tablepath=ROOT/'tools/reference/item-finishing-table-evidence.json';table=evidence.load(tablepath)
 assert (table['superior'],table['inferior'],table['types'])==(8,4,105)
 for p,digest in table['inputs'].items():assert evidence.sha(ROOT/p)==digest,p
 files += [tablepath]+[ROOT/p for p in table['inputs']]+[ROOT/'data/Sanctuary/Balance/item-finishing-catalogue.json']
 decision=ROOT/'tools/reference/item-superior-table-decision.json'
 assert evidence.load(decision)['status']=='user-approved-sanctuary-adaptation'
 files.append(decision)
 quality=evidence.load(ROOT/'tools/reference/item-quality-source-evidence.json')
 casepath=ROOT/'.local/class-skills/item-quality-source/cases.json'
 assert evidence.sha(casepath)=='9e3879b2e0d97ac824717b26a2059b3fed4c46d602b2d87e764e8a2c265f62dc'
 for group in ('inputs','artifacts'):
  for p,digest in quality[group].items():assert evidence.sha(ROOT/p)==digest,p
  files += [ROOT/p for p in quality[group]]
 files += [ROOT/'tools/reference/item-quality-source-evidence.json',ROOT/'tools/probe_item_quality.py']
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files += [ROOT/'docs'/n for n in ('item-finishing-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += evidence.stage_files(STAGE,WIDE,COMPACT)
 for folder in (STAGE/'failed-compact-1',STAGE/'pre-audit-refresh-verification',ROOT/'.local/class-skills/item-quality-source/pre-type-enum-fix',ROOT/'.local/class-skills/item-finishing-properties-source/pre-durability-writer'):
  files += [p for p in folder.rglob('*')if p.is_file()]
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],
  artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=2,integrationBuild=2,sourceSnapshot=2,
  focusedRun=1,broaderRun=1,compactRun=2,focusedMarkers=398,broaderMarkers=614,compactMarkers=27,
  bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,completeSourceControls=20548,sourceRoutines=14,
  currentEligibilityPairs=5536,numericCompositions=192,numericSourceRoutines=21,currentSuperiorSelections=20,
  currentPhysicalContexts=96,originalItemsPerCohort=288,currentSuperiorRows=8,currentInferiorNames=4,currentTypes=105,
  pythonTests=71,verificationCommands=21,auditFingerprints=audits,protection=protected,
  approvedAdaptation='tools/reference/item-superior-table-decision.json',
  qualityOracleEnumCorrection=dict(caseCount=22656,canonicalCasesUnchanged=True,sha256=evidence.sha(casepath)),
  scope='Complete older inferior/superior eligibility and selection,socket/ethereal/normal routing;approved current table-order numeric grade delivery including complete durability function13;actual qualified current physical graph bindings and original identity/snapshot/property continuation through two reloads.',
  limits=['Current executable parity is not established. Superior property delivery is an approved Sanctuary adaptation because the complete older grade dispatcher returns without delivery.',
   'Source controls declare staffmod,charm/book,socket allocation,drop-unit and operative stat services. Actual current binding requires initialized nonzero item RNG,no-sockets and empty current staffmod labels;unsupported valid contexts refuse before mutation.',
   'Original native item identities persist in world saves;graphs/metadata are explicitly restored by the fixture manifest. Automatic NPC equipment persistence,full factory,materialization and Valkyrie/Shadow actors remain unfinished.',
   'Numeric source harness declares the maximum-durability reader;native binding uses existing operative stat query. Generic player property admission/projection is unchanged.',
   'No installed build/save/config/client change,new learned handler,completed class,deployment or generated art. Effects/audio and icons remain separate identified placeholders.'])
 evidence.write('item-finishing-native-evidence.json',receipt,files,check)
 print('Item finishing:20548/14 source;192/21numeric+20selected;288original graphs/two reloads;398focused/614broader/27compact;71Python/21checks/audits/protection pass.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
