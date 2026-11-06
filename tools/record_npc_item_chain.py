#!/usr/bin/env python3
"""Bind original NPC chain roots/children,actual hop allocation and saved state."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
from record_npc_item_spell_flights import expected
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-chain';WIDE=BASE/'npc-item-chain-retained';ISOLATE=BASE/'npc-item-chain-isolate';SHARED=BASE/'npc-item-chain-shared'
PARENT='335860cbbfdc4afb63ea1f39da6377f502592bf3'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json');prior=e.load(ROOT/'tools/reference/npc-item-spell-travel-native-evidence.json');extra=Counter({'npc-item-chain-check':1,'npc-item-chain-reload-check':2})
 for previous,stage,count in [(BASE/'npc-item-spell-travel',STAGE,75),(BASE/'npc-item-spell-travel-retained',WIDE,383)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==prior['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
 e.cohort(ISOLATE,expected({'extra_families':['npc-item-chain']}),12)
 sys.path.insert(0,str(ROOT/'tests'));from check_npc_item_chain_shared import configuration
 e.cohort(SHARED,expected(configuration()),249)
 for stage,run in [(ISOLATE,2),(SHARED,1)]:
  snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
 for stage in [STAGE,WIDE,ISOLATE]:
  s=e.load(stage/'npc-item-chain-fixtures.json');assert s['Generation']==2 and len(s['Held'])==len(s['Flights'])==2 and len(s['Items'])==7 and s['Life']>0
  b=json.loads(s['Body']);binding=b['EquipmentWeapons']['Identity'];assert b['Owner']==s['Owner']and s['Random']['Actor']==s['Actor']and s['Random']['Owner']==s['Owner']and s['Random']['ValkyrieBinding']==binding
  resources=json.loads(s['Resources']);assert resources['Actor']==s['Actor']and resources['Owner']==s['Owner']and resources['Valkyrie']['RegenerationBase']==300
  assert {h['Cast']for h in s['Held']}=={f['Cast']for f in s['Flights']}and {h['Ordinal']for h in s['Held']}=={0,1}
  assert len({t['Actor']for h in s['Held']for t in [h['First'],h['Second']]})==4
  for f in s['Flights']:
   n=f['OriginalNpc'];p=n['Source']['Packet'];h=next(h for h in s['Held']if h['Cast']==f['Cast']);assert n['Ordinal']==h['Ordinal']and f['Frame']==(2 if n['Ordinal']==0 else 1)
   assert f['Owner']==s['Owner']and n['Source']['Actor']==s['Actor']and n['Source']['Binding']==binding and p['Skill']==53 and p['Rank']==f['Rank']==3 and p['Emissions']==5
   assert f['Remaining']==5-n['Ordinal']and f['NpcCaster']==f['NpcSeed']==0 and 'PlayerPacket'not in f and 'StrikeContext'not in f
   assert n['Seed']==(666<<32)|(3000+n['Ordinal'])and len(n['Steps']['Cells'])>=1 and n.get('Fault')is None
   if n['Ordinal']==1:assert len(f['Delays'])==1 and f['Delays'][0]['Target']==h['First']['Actor']and h['First']['Life']<65000*256
   else:assert f['Delays']==[]and h['First']['Life']==65000*256
   assert h['Second']['Life']==65000*256
  assert all(json.loads(item)['Equipment']['Actor']==s['Actor']for item in s['Items'])
  assert(stage/'birth-native-checks.log').read_text().count('Original NPC Chain:own source/rank/packet and shared cell travel;natural lone/five-hop/failure contacts,per-hop allocator-before-HP,moved-caster origin,exact child/actor RNG,atomic restore/missing-service/offline guards;original partial root+child/items/resources/wounds through two reloads;proc dispatch/allocator provenance/shield targets/AI/cast remain gated')==3
 for name in ['content-build-2.log','integration-build-2.log']:assert all(t in(STAGE/name).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'error CS0136'in(STAGE/'failed-build-1/content-build-1.log').read_text()
 assert 'error CS1513'in(STAGE/'failed-build-2/integration-build-1.log').read_text()
 pre=(ISOLATE/'failed-pre-server-1/native-run-1.log').read_text();assert 'FileNotFoundError'in pre and 'Sanctuary.Integration.dll'in pre
 before=e.load(STAGE/'failed-build-2/source-snapshot-2.json')['inputs'];after=e.load(STAGE/'source-snapshot-3.json')['inputs'];assert before['copiedData']==after['copiedData']and before['projectInputs']==after['projectInputs']
 assert {p for p,h in before['sourceInputs'].items()if after['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/NpcItemChainChecks.cs'}
 routepath=ROOT/'tools/reference/npc-chain-routes-source-evidence.json';route=e.load(routepath);assert route['cases']==11655 and len(route['routineHashes'])==2
 for group in ['inputs','currentTable','artifacts']:
  for p,h in route[group].items():assert e.sha(ROOT/p)==h,p
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
 files=e.stage_files(STAGE,WIDE,ISOLATE,SHARED)+[Path(__file__).resolve(),ROOT/'tests/check_npc_item_chain.py',ROOT/'tests/check_npc_item_chain_shared.py',routepath]
 for stage,subs in [(STAGE,['failed-build-1','failed-build-2']),(ISOLATE,['failed-pre-server-1'])]:
  for sub in subs:files.extend(p for p in(stage/sub).rglob('*')if p.is_file())
 files+=[ROOT/'docs'/n for n in ['npc-item-chain-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,originalProcDispatch=False,currentExecutableParity=False,
  sourceSnapshot=3,contentBuild=2,integrationBuild=2,focusedRun=1,retainedRun=1,isolatedRun=2,sharedRun=1,auditRun=1,focusedMarkers=75,retainedMarkers=383,isolatedMarkers=12,sharedMarkers=249,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,savedOriginalChains=2,originalItems=7,sourceRouteCases=11655,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Original NPC roots and retained-rank children use own immutable item packets/pierce/seed/path/ordinal,shared prepared chain cell motion and ordered NPC HP with its intrinsic actor stream. Legacy player/Mage/Lightning Strike JSON omits the new context;249shared markers preserve their natural/save/input consumers.','Natural lone/five-hop/failure cases verify exact HP/RNG,per-attempt allocator requests before HP,moved-caster missile origin and retained budget. Missing service refuses before source mutation;invalid restore and copied snapshots retain ownership.','Original partial root and already-created child retain original items/resources/wounds/path/seeds/leases through two real reloads and finish their remaining natural contacts without replay. All380parent retained markers remain.'],
  limits=['Allocator callback is an explicit host service at each actual attempt,not a guessed/eager seed list. Its world/game-stream provenance still needs the full item-proc/world binding. Callback side effects belong to the provider;missing/unsupported runtime branches suspend harmlessly rather than fabricating a result.','Current target traversal,NHD identity,return-fire/defense/critical callback order,source removal,nonzero NPC shields and players remain unverified or refused. No original proc registration/history or synchronous item skill frame is enabled.','Compiler-name and fixture-initializer failures are archived. Premature driver1 failed missing-DLL validation before any native server started. AI/A1/startup/initializer/world seed/learned placement/payment/caps/replacement/input/client and final assets remain separate;no class completion or deployment.'])
 e.write('npc-item-chain-native-evidence.json',result,files,check)
 print('Original NPC Chain:75focused/383retained/12isolated/249shared markers,two reloads;natural per-hop allocation and original saved root/child;202handlers,proc/AI/cast remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
