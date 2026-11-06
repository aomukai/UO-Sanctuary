#!/usr/bin/env python3
"""Bind original NPC spell flights to saved actors and shared spell regressions."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-spell-flights';WIDE=BASE/'npc-item-spell-flights-retained';ISOLATE=BASE/'npc-item-spell-flights-isolate';PLAYER=BASE/'npc-item-spell-flights-player';FIRE=BASE/'npc-item-spell-flights-fire'
PARENT='2d4e843a2f087b2877773cbb44449f7f67828069'
def expected(c):
 out=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 for family in c.get('extra_families',()):out[family+'-check']+=1;out[family+'-reload-check']+=2
 for command in c.get('extra_checks',()):out[command]+=3
 for command,result in c.get('extra_wait_results',{}).items():
  assert out[command]>0
  for name in [result]if isinstance(result,str)else result:out[name]+=out[command]
 for name in c.get('extra_live_commands',()):
  out[name+'-result']+=3
  if name in c.get('checked_live_commands',()):out[name+'-check']+=3
 for name in c.get('extra_target_input_commands',()):out[name+'-check']+=3;out[name+'-result']+=3
 return out
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json');prior=e.load(ROOT/'tools/reference/npc-item-spell-damage-native-evidence.json');extra=Counter({'npc-item-spell-flights-check':1,'npc-item-spell-flights-reload-check':2})
 for previous,stage,count,run in [(BASE/'npc-item-spell-damage',STAGE,69,2),(BASE/'npc-item-spell-damage-retained',WIDE,377,1)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==prior['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
 e.cohort(ISOLATE,expected({'extra_families':['npc-item-spell-flights']}),12)
 sys.path.insert(0,str(ROOT/'tests'));from check_npc_item_spell_flights_shared import configuration
 for stage,config,count in [(PLAYER,configuration(False),231),(FIRE,configuration(True),40)]:e.cohort(stage,expected(config),count)
 for stage in [ISOLATE,PLAYER,FIRE]:
  snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{2 if stage==ISOLATE else 1}.log').read_text()
 for stage in [STAGE,WIDE,ISOLATE]:
  s=e.load(stage/'npc-item-spell-flights-fixtures.json');assert s['Generation']==2 and len(s['Flights'])==134 and s['TargetLife']>0 and s['Life']>0
  b=json.loads(s['Body']);assert b['Owner']==s['Owner']and s['Random']['Actor']==s['Actor']and s['Random']['Owner']==s['Owner']
  binding=b['EquipmentWeapons']['Identity'];assert s['Random']['ValkyrieBinding']==binding
  resources=json.loads(s['Resources']);assert resources['Actor']==s['Actor']and resources['Owner']==s['Owner']and resources['Valkyrie']['RegenerationBase']==300
  counts=Counter(f['Npc']['Packet']['Skill']for f in s['Flights']);assert counts=={36:1,38:5,44:64,48:64}
  assert len({f['Id']for f in s['Flights']})==134
  for f in s['Flights']:
   assert f['Owner']==s['Owner']and f['Npc']['Actor']==s['Actor']and f['Npc']['Binding']==binding and f['Rank']==f['Npc']['Packet']['Rank']==3 and f['Frame']==0
   assert f['Modifiers']==dict(Potion=0,FireMastery=0,ColdMastery=0,LightningMastery=0,PoisonMastery=0)
   if f['Npc']['Packet']['Skill']==38:assert len(f['Charged']['Path'])==39 and f['Charged']['Next']==1
   if f['Npc']['Packet']['Skill']in [44,48]:assert f['LastCollision']==s['Actor']and f['RadialIndex']in range(64)
  assert all(json.loads(item)['Equipment']['Actor']==s['Actor']for item in s['Items'])
  assert(stage/'birth-native-checks.log').read_text().count('Original NPC spell flights:Fire Bolt/Charged Bolt/Nova/Frost Nova share checked motion,explicit child seeds and own packets;16HP/critical/immunity controls,emitter cold ownership,atomic batch/path-copy/offline/park/natural hit;134actual saved flights and original items/resources/wounds/RNG through two reloads;Chain/proc callbacks/shield targets/AI/learned cast remain gated')==3
 for stage in [STAGE,ISOLATE]:
  failed=stage/'failed-native-1';assert 'No independent fixture corridor near 5690,3800'in(failed/'birth-native-checks.log').read_text()
  assert e.load(failed/'birth-inputs.json')['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 before=e.load(STAGE/'failed-native-1/source-snapshot-2.json')['inputs'];after=e.load(STAGE/'source-snapshot-3.json')['inputs'];assert before['copiedData']==after['copiedData']and before['projectInputs']==after['projectInputs']
 assert {p for p,h in before['sourceInputs'].items()if after['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/NpcItemSpellFlightChecks.cs'}
 for name in ['content-build-1.log','integration-build-2.log']:assert all(t in(STAGE/name).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
 files=e.stage_files(STAGE,WIDE,ISOLATE,PLAYER,FIRE)+[Path(__file__).resolve(),ROOT/'tests/check_npc_item_spell_flights.py',ROOT/'tests/check_npc_item_spell_flights_shared.py']
 for stage in [STAGE,ISOLATE]:files.extend(p for p in(stage/'failed-native-1').rglob('*')if p.is_file())
 files+=[ROOT/'docs'/n for n in ['npc-item-spell-flights-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,originalProcDispatch=False,currentExecutableParity=False,
  sourceSnapshot=3,contentBuild=1,integrationBuild=2,focusedRun=2,retainedRun=1,isolatedRun=2,playerRun=1,fireRun=1,auditRun=1,focusedMarkers=69,retainedMarkers=377,isolatedMarkers=12,playerMarkers=231,fireMarkers=40,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,savedNpcFlights=134,hpControlsPerBoot=16,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Prepared original Fire Bolt/Charged Bolt/Nova/Frost Nova use shared checked movement with immutable actor/binding/own packet and explicit supplied child seeds. Atomic batch validation/path copies preserve pool ownership. Historical player JSON omits optional NPC context.','Real typed HP,5percent intrinsic actor-stream critical,immunity,notification-before-HP,packet free and cold emitter identity compose shared ordered receiver. Existing NPC fire now delegates to the same helper;40Hydra/Tainted and231player/Mage regression markers retain real saved inputs and casts.','Original actor/items/resources/wounds/RNG and134real saved flights survive two actual reloads in each new cohort;resumed Fire Bolts deal actual HP. Offline/parked aging,no replay,atomic refusal and natural straight collision pass. All374parent retained markers remain.'],
  limits=['Chain Lightning and original proc callbacks/full registration history/child RNG allocation remain unbound. Nonzero or unknown NPC shield recipients and players explicitly refuse. Modern return-fire/defense/intrinsic callback ordering remains uncertified.','Natural travel/hit of every charged path/ring ray is not certified by stored-field and direct-hit controls. Terrain/footprint/enumeration/initial-step projection and source-removal remain explicit host adapters;no claim of modern executable equivalence.','The first fixture used an exclusive search bound;only its corridor changed. Identical productionContent1 passes corrected isolated/focused/retained suites. AI/startup/initializer/world seeds/learned payment/placement/caps/replacement/input/client/final assets and wider class gaps remain. No deployment or class completion.'])
 e.write('npc-item-spell-flights-native-evidence.json',result,files,check)
 print('Original NPC spell flights:69focused/377retained/12isolated/231player/40fire markers,two reloads;134saved own flights;proc dispatch/Chain/AI/cast remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
