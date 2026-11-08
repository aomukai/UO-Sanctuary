#!/usr/bin/env python3
"""Authenticate current Death Mark pet/command composition and owned metadata."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
from build_pet_path_target_tables import build as metadata
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/death-mark-pets'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 metadata(True)
 source=ROOT/'tools/reference/death-mark-pets-current-evidence.json';s=json.loads(source.read_text());assert s['cases']==97 and s['commandCases']==165 and s['sourceBytes']==546 and s['commandBytes']==126
 caster=ROOT/'tools/reference/death-mark-cast-current-evidence.json';p=json.loads(caster.read_text());assert p['cases']==122
 for proof in [s,p]:
  for rel,h in proof['artifacts'].items():assert sha(ROOT/rel)==h,rel
 helpers=ROOT/'.local/class-skills/warlock-runtime-reference/callees-death-mark-pet-services-20261108.json';h=json.loads(helpers.read_text());r=next(r for r in h['records']if r['rva']=='0x3739a0')
 placement=ROOT/'tools/reference/generic-pet-monster-placement-source-evidence.json';reuse=json.loads(placement.read_text());assert reuse['cases']==5229 and all(f['sha256']in reuse['artifacts'].values()for f in r['fragments'])
 command=ROOT/'.local/class-skills/warlock-runtime-reference/callees-death-mark-target-command-20261109.json';cmd=json.loads(command.read_text());assert cmd['records'][0]['allUnwindFragmentsCaptured']
 controls=ROOT/'.local/class-skills/warlock-runtime-reference/callees-death-mark-control-list-services-20261109.json';control=json.loads(controls.read_text());assert all(r['allUnwindFragmentsCaptured']for r in control['records'])
 for capture in [h,cmd,control]:
  assert not any(capture[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  for rel,digest in capture['artifacts'].items():assert sha(ROOT/rel)==digest,rel
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==18 and ': failed'not in log and ' ERR]'not in log
 for name in ['iron-golem-birth','pet-path-targets']:assert counts[name+'-check']==3
 for name in ['iron-golem-items','npc-resource-callbacks','death-mark-pets','death-mark-cast']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-3.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-6.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,digest in native['sourceInputs'].items():assert sha(ROOT/rel)==digest,rel
 for rel,digest in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==digest,rel
 for name in ['death-mark-pets','death-mark-cast']:assert int((STAGE/(name+'-fixtures.json')).read_text())==2
 prior=json.loads((ROOT/'.local/class-skills/death-mark-cast/birth-inputs.json').read_text())['copiedData'];assert {k for k in prior if prior[k]!=native['copiedData'][k]}=={'Data/Sanctuary/Balance/generic-pet-path-targets.json'}and prior.keys()==native['copiedData'].keys()
 data=ROOT/'data/Sanctuary/Balance/generic-pet-path-targets.json';rows=json.loads(data.read_text());old=json.loads(subprocess.check_output(['git','show','b91fea3b46924c6b0752eed80e484b870177c178:data/Sanctuary/Balance/generic-pet-path-targets.json'],cwd=ROOT))
 assert [{k:v for k,v in r.items()if k!='flags62'}for r in rows]==old
 inventory=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(r['implementation']=='handler-present'for r in inventory['skills'])==211 and inventory['completedClasses']==[]
 paths=['src/Sanctuary.Content/'+x+'.cs'for x in ['DeathMarkPets','GenericPetTargetCommand','GenericPetPathTargets','GenericPetMonsterPlacement']]+['tests/Sanctuary.Integration/DeathMarkPetChecks.cs','tests/check_death_mark_pets.py','tools/probe_death_mark_pets.py','tools/build_pet_path_target_tables.py','tools/record_death_mark_pets.py','tools/reference/death-mark-pets-current-evidence.json','tools/reference/pet-path-targets-data-evidence.json']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedDeathMarkEnabled=False,currentExecutableParity=False,
  sourceCases=dict(petCallback=97,targetCommand=165,retainedCast=122),sourceBytes=dict(petCallback=546,targetCommand=126),nativeMarkers=18,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=3,sourceSnapshot=6,nativeRun=3,newSourceCompilerMatrices=0,priorCompilerMatricesRerun=0,
  sharedPlacement=dict(proofSha256=sha(placement),retainedSourceCases=5229,codeByteIdentical=True,oldEmulatorNotRerun=True),targetCommandCapture=dict(path=str(command.relative_to(ROOT)),sha256=sha(command),userEnemyCastConfirmed=True,allFragmentsCaptured=True),controlServiceReference=dict(path=str(controls.relative_to(ROOT)),sha256=sha(controls),completeBytes=2220,translationVerified=False),
  metadata=dict(rows=751,compiledPathFlagChecks=6008,compiledSwitchAiChecks=751,commandOffset=62,commandMask=1,sourceFlag='switchai',oldPathRowsUnchanged=True,demonRows=[742,743,744]),
  artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
  scope='Prepared current pet callback and shared target-command helper match262 machine-code cases,including null/failed relocation,no quota loss,overlay/target/idle order,21stunned/1freeze/11cold first-list precedence,root reload after unlink,post-unlink signed free flags and untouched poison/duplicate states. Native bindings call existing monster placement and the real current command setter;command refusal does not reject successful relocation. Combined cast/pet checks preserve a shared3pet quota,failed-place retries,no mark when none moved,and state-allocation failure after pet commands. Original122 cast cases and the unchanged byte61 path reset consumer remain passing. Byte62 is exported from the same owned751row primary table and switchai corroborated against text;actual demon rows reach the setter through explicit selector-bound metadata. No actual DeathMark world/debuff/CC/controller or learned input is admitted by these detached checks.',
  diagnostic='C1/I1/native1passed15 markers/97 callback plus122 cast cases. User monster-targeted DeathMark then unlocked126byte target command;C2/I2/native2adds165 command cases and passes15 markers. C3/I3/native3publishes owned command flags and adds retained path reset checks,passes18 markers. Prior passed input/source/binary/log/fixture sets are archived native-1/native-2. No failed native run or previous compiler matrix rerun.',
  remaining=['Bind callback/command/placement to actual original demon roster/controller and saved command fields;native checks here use detached source contexts,not live DeathMark actors.','Overlay326byte/idle204byte and freshly captured unlink1896byte/free324byte helpers remain services needing comparison/binding. Reuse existing stat/movement systems where contracts match;no new user cast request is pending.','Connect actual target armor/normal/magic damage-reduction state,duration/replacement,requirespettype startup,localdelay20,SC payment/cancellation and left/right/quick before learned admission.','Final cast/warp/landing/mark/expiry/audio and talent/hotkey icons remain separate pending assets;no images generated.'])
 path=ROOT/'tools/reference/death-mark-pets-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print('Death Mark pets:97callback/165command/122retained cast cases;18native markers/2reloads;owned751row command metadata and old path reset pass.211handlers;actual world/learned route remains unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
