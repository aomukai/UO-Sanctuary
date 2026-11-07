#!/usr/bin/env python3
"""Bind original ordinary/incoming producers and saved contact identities."""
import argparse,json,sys,re
from datetime import datetime
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-original-contact';WIDE=BASE/'npc-original-contact-retained';ISO=BASE/'npc-original-contact-isolate';SHARED=BASE/'npc-original-contact-shared'
BLIND=BASE/'npc-original-contact-blind-isolate'
PARENT='76964bcc4dc37158b606b00f227779a56afbf57f'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-8.json');parent=e.load(ROOT/'tools/reference/npc-original-procs-native-evidence.json');extra=Counter({'npc-original-contact-check':1,'npc-original-contact-reload-check':2})
 for previous,stage,count in [(BASE/'npc-original-procs',STAGE,99),(BASE/'npc-original-procs-retained',WIDE,407)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected({'extra_families':['npc-original-contact']}),12)
 sys.path.insert(0,str(ROOT/'tests'));from check_npc_original_contact import configuration
 e.cohort(SHARED,expected(configuration(shared=True)),255)
 phrase='Original NPC contact:57unfiltered original loadouts admitted by explicit host/profile;ordinary hit/miss/range/lethal,actual proc allocation and single incoming melee/missile phases,pre-HP hooks,own actor/world streams,both saved event profiles and original pending packets through two reloads;default game service/AI/cast remain gated'
 e.cohort(BLIND,Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'blind-world-check':1,'blind-world-reload-check':2,'blind-policy-check':3,'blind-world-live-result':3}),18)
 for stage in [STAGE,WIDE,ISO,SHARED,BLIND]:
  run=6 if stage==ISO else 5 if stage in [WIDE,BLIND] else 3;assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  if stage in [ISO,SHARED,BLIND]:
   snapshot(STAGE/'source-snapshot-8.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
   for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage in [SHARED,BLIND]:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  saved=e.load(stage/'npc-original-contact-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==57 and sum(len(h['Items'])for h in saved['Bodies'])==330
  assert isinstance(saved['Game'],int)and 0<=saved['Game']<1<<64
  for h in saved['Bodies']:
   body=json.loads(h['Body']);r=json.loads(h['Resources']);pending=h['Pending'];impact=pending['Impact']
   assert body['EquipmentContactProfile']=='valkyrie-original-proc-contact-v1' and body['EquipmentDefenseProfile']=='valkyrie-inherited-defense-v1'
   assert h['Actor']==r['Actor']==h['Random']['Actor']==impact['Actor'] and body['Owner']==r['Owner']==saved['Owner']
   assert r['ItemEvents']['Actor']==h['Actor'] and r['ItemEvents']['Owner']==saved['Owner'] and h['Random']['ValkyrieBinding']==body['EquipmentWeapons']['Identity']
   assert pending['SourceOriginalEvents'] and not pending.get('TargetOriginalEvents',False) and impact['Target']==h['Target'] and h['TargetLife']==65000*256
   assert impact['Packet']['ResultFlags']&1 and impact['Packet']['Total256']>0
 for stage in [WIDE,BLIND]:
  observations=re.findall(r'Blind live decision: at=([^,]+),previousDeadline=([^,]+),state=(\{[^\n]+?\}) <s:',(stage/'birth-native-checks.log').read_text())
  assert len(observations)==9
  for index,(at,deadline,state)in enumerate(observations):
   at,deadline=datetime.fromisoformat(at.replace('Z','+00:00')),datetime.fromisoformat(deadline.replace('Z','+00:00'));state=json.loads(state);step=index%3+1
   assert at>=deadline and state['Thinks']==step and state['Draws']==1 and state['Fault']is None
   due=datetime.fromisoformat(state['NextUtc'].replace('Z','+00:00'))
   assert abs((due-at).total_seconds()-({1:.4,2:1,3:.04}[step]))<.03
   assert state['Combat']==(step==3) and state['Goal']is None
 for n in ['content-build-2.log','content-build-3.log','content-build-4.log','integration-build-1.log','integration-build-2.log','integration-build-3.log','integration-build-4.log','integration-build-5.log','integration-build-6.log','integration-build-7.log']:
  assert all(t in(STAGE/n).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'CS0136'in(STAGE/'content-build-1.log').read_text()
 assert 'Ran 71 tests'in(STAGE/'python-tests-I7.log').read_text() and '\nOK\n'in(STAGE/'python-tests-I7.log').read_text()
 commands=e.load(STAGE/'verification-commands-I7.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check-I7.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED,BLIND)if not p.name.startswith('receipt-run-')]+[Path(__file__).resolve(),ROOT/'tests/check_npc_original_contact.py',ROOT/'tests/Sanctuary.Integration/NpcOriginalContactChecks.cs']
 for folder in [STAGE/'failed-content-1',ISO/'failed-run-1',ISO/'accepted-run-2',ISO/'failed-run-3',WIDE/'failed-run-1',WIDE/'failed-run-2',WIDE/'failed-run-3',WIDE/'failed-run-4']:files+=list(folder.iterdir())
 files += [STAGE/'content/Sanctuary.Content.dll',STAGE/'integration/Sanctuary.Integration.dll']
 files+=[ROOT/'docs'/n for n in ['npc-original-contact-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,explicitOriginalProcContact=True,defaultWorldAllocation=False,currentExecutableParity=False,
  sourceSnapshot=8,contentBuild=4,integrationBuild=7,focusedRun=3,retainedRun=5,isolatedRun=6,sharedRun=3,auditRun=3,focusedMarkers=99,retainedMarkers=407,isolatedMarkers=12,sharedMarkers=255,blindIsolationRun=5,blindIsolationMarkers=18,originalLoadouts=57,originalItems=330,outgoingCasesPerBoot=456,rangeCasesPerBoot=57,lethalCasesPerBoot=57,incomingCasesPerBoot=1026,savedPendingContacts=57,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Explicit original-proc contact profile validates supplied original graph/resources/weapon/defense and mandatory world root/Chain child allocation services. Ordinary damage/kill/attempt events use the full saved registry and shared callback consumer;legacy profile policy remains unchanged. All57unfiltered loadouts admit under this explicit host.',
   'NpcItemProcs.RequireIncoming/Received owns actual original incoming callbacks once. Original registry incoming calls defer;shared HP receiver preserves result/hit masks,retained actual NPC attacker and pre-health timing. Flat return remains prepared-producer owned. Qualified original Valkyrie inherited guardian friendship wrapper reuses existing demon guard;other modifier exclusions remain.',
   'Contact retains victim identity before HP for lethal hit/kill/attack callbacks. Optional separate saved source/defender original-event flags preserve old JSON defaults;restoration uses real capability definitions,not fabricated packed layers. Missing host/profile/Chain service refuses before allocation/HP or consuming pending contact.',
   'Per boot57loadouts cover456hit/miss/chance/factory ordinary cases,57range rejects,57lethal death/credit cases,1026qualified incoming contexts,missing-service/friend guards and both original saved profile flags.57real pending original packets/330items survive actual two reloads and release with exact HP. Parent96/404and255sharedplayer/Mage consumers remain.'],
  limits=['Actual event producer wiring is implemented under explicit host services. No default persisted world/game allocator,complete initializer/AI or learned cast is enabled. Fixture game stream continuity is explicitly supplied/saved by the test,not claimed as the production whole-engine allocation chronology.',
   'Incoming fixtures supply qualified post-defense packets to the real HP/return receiver,as the existing incoming-proc controls do. They do not claim full ordinary shield/PvP/avoidance result generation. Current executable initial dispatch/target/NHD/critical/removal,class/client certification and Charged Strike legacy RNG remain open;effects/icons separate.',
   'Content1name collision,Native1administrative Frozen knockback refusal andNative3missing guardian incoming adapter are preserved. Isolate2accepted/archived before lethal/two-profile extension. Only fixture Frozen flag changed for Native1;C3adds explicit-profile shared wrapper recognition and friendly-source refusal for Native3. C2-4andI1-7clean. Broad1/2Blind first-idle assertions are archived;diagnosticBroad3showedThinks0and normal movement. Broad4arm log confirms its seven-second fixture curse expired before the probe armed aboutnine seconds later. I6fixture holds thatsamecurserank/seed from observation start;Controls separately testexpiry,all decision/deadline assertions remain strict. Broad4thenfailed a timer-based equality sample;the old log lacks its actual callback timestamp and cannot distinguish an early decision from a late observation. C4adds only a passive completed-decision observer;I7checks actual decision timestamps,exact three decisions/one draw,400/1000ms idle deadlines and adjacent handoff without forced Think/deadline advancement. Standalone Blind5and retained5pass across3boots;Blind4was a harness-only missing source-file path before native boot. Final native/71Python pass. No deployment.'])
 e.write('npc-original-contact-native-evidence.json',result,files,check)
 print('Original NPC contact:57loadouts/330items,456ordinary+57range+57lethal/1026incoming per boot,57saved pending contacts;99focused/407retained/12isolated/255shared;202handlers,default world service/AI/cast next.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
