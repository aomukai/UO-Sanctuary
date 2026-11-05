#!/usr/bin/env python3
"""Bind complete staffmod source and current original-item persistence scopes."""
import argparse
from pathlib import Path
from record_item_finishing import expected as previous_expected
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT;STAGE=ROOT/'.local/class-skills/item-staffmods';WIDE=ROOT/'.local/class-skills/item-staffmods-broader';COMPACT=ROOT/'.local/class-skills/item-staffmods-compact'
PARENT='649a7acf783777fed2b8a9607480a395762c7d98'
CHANGED={'src/Sanctuary.Content/SourceItemStaffmods.cs','src/Sanctuary.Content/CurrentItemStaffmods.cs','src/Sanctuary.Content/CurrentItemFinishing.cs',
 'tests/Sanctuary.Integration/ItemStaffmodChecks.cs','tests/Sanctuary.Integration/ItemFinishingChecks.cs'}
def expected(broader=False,compact=False):
 r=previous_expected(broader,compact);r['item-staffmods-check']+=1;r['item-staffmods-reload-check']+=2;return r
def fields(rows):return {r['Packed']:r['Value']for r in rows if r['Value']}

def build(check=False):
 born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-3.json')
 old=evidence.load(ROOT/'.local/class-skills/item-finishing/pre-build-2.json')['inputs'];now=evidence.load(STAGE/'pre-build-3.json')['inputs']
 changed={p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
 assert changed==CHANGED,changed
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=evidence.load(ROOT/'tools/reference/item-finishing-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 cases=evidence.load(ROOT/'.local/class-skills/item-staffmods-source/cases.json')
 assert len(cases['inputs'])==4779 and len(cases['nativeInputs'])==414 and len(cases['adaptationInputs'])==960
 assert len(set(cases['nativeCodes']))==138
 modulo={((t[1]&0xFFFFFFFF)%100)for out in cases['outputs']for t in out[3]if t[0]==0}
 assert modulo==set(range(100))
 assert sum(any(t==[3,0]for t in out[3])for out in cases['outputs'])==255
 for stage,wide,compact,count in ((STAGE,False,False,401),(WIDE,True,False,617),(COMPACT,False,True,30)):
  log=evidence.cohort(stage,expected(wide,compact),count)
  assert log.count('Item staffmods:4779complete source controls/6routines;960approved tier controls;414current contexts/138bases on414original graphs')==3
  saved=evidence.load(stage/'item-staffmods-fixtures.json')
  assert saved['Generation']==2 and len(saved['Items'])==len({r['Item']for r in saved['Items']})==414
  assert {r['Case']for r in saved['Items']}==set(range(414))
  for r in saved['Items']:
   a=cases['nativeInputs'][r['Case']];out=cases['nativeOutputs'][r['Case']];g=r['Graph']
   assert g['Seed']==int(out[0])and g['HasProperties']==out[1]and not g['Ethereal']
   assert fields(g['ItemBase'])=={73<<16:100}
   properties={(107<<16)|skill:value for skill,value in out[2]}
   if a[9]==0:properties[7<<16]=256
   assert fields(g['Properties'])==properties
  for fixture in ('item-finishing','item-quality','item-initialization','npc-retained-target','npc-incoming-lethal','npc-incoming-proc'):
   assert evidence.load(stage/(fixture+'-fixtures.json'))['Generation']==2
  assert len(evidence.load(stage/'item-finishing-fixtures.json')['Items'])==288
  inv=evidence.load(stage/'birth-inputs.json');assert inv['artifacts']==born['artifacts']and inv['copiedData']==born['copiedData']
  for path,digest in inv['sourceInputs'].items():assert evidence.sha(ROOT/path)==digest,path
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-3.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1061,'first-loop-affix-audit.json':21}
 evidence.verification(STAGE);assert len(evidence.load(STAGE/'verification-commands.json'))==22;protected=evidence.protection()
 sourcepath=ROOT/'tools/reference/item-staffmods-source-evidence.json';source=evidence.load(sourcepath)
 assert source['cases']==4779 and source['nativeContexts']==414 and len(source['completeRoutineHashes'])==6 and not source['currentExecutableParity']
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tools/probe_item_staffmods.py',ROOT/'tests/check_item_staffmods.py',sourcepath]+[ROOT/p for p in CHANGED]
 for group in ('inputs','artifacts'):
  for p,digest in source[group].items():assert evidence.sha(ROOT/p)==digest,p
  files += [ROOT/p for p in source[group]]
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files += [ROOT/'docs'/n for n in ('item-staffmods-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 decision=ROOT/'tools/reference/item-staffmods-tier-decision.json';assert evidence.load(decision)['status']=='user-approved-sanctuary-adaptation'
 assert source['candidateReplacementOnly']and source['adaptationCases']==960
 files.append(decision)
 files += evidence.stage_files(STAGE,WIDE,COMPACT)
 for folder in (STAGE/'pre-layer-review',ROOT/'.local/class-skills/item-staffmods-source/compiler-attempt-1',ROOT/'.local/class-skills/item-staffmods-legacy-before-tier'):
  files += [p for p in folder.rglob('*')if p.is_file()]
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],
  contentBuild=3,integrationBuild=3,sourceSnapshot=3,focusedRun=2,broaderRun=2,compactRun=2,focusedMarkers=401,broaderMarkers=617,compactMarkers=30,
  bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,completeSourceControls=4779,sourceRoutines=6,currentStaffmodBases=138,approvedTierControls=960,approvedAdaptation='tools/reference/item-staffmods-tier-decision.json',
  originalItemsPerCohort=414,retainedFinishingItemsPerCohort=288,randomPercentValuesCovered=100,controlledAllocationFailureCases=255,
  pythonTests=71,verificationCommands=22,auditFingerprints=audits,protection=protected,
  scope='Complete older staffmod dispatcher/modern/legacy generators reuse current raw429skill/type metadata,approved required-level groups for eight classes and item graph/RNG. All138staffmod bases have414native contexts preserving raw skill keys,overwrite/list behavior and original identity/snapshots/property continuation through two reloads;actual normal finishing invokes the binding.',
  limits=['Current executable parity is not established. Class metadata,skill-row/type and list allocation/post/write services are explicit;controlled allocation failures are not native allocation-failure proofs.',
   'Current required-level tiers are established by the owned guide. Ascending IDs,full-tier modulo selection and retained roll/retry rules are user-approved Sanctuary adaptation;exact current executable ordering/RNG parity remains unproved.',
   'Original native identities persist in world saves;graphs are explicitly restored by the fixture manifest. Full factory,special normal items,socket materialization,automatic NPC equipment persistence/skill delivery and Valkyrie/Shadow actors remain required.',
   'Existing valid modifiers are not filtered from candidate pools. Extreme signed overflow/uninitialized looping domains remain outside actual binding. No generic player property admission/projection change.',
   'No installed build/save/config/client change,new learned handler,completed class,deployment or generated art. Effects/audio and icons remain separate identified placeholders.'])
 evidence.write('item-staffmods-native-evidence.json',receipt,files,check)
 print('Staffmods:4779source controls/6routines;960approved tier controls;414original graphs/138bases/two reloads;401focused/617broader/30compact;71Python/22checks/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
