#!/usr/bin/env python3
"""Bind full original graph registration history to source and native reloads."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-original-events';WIDE=BASE/'npc-original-events-retained';ISO=BASE/'npc-original-events-isolate';SHARED=BASE/'npc-original-events-shared'
PARENT='b8ca154680ea02e87e88efb57bd9895351c32a74'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json');parent=e.load(ROOT/'tools/reference/npc-item-wrapper-native-evidence.json');extra=Counter({'npc-original-events-check':1,'npc-original-events-reload-check':2})
 for previous,stage,count in [(BASE/'npc-item-wrapper',STAGE,93),(BASE/'npc-item-wrapper-retained',WIDE,401)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected({'extra_families':['npc-original-events']}),12)
 sys.path.insert(0,str(ROOT/'tests'));from check_npc_original_events import configuration
 e.cohort(SHARED,expected(configuration(shared=True)),255)
 source=e.load(ROOT/'tools/reference/npc-original-events-source-evidence.json');assert source['contexts']==57 and source['originalItems']==330 and source['cycles']==3 and source['stages']==2892 and source['eventStatChanges']==593 and len(source['eventRoutineHashes'])==8
 for group in ['inputs','artifacts']:
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 cases=e.load(BASE/'npc-original-events-source/cases.json');assert len(cases)==57 and sum(c['Items']for c in cases)==330
 from review_weapon_event_order import function
 ancestor=e.load(ROOT/'tools/reference/generated-npc-stats-source-evidence.json');numeric=(BASE/'npc-original-events-source/numeric.cpp').read_text()
 for name,h in ancestor['inheritedRoutineHashes'].items():
  import hashlib
  assert hashlib.sha256(function(numeric,name).encode()).hexdigest()==h,name
 phrase='Original NPC item events:57unfiltered loadouts/330original items;complete source registration head/order/IDs through3cycles,all removals/re-additions,percentage/difficulty/cache rebuilds and two actual reloads;all14phase traversal,nested/throwing callbacks,corrupt-history/alias guards;no proc carrier copies or automatic proc admission'
 for stage in [STAGE,WIDE,ISO,SHARED]:
  assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
  if stage in [ISO,SHARED]:
   snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
   for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  saved=e.load(stage/'npc-original-events-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==57
  for c,h in zip(cases,saved['Bodies']):
   assert len(h['Items'])==c['Items'];body=json.loads(h['Body']);r=json.loads(h['Resources']);v=r['ItemEvents'];events=v['Events']
   assert h['Actor']==r['Actor']==v['Actor'] and body['Owner']==r['Owner']==v['Owner']==saved['Owner'] and body['Difficulty']==r['Valkyrie']['Difficulty']==0
   assert r['ActiveItems']==v['ActiveItems'] and len(v['Originals'])==64 and events['HasOwner']
   observed=[events['NextId'],[[n[k]for k in ['Id','Event','Guid1','Guid2','Function','Queue','Key','Flags']]for n in events['Entries']]]
   assert observed==c['States'][-1]
   assert h['Random']['Actor']==h['Actor'] and h['Random']['ValkyrieBinding']==body['EquipmentWeapons']['Identity']
 for n in ['content-build-1.log','content-build-2.log','integration-build-1.log']:assert all(t in(STAGE/n).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text() and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]+[Path(__file__).resolve(),ROOT/'tests/check_npc_original_events.py',ROOT/'tests/Sanctuary.Integration/NpcOriginalEventChecks.cs',ROOT/'tools/probe_npc_original_events.py',ROOT/'tools/reference/npc-original-events-source-evidence.json']
 failed=BASE/'npc-original-events-source/failed-run-1';files+=list(failed.iterdir())+list((STAGE/'receipt-failed-1').iterdir())
 files+=[ROOT/'docs'/n for n in ['npc-original-events-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,originalProcDispatch=False,currentExecutableParity=False,
  sourceSnapshot=3,contentBuild=2,integrationBuild=1,focusedRun=1,retainedRun=1,isolatedRun=1,sharedRun=1,auditRun=1,focusedMarkers=93,retainedMarkers=401,isolatedMarkers=12,sharedMarkers=255,originalLoadouts=57,originalItems=330,sourceStages=2892,eventStatChanges=593,eventSourceRoutines=8,numericSourceRoutines=30,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Optional event registrations bind to the actual full GeneratedNpcStatGraph before original item construction/attachment replay. The shared source ItemEventBindings/SourceUnitEvents capture actual head/order/NextId and full packed-layer identity;no carrier copies/filtering or sorted-stat reconstruction.',
   'Saved optional GeneratedNpcEventState binds original actor/owner,full original item identity/payload digest and logical active-item order. Reconstruction proves live membership then restores saved order/IDs. Resource transactions,percentage and difficulty/cache rebuilds retain history;old saves omit the field.',
   '57unfiltered original loadouts/330items match all2892complete numeric-to-event source stages over3cycles and actual two reloads. Native actors follow their owner facet (Normal),with explicit Normal/Nightmare basis transitions;source corpus difficulty labels identify original fixture recipes,not a native difficulty override. All14phase traversal,ignored callback returns,nested/throwing callback cleanup,active capture/rebuild refusals,corrupt-history and snapshot-alias guards pass. All90/398parent markers retained.'],
  limits=['The full original graph registration/history and mandatory synchronous dispatch service are implemented. Callback effect bodies/ordinary contact producers still need binding to that actual graph;27proc-bearing original contact loadouts and learned Valkyrie remain refused.',
   'Actual world/game allocation stream remains mandatory and separate from intrinsic actor critical/proc RNG. Innate event-base initialization needs explicit history;rebuilding a dispatching graph refuses instead of replacing its active list. Non-event callback effects keep their existing separate services.',
   'Source probe run1 exposed retained fixture insert capacity bookkeeping after repeated source shrink operations. Failed artifacts are preserved;only the external insert service was corrected to maintain logical capacity. Complete source routines are unchanged;run3 ASan/UBSan clean. Initial Python run ran before fresh audit publication and rejected stale fingerprints;the preserved failure passes after fresh unchanged-decision audit publication. Initial receipt draft incorrectly equated source recipe difficulty with current native facet;the failed draft/log are preserved. The check now requires matching body/resource Normal facet,following the approved area rule. C1/C2/I1 all clean,no native failures.',
   'Current executable chronology/dispatch/target/NHD/critical/removal,nonzero shields/PvP,AI/initializer/learned class/client and Charged Strike separate RNG remain open. No deployment;effects/icons tracked separately.'])
 e.write('npc-original-events-native-evidence.json',result,files,check)
 print('Original NPC event history:57loadouts/330items,2892source stages;93focused/401retained/12isolated/255shared through two reloads;202handlers,actual proc/cast remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
