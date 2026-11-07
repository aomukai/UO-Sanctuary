#!/usr/bin/env python3
"""Bind shared world allocation provenance,persistent streams and existing consumers."""
import argparse,json,sys
from pathlib import Path
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-world-allocation';WIDE=BASE/'npc-world-allocation-retained';ISO=BASE/'npc-world-allocation-isolate';SHARED=BASE/'npc-world-allocation-shared'
PARENT='7b10b707fe8274ddeec01ffa2fab81d4be94428f'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json');parent=e.load(ROOT/'tools/reference/npc-original-contact-native-evidence.json');extra=Counter({'npc-world-allocation-check':1,'npc-world-allocation-reload-check':2})
 for previous,stage,count in [(BASE/'npc-original-contact',STAGE,102),(BASE/'npc-original-contact-retained',WIDE,410)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected({'extra_families':['npc-world-allocation']}),12)
 sys.path.insert(0,str(ROOT/'tests'));from check_npc_world_allocation import configuration
 e.cohort(SHARED,expected(configuration(shared=True)),255)
 phrase='World allocation:960complete mixed source contexts;one persistent world stream,independent actor/item/property/missile streams,actual equipped item/five root spells/natural path child beforeHP,range/domain guards,lazy host entropy once,exact world/item/child/HP continuation through two reloads;no historical global chronology or learned initializer/AI claim'
 for stage in [STAGE,WIDE,ISO,SHARED]:
  run=2 if stage==ISO else 1;assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  if stage in [ISO,SHARED]:
   snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
   for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  s=e.load(stage/'npc-world-allocation-fixtures.json');assert s['Generation']==2
  world=s['World'];assert world['Version']==0 and world['Profile']=='sanctuary-world-explicit-seed-v1'and 0<=world['InitialLow']<1<<32 and 0<world['Seed']<1<<64
  item=json.loads(s['ItemState']);assert item['Request']['Code']=='cap'and item['Request']['Quality']==2 and item['Equipment']['Actor']==s['EquippedBody']and item['Equipment']['Owner']==s['Owner']
  assert item['Allocation']is not None and item['Metadata']['Flags']==0x82010
  body=json.loads(s['BodyState']);assert body['Owner']==s['Owner'] and s['Actor']['Actor']==s['Body']
  flight=s['Flight'];assert flight['Owner']==s['Owner']and flight['Remaining']==2 and flight['OriginalNpc']['Ordinal']==3 and flight['OriginalNpc']['Source']['Actor']==s['Body']
  assert len(s['Targets'])==len(s['Lives'])==5 and any(life<65000*256 for life in s['Lives'])
 for n in ['content-build-1.log','integration-build-1.log','integration-build-2.log']:assert all(x in(STAGE/n).read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Range refusal allocated a world unit'in(ISO/'failed-native-1/birth-native-checks.log').read_text()
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 sourcepath=ROOT/'tools/reference/npc-world-allocation-source-evidence.json';source=e.load(sourcepath)
 assert source['contexts']==960 and source['entropyContexts']==80 and len(source['completeRoutineHashes'])==12 and(source['missiles'],source['items'],source['failedAllocations'])==(1080,480,1560)
 for group in ['inputs','artifacts']:
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]+list((ISO/'failed-native-1').iterdir())
 files += [Path(__file__).resolve(),ROOT/'tests/check_npc_world_allocation.py',sourcepath,ROOT/'tools/probe_npc_world_allocation.py',STAGE/'content/Sanctuary.Content.dll',STAGE/'integration/Sanctuary.Integration.dll']
 files += [ROOT/'docs'/n for n in ['npc-world-allocation-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,defaultWorldAllocation=True,targetlessFirstPath=False,currentExecutableParity=False,sourceSnapshot=3,contentBuild=1,integrationBuild=2,focusedRun=1,retainedRun=1,isolatedRun=2,sharedRun=1,auditRun=1,focusedMarkers=102,retainedMarkers=410,isolatedMarkers=12,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=7,sourceContexts=960,sourceEntropyContexts=80,sourceMissiles=1080,sourceItems=480,sourceRefusals=1560,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=['One persisted world allocation domain supplies original item/equipment creation and installed root/Chain child factories.32-bit host bootstrap is lazy and identified,then existing source InitLow/LCG is shared. Per-actor,unit,property and missile damage/path streams stay distinct. No future child draws are taken.', 'Main-thread immutable save capture and strict atomic restore preserve current stream and bootstrap low word through actual reloads. Existing/unused worlds do not allocate while loading. Source/UTC/rank/factory checks precede consumption;shared emitter origin/room/range refusal remains before allocation.', 'Native960mixed-source controls,actual equipped cap,five real proc-root spells,atomic malformed state,range/no-flight/no-RNG checks and real path/HP child allocation prove composition. A partially moved child,original item/body/actor stream and target wounds survive both reloads and continue from the exact next world draw.102/410totals retain all99/407parent consumers;255shared player/Mage controls remain.'],limits=['The Sanctuary host generates an initial32-bit seed. This explicit backend bootstrap/persistent-world domain does not establish D2R3.3.93847 game/map seed provenance,whole-engine allocation order or binary heap/room/event behavior. The80legacy entropy helpers are supplementary and do not run the full game creation function.', 'Path tests advance the real shared ChainLightning.Tick at retained future frame deadlines;they prove real trace/target/HP/allocation composition,not wall-clock input cadence. Full summon initializer,AI/A1/learned casting,targetless first-path adapter,current binary target/NHD/critical/removal,nonzero shield/PvP and Charged Strike legacy RNG remain open.', 'Isolated1failed a fixture return-value assumption:shared direct execution returns1 even when the distant factory refuses. Source/Integration1DLL/log/input remain archived. I2requires that established return,unchanged world RNG and no flight;production did not change. No deployment or new artwork/UI slot.'])
 e.write('npc-world-allocation-native-evidence.json',result,files,check)
 print('NPC world allocation:102focused/410retained/12isolated/255shared,real saved item/root/child/world streams;source960mixed/80entropy,71Python/sevenchecks;202handlers,no class complete/deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
