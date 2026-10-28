#!/usr/bin/env python3
"""Bind current combat orchestration to native retained-build acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-combat'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==174
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert log.count('GenericPet combat:8079captured-current orchestration comparisons,3fatal-selector boundaries;real targeting/movement/rating/skill helpers remain gated')==3
 frozen_receipt('71d3fd5','tools/reference/generic-pet-decisions-native-evidence.json')
 previous=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/generic-pet-decisions/stats-native-checks.log').read_text()));current=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous.values())==171 and current-previous==Counter({'generic-pet-combat-check':1,'generic-pet-combat-reload-check':2}) and not previous-current
 for name in ['warlock-runtime-reference-evidence.json','generic-pet-parameter-emulation.json','generic-pet-decisions-source-evidence.json','generic-pet-combat-source-evidence.json']:
  p=ROOT/'tools/reference'/name;j=json.loads(p.read_text());artifacts.append(p)
  for n,h in j['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 runtime=json.loads((ROOT/'tools/reference/warlock-runtime-reference-evidence.json').read_text());assert runtime['verified']['offlineInitializerContexts']==2016 and not runtime['completeAiVerified']
 source=json.loads((ROOT/'tools/reference/generic-pet-combat-source-evidence.json').read_text());assert (source['machineCodeBytes'],source['matrixCases'],source['fatalSelectorCases'])==(2067,8079,3) and not source['processMemoryRead'] and not source['targetProcessCodeExecuted'] and not source['completeAiVerified']
 library=ROOT/source['emulatorLibrary']['path'];assert sha(library)==source['emulatorLibrary']['sha256'];artifacts.append(library)
 recheck=STAGE/'source-recheck.log';assert '8079 orchestration contexts,3fatal-selector boundaries' in recheck.read_text();artifacts.append(recheck)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','71d3fd5:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[710,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==183 and status['completedClasses']==[]
 for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/n)
 for p in (STAGE/'diagnostics').iterdir():artifacts.append(p)
 failed=(STAGE/'diagnostics/misrouted-audit-native.log').read_text();assert 'DirectoryNotFoundException' in failed and 'named-gear-check: failed' in failed and 'generic-pet-combat-check: passed' in failed
 failedInputs=json.loads((STAGE/'diagnostics/misrouted-audit-inputs.json').read_text());assert failedInputs==inputs
 for p in STAGE.glob('*fixtures.json'):artifacts.append(p)
 for n in ['tools/record_generic_pet_combat.py','tests/check_generic_pet_combat.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/goatman-tooltip-observation.json','tools/reference/creeper-frame-installation-evidence.json']:artifacts.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='GenericPet combat orchestration with explicit services',installed=False,handlerEnabled=False,fullAiVerified=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=174,retainedMarkers=171,worldBoots=3,actualWorldReloads=2,combatCasesPerBoot=8079,fatalSelectorBoundariesPerBoot=3,retainedRootCasesPerBoot=12816,retainedFollowCasesPerBoot=46936,retainedInitializerCasesPerBoot=2016,ownedDemonProfiles=3,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[710,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='71d3fd5',receipt='tools/reference/generic-pet-decisions-native-evidence.json'),
  established=['The complete2067-byte captured current combat body executes offline with declared gather/range/search/collision/movement/rating/order/action services. Native behavior matches8079contexts per boot,including three invalid-selector fatal-service boundaries. This certifies orchestration and supplied service effects,not the actual helper mechanics.',
   'A remembered skill is matched by first base-mode flag or exact skill ID while counter<25;negative counters remain signed. Its counter is copied before target/skill/counter memory is reset. A nonzero remembered-target range advances the signed wrapping cycle and drops the target bias when the increment exceeds5. Any of five search slots can retain the old target.',
   'Accepted known skills get target plus maximum rating. Accepted targets with unknown skills use a second full search at range0and shared opaque rating context. Rejected targets use the blocked-position recovery path or fresh range/search/prepare/rating. Source order and raw40-byte descriptor payloads remain intact.',
   'Choose0orders then tries candidates until success or an exact int-min unusable marker;choose1delegates to an alternative helper;other values returnfalse. Remembered identity is a vector slot whose contents can change during supplied ordering. Movement and action callbacks are supplied services,not enabled world behavior.',
   'All171predecessor marker counts remain exact,including raw initializer/root/follow controls and saved Warlock/Witch birth suppliers. Both builds are clean,70Python tests pass,573named decisions and106installed artifacts remain unchanged. No new actor or in-flight AI persistence is claimed.'],
  remaining=['Current AI dispatcher linkage,initializer owner iteration,actual skill gathering/range/search/rating/order/action/alternative helpers and follow executor remain unverified. Borrowed-container controls do not certify dynamic allocation,failure or exception cleanup. The current fatal-selector boundary is surfaced as an unsupported-context exception,not a guessed normalfalse result.',
   'Modern extended passives,hit callbacks,Blood Oath and NPC Stun/Berserk/Frenzy/Cleave remain independent work. No learned summon or world AI is enabled. Final bodies,effects/audio and separate talent/hotkey icons remain in their registers.',
   'The initial cookie fixture incorrectly zeroed the return register;the declared no-op cookie service now preserves it without altering captured code. The first native run passed combat then failed writing a named audit to a mistyped nonexistent path. Its logs/input inventory are retained;the exited run was rerun with only the output environment corrected. No source or test expectation was weakened. Main4c5d3a0and original saves/configuration/client remain preserved;no installation,full-suite regression or class completion is claimed.'])
 p=ROOT/'tools/reference/generic-pet-combat-native-evidence.json';out=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==out
 else:p.write_text(out)
 print('GenericPet combat:174native/3boots/2reloads,8079current-code comparisons per boot,70Python;real helpers/full AI remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
