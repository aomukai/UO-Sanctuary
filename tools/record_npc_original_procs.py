#!/usr/bin/env python3
"""Bind actual original graph proc callbacks and saved natural spell impacts."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-original-procs';WIDE=BASE/'npc-original-procs-retained';ISO=BASE/'npc-original-procs-isolate';SHARED=BASE/'npc-original-procs-shared'
PARENT='2cdba62081d1b1faa3b3814a6518e1256a27d00e'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json');parent=e.load(ROOT/'tools/reference/npc-original-events-native-evidence.json');extra=Counter({'npc-original-procs-check':1,'npc-original-procs-reload-check':2})
 for previous,stage,count in [(BASE/'npc-original-events',STAGE,96),(BASE/'npc-original-events-retained',WIDE,404)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected({'extra_families':['npc-original-procs']}),12)
 sys.path.insert(0,str(ROOT/'tests'));from check_npc_original_procs import configuration
 e.cohort(SHARED,expected(configuration(shared=True)),255)
 source=e.load(ROOT/'tools/reference/npc-original-procs-source-evidence.json');assert source['contexts']==1536 and source['loadouts']==27 and len(source['sourceRoutines'])==5
 for group in ['inputs','artifacts']:
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 cases=e.load(BASE/'npc-original-procs-source/cases.json');assert len(cases)==1536 and len({c['Case']for c in cases})==27
 casts=[t for c in cases for result in c['Results']for t in result[2]if t[0]==3];assert {t[2]for t in casts}=={36,38,44,48,53}
 phrase='Original NPC procs:1536source callback contexts across57unfiltered loadouts;actual full graph order/chance/rank/flags and actor/allocation streams;five proc-created saved spell bodies resume exact natural HP/RNG through two reloads;ordinary producers/game stream remain separate'
 for stage in [STAGE,WIDE,ISO,SHARED]:
  run=2 if stage==ISO else 1;assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  if stage in [ISO,SHARED]:
   snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
   for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  saved=e.load(stage/'npc-original-procs-fixtures.json');assert saved['Generation']==2 and len(saved['Held'])==5 and len(saved['Spells'])==135 and len(saved['Chains'])==1
  assert {h['Skill']for h in saved['Held']}=={36,38,44,48,53} and len({h['Actor']for h in saved['Held']})==5 and sum(len(h['Items'])for h in saved['Held'])==24
  for h in saved['Held']:
   b=json.loads(h['Body']);r=json.loads(h['Resources']);assert b['Owner']==r['Owner']==saved['Owner'] and h['Actor']==r['Actor']==h['Random']['Actor']
   assert h['Random']['ValkyrieBinding']==b['EquipmentWeapons']['Identity'] and r['ItemEvents']['Actor']==h['Actor'] and h['TargetLife']==65000*256
   assert len(h['Ids'])==({36:1,38:6,44:64,48:64,53:1}[h['Skill']])
   if h['Skill']==53:flights=saved['Chains'];assert all(f['OriginalNpc']['Source']['Actor']==h['Actor']and f['Frame']==1 and f['Cast']in h['Ids']for f in flights)
   else:
    flights=[f for f in saved['Spells']if f['Npc']['Actor']==h['Actor']];assert len(flights)==len(h['Ids']) and all(f['Frame']==1 and f['Id']in h['Ids']and f['Npc']['Packet']['Skill']==h['Skill']for f in flights)
 for n in ['content-build-1.log','content-build-2.log','content-build-3.log','integration-build-1.log','integration-build-2.log','integration-build-3.log']:assert all(t in(STAGE/n).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text() and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]+[Path(__file__).resolve(),ROOT/'tests/check_npc_original_procs.py',ROOT/'tests/Sanctuary.Integration/NpcOriginalProcChecks.cs',ROOT/'tools/probe_npc_original_procs.py',ROOT/'tools/reference/npc-original-procs-source-evidence.json']
 for name in ['failed-run-1','failed-run-2']:files+=list((BASE/'npc-original-procs-source'/name).iterdir())
 files+=list((ISO/'accepted-run-1').iterdir())
 files+=[ROOT/'docs'/n for n in ['npc-original-procs-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,ordinaryOriginalProcDispatch=False,currentExecutableParity=False,
  sourceSnapshot=3,contentBuild=3,integrationBuild=3,focusedRun=1,retainedRun=1,isolatedRun=2,sharedRun=1,auditRun=1,focusedMarkers=96,retainedMarkers=404,isolatedMarkers=12,sharedMarkers=255,callbackContextsPerBoot=1536,originalLoadouts=57,procLoadouts=27,savedPartialProjectiles=136,savedOriginalItems=24,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['NpcOriginalItemProcs.Require/Dispatch validates actual original actor/item/rank/world graph and drives saved SourceUnitEvents registration order through existing ItemProcCallbacks20/21 and protected five-spell wrappers. Chance uses actual full packed-layer graph query and intrinsic actor RNG;explicit allocation service owns separate state.',
   'Shared NpcItemEventEffects retains old carrier effect bodies. Full original dispatch publishes a per-actor numeric frame only during its synchronous event;existing NPC scalar consumers read that actual graph through the reused Valkyrie numeric map. Finally restores nesting/cleanup;ordinary contact admission is unchanged.',
   '1536complete source callback contexts over27unfiltered proc-bearing loadouts compare per-node function/phase/key/result/before/after actor streams and actual world allocation skill/rank/count. All57loadouts validate and real non-proc callbacks read their own numeric graph. Targetless failed damage gates need no path or RNG;missing path after success retains consumed source chance draws and restores state.',
   '136actual proc-created saved partial projectiles from five different original actors/24original items resume natural exact HP/projectile/actor RNG through two actual reloads. All93/401parent markers and255sharedplayer/Mage/LightningStrike inputs remain.'],
  limits=['This is an actual full-graph callback consumer with mandatory world allocation/path services. Ordinary contact and incoming producer wiring,its saved packet registry policy and a persisted world/game allocation stream remain next. The27proc-bearing ordinary contact loadouts still refuse;no learned Valkyrie is enabled.',
   'Flat function6 remains owned by the prepared ordinary flat-return producer;dispatch does not replay it. Death/levelup/reanimation and unsupported skill metadata refuse. Provider/observer side effects are not transactional. Current executable chronology/target/NHD/critical/removal/shields/PvP/AI/initializer/class/client and Charged Strike legacy RNG remain open.',
   'Source harness run1 searched full64bit state instead of returned32bit roll;run2 redeclared per-case fixture damage. Both failed harnesses/logs are preserved;corrected run3 compiles unchanged complete callbacks and seed routines with ASan/UBSan clean. AllC1-3/I1-3builds clean;prior C2/I2isolate1 accepted and archived before C3scalar-scope addition. Final native runs and71Python tests pass;no native failures or deployment.'])
 e.write('npc-original-procs-native-evidence.json',result,files,check)
 print('Original NPC procs:1536source contexts/27proc loadouts,136saved live projectiles;96focused/404retained/12isolated/255shared through two reloads;202handlers,ordinary producer/game service next.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
