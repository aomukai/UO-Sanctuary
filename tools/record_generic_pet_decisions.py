#!/usr/bin/env python3
"""Bind current root/follow policies to native retained-build acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-decisions'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==171
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert log.count('GenericPet decisions:12816captured-current root/46936follow comparisons;ordered services only,movement/combat/owner callbacks remain gated')==3
 frozen_receipt('fe8d9e1','tools/reference/generic-pet-parameters-native-evidence.json')
 previous=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/generic-pet-parameters/stats-native-checks.log').read_text()));current=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous.values())==168 and current-previous==Counter({'generic-pet-decisions-check':1,'generic-pet-decisions-reload-check':2}) and not previous-current
 for name in ['warlock-runtime-reference-evidence.json','generic-pet-parameter-emulation.json','generic-pet-decisions-source-evidence.json']:
  p=ROOT/'tools/reference'/name;j=json.loads(p.read_text());artifacts.append(p)
  for n,h in j['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 runtime=json.loads((ROOT/'tools/reference/warlock-runtime-reference-evidence.json').read_text());assert runtime['verified']['offlineInitializerContexts']==2016 and not runtime['completeAiVerified']
 source=json.loads((ROOT/'tools/reference/generic-pet-decisions-source-evidence.json').read_text());assert (source['rootBytes'],source['followBytes'],source['rootCases'],source['followCases'])==(781,748,12816,46936) and not source['processMemoryRead'] and not source['targetProcessCodeExecuted'] and not source['completeAiVerified']
 library=ROOT/source['emulatorLibrary']['path'];assert sha(library)==source['emulatorLibrary']['sha256'];artifacts.append(library)
 recheck=STAGE/'source-recheck.log';assert '12816 root/46936 follow contexts' in recheck.read_text();artifacts.append(recheck)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','fe8d9e1:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[709,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==183 and status['completedClasses']==[]
 for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/n)
 for p in STAGE.glob('*fixtures.json'):artifacts.append(p)
 for n in ['tools/record_generic_pet_decisions.py','tests/check_generic_pet_decisions.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/goatman-tooltip-observation.json','tools/reference/creeper-frame-installation-evidence.json']:artifacts.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='GenericPet root/follow policies with explicit services',installed=False,handlerEnabled=False,fullAiVerified=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=171,retainedMarkers=168,worldBoots=3,actualWorldReloads=2,rootCasesPerBoot=12816,followCasesPerBoot=46936,retainedInitializerCasesPerBoot=2016,ownedDemonProfiles=3,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[709,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='fe8d9e1',receipt='tools/reference/generic-pet-parameters-native-evidence.json'),
  established=['Current781-byte root and748-byte follow instructions execute offline in Unicorn2.1.4 with synthetic memory and declared helper services. Native order/state/returns match12816root and46936follow contexts per boot,including owner-chain guards,null/dynamic/static-path branches,all five town levels,leash boundaries and independent follow/combat returns.',
   'Root initializes before removing every queued type2AITHINK event. A successful over-leash follow bypasses combat;failure permits town/combat checks and a later follow retry. Either pet or owner town suppresses combat. Failed final follow idles15. Finish runs before all three current AI-control state writes;declared services mutate state to verify this order.',
   'Follow evaluates owner distance,moving predicate and both region lookups before choosing a numeric executor mode. Warp threshold precedes distance100,region mismatch,moving,destination36and distance4conditions. Missing room gives region0;missing region gives-1. Ownerless mode2requires positive wander. Actual movement mode meanings and executor behavior remain unverified.',
   'All168predecessor marker counts remain exact,including raw initializer profiles and saved Warlock/Witch birth suppliers. No new actor or in-flight AI persistence is claimed. Both builds are clean,70Python tests pass,573named decisions and106installed artifacts remain unchanged.'],
  remaining=['Current AI dispatcher linkage,initializer owner iteration,actual owner/room/distance/region services,follow executor and combat/skill selection remain unverified. Source emulation uses declared services and does not certify a functioning world summon.',
   'Modern extended passives,hit callbacks,Blood Oath and NPC Stun/Berserk/Frenzy/Cleave modes remain independent work. No learned summon or world AI is enabled.',
   'The owned D2R process was absent when another bounded helper capture was attempted;zero target memory was read this turn. Work continued from frozen captures. Final summon bodies,effects/audio and separate talent/hotkey icons remain in their registers. Main4c5d3a0and original saves/configuration/client remain preserved;no installation,full-suite regression or class completion is claimed.'])
 p=ROOT/'tools/reference/generic-pet-decisions-native-evidence.json';out=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==out
 else:p.write_text(out)
 print('GenericPet decisions:171native/3boots/2reloads,12816root/46936follow comparisons per boot,70Python;actual services/full AI remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
