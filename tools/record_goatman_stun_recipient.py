#!/usr/bin/env python3
"""Bind the prepared surviving-monster Stun phase; full release remains separate."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/goatman-stun-recipient'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 _,previousReceipt=frozen_receipt('ec678d9','tools/reference/goatman-berserk-states-native-evidence.json')
 assert previousReceipt['verified']['fullClassMarkers']==1184
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==185
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 previous=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/goatman-berserk-states/stats-native-checks.log').read_text()));current=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous.values())==181 and current-previous==Counter({'goatman-stun-recipient-check':1,'goatman-stun-recipient-result':1,'goatman-stun-recipient-reload-check':2}) and not previous-current
 marker='NPC Stun recipient:3840source selections/160native surviving commits/72zero-damage/72shorter refreshes;24lethal source states remain unsupported'
 assert log.count(marker)==log.count('goatman-stun-recipient-result: passed')==1 and log.count('NPC Stun recipient:actual saved body/exact deadline and shorter replacement verified')==2
 p=ROOT/'tools/reference/goatman-npc-release-source-evidence.json';source=json.loads(p.read_text());artifacts.append(p)
 assert (source['contexts'],source['zeroDamageStuns'],source['lethalHealthFieldStuns'],source['shortenedExistingStuns'])==(3840,72,24,72) and not source['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for n,h in source[group].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 assert not (ROOT/'.local/class-skills/goatman-npc-release/sanitizer.log').read_bytes()
 saved=json.loads((STAGE/'goatman-stun-recipient-fixtures.json').read_text());assert saved['Target']==saved['State']['Target'] and saved['State']['UntilUtc'].endswith('Z')
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','ec678d9:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[715,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==183 and status['completedClasses']==[]
 for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/n)
 artifacts += list(STAGE.glob('*fixtures.json'))
 for n in ['tools/record_goatman_stun_recipient.py','tests/check_goatman_stun_recipient.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/creeper-frame-installation-evidence.json']:artifacts.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='Prepared surviving monster Stun recipient phase',installed=False,handlerEnabled=False,currentExecutableParity=False,fullReleaseAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=185,retainedMarkers=181,worldBoots=3,actualWorldReloads=2,sourceSelections=3840,nativeSurvivingCommits=160,zeroDamageStuns=72,shorterReplacements=72,sourceLethalStatesExplicitlyUnsupported=24,naturalExpiryResults=1,fullClassMarkers=None,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[715,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='ec678d9',receipt='tools/reference/goatman-berserk-states-native-evidence.json',fullClassMarkers=1184),
  established=['PreparedMonsterStun reuses CombatStuns for native state/interruption/persistence. The existing Apply path retains its original selection and now shares only the unchanged final state commit. The new resolver checks positive duration,unique RNG,boss,velocity and hireling in source short-circuit order. Successful source unique draws use bound100 and threshold90;ordinary recipients do not draw. Hirelings cap13,other monsters250.',
   '3840qualified source recipient selections match the complete shared release fixture,including suppressed/miss/guard/Berserk paths. 160surviving native state commits match exact deadlines,including72zero-damage and72shorter replacements. Recipient application does not mutate health or reroll metadata. The24transient lethal source stuns are counted as unsupported,not equated with native death dispatch.',
   'Actual30-frame expiry,one saved real encounter NPC body,exact absolute state deadline over two reloads and shorter replacement after reload pass. Inspection retention is applied only after natural expiry. These are internal recipient fixtures,not a Goatman body/cast or full action release. All181previous marker counts remain exact;70Python tests,573named decisions and106installed artifacts remain unchanged.'],
  remaining=['Health/contact qualification,source callback and cold/poison/death ordering remain the future release caller responsibility. CommitSurvivor explicitly refuses dead/deleted/sub-one-life recipients for a positive duration. Native lethal/reentrant equivalence is not claimed. Metadata services in the source comparison are synthetic;actual world recipient metadata must be supplied and verified by the actor consumer.',
   'Full Goatman casting/body/AI,modern extended passives/event16/CrushingBlow/BloodOath/sunder and new Frenzy/Cleave callbacks remain unfinished. No installation,full-suite acceptance for this new package,learned handler or class completion is claimed. The frozen predecessor retains its1184-marker full regression. Final Stun effects/audio and separate talent/hotkey icons remain pending;the existing stunned-state presentation is an identified placeholder.'])
 p=ROOT/'tools/reference/goatman-stun-recipient-native-evidence.json';out=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==out
 else:p.write_text(out)
 print('NPC Stun:185native/3boots/2reloads;3840source selections,160surviving commits,24lethal source states explicitly unsupported;70Python.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
