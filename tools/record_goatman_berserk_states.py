#!/usr/bin/env python3
"""Bind NPC418 live defense/clock/persistence to focused and full acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/goatman-berserk-states'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False,focused_only=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==181
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert log.count('NPC Berserk:108complete-source state bridges,real NPC defense/miss-start and mode controls;natural expiry/persistence staged')==1
 assert log.count('goatman-berserk-state-result: passed')==1 and log.count('NPC Berserk:actual saved timed state/deadline restores zero defense;temporary state omitted,expired state ignored')==2
 frozen_receipt('de3ad27','tools/reference/goatman-npc-melee-native-evidence.json')
 previous=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/goatman-npc-melee/stats-native-checks.log').read_text()));current=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous.values())==177 and current-previous==Counter({'goatman-berserk-state-check':1,'goatman-berserk-state-result':1,'goatman-berserk-state-reload-check':2}) and not previous-current
 saved=json.loads((STAGE/'goatman-berserk-state-fixtures.json').read_text());s=saved['Effect'];assert saved['Timed']!=saved['Temporary'] and saved['TimedDefense']>0 and saved['TemporaryDefense']>0
 assert s['Actor']==saved['Timed'] and (s['Rank'],s['Flags'],s['DefenseOverride'])==(20,2,-100) and s['UntilUtc'].endswith('Z')
 full=None
 if not focused_only:
  full=inventory.validate_inventory(STAGE/'class-inputs.json');assert full['artifacts']==inputs['artifacts'] and full['copiedData']==inputs['copiedData']
  fullLog=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in fullLog and ' ERR]' not in fullLog and fullLog.count('Shutting down')==3
  assert fullLog.count(': passed')==1184 and fullLog.count('Writing world save snapshot done')==35
  assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
  assert fullLog.count('named-gear-check: passed')==1 and fullLog.count('named-gear-reload-check: passed')==2
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  if not focused_only:assert a==json.loads((STAGE/('full-'+name)).read_text())
  assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
  if not focused_only:artifacts.append(STAGE/('full-'+name))
 old=json.loads(subprocess.check_output(['git','show','de3ad27:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[714,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 for name in ['goatman-npc-melee-source-evidence.json','berserk-engine-evidence.json']:
  p=ROOT/'tools/reference'/name;j=json.loads(p.read_text());artifacts.append(p)
  for group in ['inputs','artifacts']:
   for n,h in j.get(group,{}).items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
  for n,h in j.get('sourceHashes',{}).items():p=ROOT/'.local/class-skills/d2moo-pinned-source'/n;assert sha(p)==h,n;artifacts.append(p)
 for p in (STAGE/'diagnostics').iterdir():artifacts.append(p)
 assert 'cannot convert' in (STAGE/'diagnostics/placement-build-failure.log').read_text() and 'FileNotFoundError' in (STAGE/'diagnostics/pre-server-missing-dll.log').read_text()
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==183 and status['completedClasses']==[]
 for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/n)
 if not focused_only:
  for n in ['class-inputs.json','class-native-checks.log','full-run.log']:artifacts.append(STAGE/n)
 for p in STAGE.glob('*fixtures.json'):artifacts.append(p)
 for n in ['tools/record_goatman_berserk_states.py','tests/check_goatman_berserk_states.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/creeper-frame-installation-evidence.json']:artifacts.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='NPC418 Berserk live defense and state persistence',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=181,retainedMarkers=177,worldBoots=3,actualWorldReloads=2,completeSourceStateBridges=108,naturalExpiryResults=1,savedTimedNpcBodies=1,savedTemporaryNpcBodiesWithoutInFlightState=1,fullClassMarkers=None if focused_only else 1184,fullClassBoots=None if focused_only else 3,fullClassReloads=None if focused_only else 2,fullClassSaveSnapshots=None if focused_only else 35,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[714,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,fullInputInventory=full,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='de3ad27',receipt='tools/reference/goatman-npc-melee-native-evidence.json'),
  established=['NPC418self-state now feeds ClassStats final defense through the existing override arithmetic. NPCs with no state retain ordinary defense;player Barbarian behavior retains its existing path. BeginFor connects prepared packet capture to the self-state after successful preparation,including a supplied miss. RefreshForRelease preserves the exact absolute deadline,including subframe precision.',
   '108complete-source successful-allocation state vectors match the live clock bridge. Natural rank1expiry restores ordinary NPC defense after68frames. Same-mode notifications retain temporary states;actual changed-mode notifications clear only flag4states. Timed flags survive mode changes;starting during temporary state retains its flag with the new source expiry. Actual mode scheduling still belongs to the future NPC actor.',
   'GenericPersistence186stores timed states with exact UTC deadlines. Worker serialization filters expired and temporary entries without pruning,refreshing or mutating live state. Invalid/duplicate restore inputs are rejected before replacement. Actual saved NPC fixture bodies retain a timed deadline and zero defense over two reloads,while a temporary-state body returns to normal defense. Inspection-only retention is applied after natural duration checks;production Begin duration is unchanged.',
   ('All177prior marker counts remain exact. The identical DLL/data package passes the1184-marker full class/projectile suite over three boots,two reloads and35save snapshots. Both builds are clean;70Python tests pass;573named decisions and106installed artifacts are unchanged.' if not focused_only else 'All177prior marker counts remain exact. Both builds are clean;70Python tests pass;573named decisions and106installed artifacts are unchanged. Broader full-class regression is still pending;this receipt claims focused acceptance only.')],
  remaining=['No learned Goatman cast,body/world AI or new NPC skill grant is enabled. The state consumer accepts an explicitly assigned NPC418caster;fixtures use existing encounter NPC bodies,not a fabricated Goatman body. Real target/result selection,Stun contact,action release and actual NPC animation/cancellation hooks remain unfinished.',
   'Temporary state requires the caller to deliver real ModeChanged/cancellation signals;no invented timeout is substituted. Current executable parity,modern extended passives/CrushingBlow/event16/BloodOath/sunder and real GenericPet helpers/new Frenzy/Cleave remain independent gaps. Facet/owner/death/respec/history and complete actor integration still need acceptance.',
   'The fixture initially called a player-only placement helper;compilation and pre-server missing-DLL diagnostics are retained. Legal NPC placement now uses the map spawn test. Saved inspection bodies are parked internally to avoid affecting other fixtures. Final NPC Berserk presentation and separate summon talent/hotkey icons remain pending. Main4c5d3a0and original saves/configuration/client remain preserved;no installation or class completion is claimed.'])
 p=ROOT/'tools/reference/goatman-berserk-states-native-evidence.json';out=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==out
 else:p.write_text(out)
 print('NPC Berserk:181focused/full='+str(result['verified']['fullClassMarkers'])+';108source bridges,natural expiry/read-only worker/timed persistence pass;learned summon remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--focused-only',action='store_true');args=p.parse_args();record(args.check,args.focused_only)
