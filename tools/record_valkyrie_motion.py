#!/usr/bin/env python3
"""Bind original equipped ground movement and actual moving-defense evidence."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-motion';WIDE=BASE/'valkyrie-motion-retained'
PARENT='ef99e49d69273a4ec3139b9c90eaaa4d82bff609'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-4.json');old=e.load(ROOT/'tools/reference/valkyrie-animation-native-evidence.json');extra=Counter({'valkyrie-motion-check':1,'valkyrie-motion-reload-check':2})
 for previous,stage,count,run in [(BASE/'valkyrie-animation',STAGE,60,2),(BASE/'valkyrie-animation-retained',WIDE,368,1)]:
  path=previous/'birth-native-checks.log';assert e.sha(path)==old['files'][str(path.relative_to(ROOT))];e.cohort(stage,markers(path)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Valkyrie motion:actual shared walk/run/follow and source-mode melee/missile Evade;original rates/items/wounds/stream through parking/two reloads;offense and learned cast remain gated')==3
  saved=e.load(stage/'valkyrie-motion-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==2 and sum(len(h['Items'])for h in saved['Bodies'])==4
  for h in saved['Bodies']:
   body=json.loads(h['Text']);rng=h['Random'];motion=body['GroundMotion'];world=motion['World'];controller=world['Controller']
   assert body['Owner']==saved['Owner']==world['Owner']==rng['Owner']and rng['Actor']==h['Body']and h['Life']>0
   assert motion['Profile']==controller['Profile']=='valkyrie-original-ground-motion-v1'and motion['WalkVelocityPercent']==75 and motion['RunVelocityPercent']==125
   assert controller['Mode']==1 and controller['Attempts']==controller['Hits']==0 and controller['Fault']is None and controller['ActionAt']==controller['EndAt']==0
   assert controller['Motion']['Points']==[]and controller['Frame']>=120
   assert body['EquipmentWeapons']['Identity']==rng['ValkyrieBinding']and body['EquipmentDefenseProfile']=='valkyrie-inherited-defense-v1'
   for text in h['Items']:
    item=json.loads(text);assert item['Equipment']['Actor']==h['Body']and item['Equipment']['Owner']==saved['Owner']
 for name in ['content-build-3.log','integration-build-3.log']:
  text=(STAGE/name).read_text();assert all(n in text for n in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 failed=STAGE/'failed-native-1';assert 'No independent fixture corridor near 6200,3820'in(failed/'birth-native-checks.log').read_text()
 assert 'firstRow;y<3800' in (ROOT/'tests/Sanctuary.Integration/FixtureSpace.cs').read_text()
 assert e.sha(failed/'content/Sanctuary.Content.dll')!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 assert e.load(failed/'source-snapshot-1.json')['inputs']['copiedData']==born['copiedData']
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
 source=ROOT/'tools/reference/valkyrie-animation-source-evidence.json';proof=e.load(source);assert proof['contexts']==1368 and len(proof['completeRoutineHashes'])==9 and not proof['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 files=e.stage_files(STAGE,WIDE)+[source,ROOT/'tests/check_valkyrie_motion.py',Path(__file__).resolve()]
 for sub in ['failed-native-1','prior-build-2','prior-content-2']:files.extend(p for p in(STAGE/sub).rglob('*')if p.is_file())
 files += [ROOT/'docs'/n for n in ['valkyrie-motion-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,
  sourceSnapshot=4,contentBuild=3,integrationBuild=3,focusedRun=2,retainedRun=1,auditRun=1,focusedMarkers=60,retainedMarkers=368,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
  originalMotionBodies=2,originalMotionItems=4,suppliedFixtureRates=dict(walkVelocityPercent=75,runVelocityPercent=125,regenerationBase=300),pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Optional original-body motion context reuses the shared NecroPet/ground conductor for actual source walk/run,fixed-point/native projection,follow/long-leash return,class-aware parking/suspension and saved state. Original resource profile is mandatory;supplied/item regeneration and delegated recovery avoid double counting.','Actual source-mode moving guaranteed melee/ordered melee/missile Evade uses one persisted defense draw. A native pose change refuses before accuracy/RNG until repaired. Detached path snapshots,original body/items/wounds/stream and old JSON retain through parking/two actual reloads.','Automatic AI attacks refuse the unfinished original item-event/contact binding after AI decisions,before hit/damage RNG or HP;no legacy Iron/unarmed fallback. All365parent broad markers and original source timing/defense corpora retain.'],
  limits=['75/125walk/run and300regeneration are mandatory supplied test inputs,not established current initializer/run-state defaults. Current NU data and older20frame first think are supplementary source context;modern full mode/rate/external stat-list/client/terrain equivalence remains unverified.','Full original item events/skills,ordered contact/action/recovery modes,external offensive composition,nonzero inherited sunder,initializer/world RNG and learned paid placement/replacement/input remain unfinished. Final effects/audio/talent/hotkey assets stay separate;no class completion or deployment.','First native failure was a fixture scan-bound error before motion ran. Its complete inputs/sources/binaries/logs and intermediate built versions are preserved;the final cohort uses snapshot4/Content3/Integration3 only.'])
 e.write('valkyrie-motion-native-evidence.json',result,files,check)
 print('Valkyrie original movement:60focused/368retained markers,two reloads;actual follow/run/walk/moving melee-missile Evade;original2bodies4items;202handlers,contact/cast gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
