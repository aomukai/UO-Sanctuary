#!/usr/bin/env python3
"""Bind current raw AI parameters to native retained-build acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-parameters'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==168
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert log.count('GenericPet parameters:2016captured-current initializer cases,3owned demon profiles;raw decoder only,full AI/owner callbacks remain gated')==3
 frozen_receipt('77393e1','tools/reference/goatman-native-evidence.json')
 previous=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/goatman-foundation/stats-native-checks.log').read_text()));current=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous.values())==165 and current-previous==Counter({'generic-pet-parameters-check':1,'generic-pet-parameters-reload-check':2}) and not previous-current
 for name in ['warlock-runtime-reference-evidence.json','generic-pet-parameter-emulation.json']:
  p=ROOT/'tools/reference'/name;j=json.loads(p.read_text());artifacts.append(p)
  for n,h in j['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 runtime=json.loads((ROOT/'tools/reference/warlock-runtime-reference-evidence.json').read_text());assert runtime['verified']['offlineInitializerContexts']==2016 and not runtime['completeAiVerified']
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','77393e1:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[708,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==183 and status['completedClasses']==[]
 for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/n)
 for p in STAGE.glob('*fixtures.json'):artifacts.append(p)
 for n in ['tools/record_generic_pet_parameters.py','tests/check_generic_pet_parameters.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/goatman-tooltip-observation.json','tools/reference/creeper-frame-installation-evidence.json']:artifacts.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='GenericPet raw parameter decoder',installed=False,handlerEnabled=False,fullAiVerified=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=168,retainedMarkers=165,worldBoots=3,actualWorldReloads=2,currentInitializerCasesPerBoot=2016,ownedDemonProfiles=3,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[708,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='77393e1',receipt='tools/reference/goatman-native-evidence.json'),
  established=['Shared immutable decoder loads the owned Goatman,Tainted and Defiler raw AI profiles. Current x64 initializer instructions independently produce the2016 boundary controls compared by native code on all three boots. No difficulty-column selection,invented percentage clamp or range sorting is applied.',
   'Missing-monstats defaults differ from an all-zero parameter record. Signed16 squares,unsigned flag bits,nonzero booleans and the three explicit clamps match current captured code. Slots20..23are ignored by the parameter prefix. Unsupported BindDemon routing and malformed records are refused.',
   'All165 predecessor markers remain exact,including saved Warlock/Witch birth suppliers and the current Nightmare Goatman tooltip observation. No new actor or in-flight AI persistence is claimed. Both builds are clean;70Python tests pass;573named decisions and106installed artifacts are unchanged.'],
  remaining=['Actual owner callback iteration may alter the raw prefix;it is a declared no-op in the isolated initializer proof. Current AI dispatcher linkage,root execution,target/skill selection,follow/movement/event/seed services and combat remain unverified.',
   'Modern extended passives,hit callbacks,Blood Oath and NPC Stun/Berserk/Frenzy/Cleave modes remain independent work. No learned summon or world AI is enabled by this decoder.',
   'Final summon bodies,animations,skill effects/audio and separate talent/hotkey icons remain in their asset registers. Main4c5d3a0and original saves/configuration/client remain preserved;no installation,full-suite regression or class completion is claimed.'])
 p=ROOT/'tools/reference/generic-pet-parameters-native-evidence.json';out=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==out
 else:p.write_text(out)
 print('GenericPet raw parameters:168native/3boots/2reloads,2016current-code cases per boot,70Python;full AI/learned summons remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
