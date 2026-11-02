#!/usr/bin/env python3
"""Bind Hydra's real emitter/flight checkpoint without certifying open parity gaps."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from record_blaze import proof

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/hydra-world'
BUILD=8
PARENT='483ce07ba85da7088a7e8e5b0e4957a9b9523470'
sha=inventory.sha
def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)

def record(check=False):
 inventory.STAGE=STAGE
 tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
 snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
 assert sum(counts.values())==655 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_hydra_world import configuration
 c=configuration(True)
 assert len(c['extra_families'])==47 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==63
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['hydra-world-reload-result']==2
 for text in ('Hydra world:actual source bodies/captured hard/mastery/ordinary clock/slow source bolts/typed ordered HP and credit',
 'Hydra prepared:2880birth suppliers/41310endpoints/576clocks/7776AI decisions',
 'Player Frozen Orb:9180native endpoints/9216shared source update comparisons',
 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):
  assert text in log,text
 saved=json.loads((STAGE/'hydra-world-fixtures.json').read_text())
 assert saved['Generation']==2 and saved['Mana']>0 and saved['Life']>0 and saved['TargetLife']>0
 assert len(saved['Heads'])==3 and len({h['Id'] for h in saved['Heads']})==3 and len({h['Source']['Actor'] for h in saved['Heads']})==3
 for head in saved['Heads']:
  assert head['Owner']==saved['Owner'] and head['Source']['Birth']['Rank']==head['Rank']>1
  assert head['Source']['Control']['Frame']>33 and head['Source']['Control']['Dispatches']>0
  assert head['Source']['RetireUtc'] is None and head['Source']['Fault'] is None
 for bolt in saved['Bolts']:
  assert bolt['Owner']==saved['Owner'] and any(h['Id']==bolt['Head'] and h['Source']['Actor']==bolt['Source']['Actor'] for h in saved['Heads'])
 focused_dir=STAGE/f'focused-accepted-{BUILD}'
 focused=inventory.validate_inventory(focused_dir/'birth-inputs.json')
 assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
 flog=(focused_dir/'birth-native-checks.log').read_text()
 assert sum(markers(focused_dir/'birth-native-checks.log').values())==35 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (focused_dir/f'focused-run-{BUILD}.log').read_text()
 for kind in ('content','integration'):
  text=(STAGE/f'{kind}-build-{BUILD}.log').read_text()
  assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  name=f'{kind}/Sanctuary.{kind.title()}.dll';assert sha(STAGE/name)==sha(focused_dir/name)
 proofs=[proof('hydra-'+n+'-source-evidence.json',cases,routines) for n,cases,routines in
  [('creation',12800,9),('ai',15552,8),('clock',576,8),('damage',41310,16),('bolt-creation',5760,10),('retained-owner-damage',23040,14)]]
 for p in proofs:assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 parent=ROOT/'tools/reference/hydra-prepared-native-evidence.json'
 assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/hydra-prepared/birth-inputs.json').read_text())
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][n]==prior['artifacts'][n]
 assert tested['copiedData'].keys()==prior['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
 current=next(s for s in status['skills'] if s['talentId']=='sorceress.hydra');before=next(s for s in old['skills'] if s['talentId']=='sorceress.hydra')
 assert current['verification']==before['verification']=='partial-native-coverage'
 before['remaining']=current['remaining'];assert status==old and not status['completedClasses']
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
 assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text())
  assert value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,digest in value['InputHashes'].items():assert sha(ROOT/path)==digest,path
  previous=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore}
  fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[905,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
 assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 negative=STAGE/'negative-town-facet'
 assert 'Hydra return used owner\'s facet for the target town check' in (negative/'birth-native-checks.log').read_text()
 assert 'hydra-world-check: failed' in (negative/'birth-native-checks.log').read_text()
 ni=json.loads((negative/'birth-inputs.json').read_text())
 assert sha(negative/'NativeMonsterReturn.cs')==ni['sourceInputs']['src/Sanctuary.Content/NativeMonsterReturn.cs']
 assert sha(negative/'HydraWorldChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/HydraWorldChecks.cs']==sha(ROOT/'tests/Sanctuary.Integration/HydraWorldChecks.cs')
 assert sha(negative/'Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 files=[Path(__file__).resolve(),ROOT/'tests/check_hydra_world.py',ROOT/'tools/build_skill_roadmap.py',parent,*proofs,
  *(ROOT/'docs'/n for n in ('hydra-world-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','hydra-world-fixtures.json',
   'audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
  *(focused_dir/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{BUILD}.log','hydra-world-fixtures.json')),
  *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','NativeMonsterReturn.cs','HydraWorldChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 value=dict(referenceVersion='3.3.93847',component='Hydra native emitting heads and captured saved fire bolts',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=655,focusedMarkers=35,boots=3,reloads=2,saves=3,newServerWireActions=6,retainedServerWireActions=183,
   sourceCases=75998,retainedOwnerSourceCases=23040,nativeBirthComparisons=2880,nativeEndpointComparisons=41310,nativeClockComparisons=576,nativeAiComparisons=7776,
   fixtureFamilies=47,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=[
   'Prepared learned release resolves live selected aim/rank, captures accrued mastery/pierce and hard synergies, admits independent placed heads under18cap, and pays fractional mana once only on successful placement. No local delay is added.',
   'Actual untargetable nonblocking native NPC heads use source-compared emergence/AI25/action8/end12/neutral15 and full64bit chance. Existing Hydra persistence/timer retains actor identities, captures, clocks, aims and owned fire bolts; atomic restore preserves historical virtual records without invented captures.',
   'Bolts capture actual-head Slow Missiles, retain typed source steps/LastCollide/seed, and use shared basic update/ordered mitigation/HP/death/return with the head as attacker and player as responsible owner. Tests cover positive/negative pierce, immunity, no second player mastery/potion, native credit and registered retired-emitter HP.',
   'Old-facet local combat persists;native return checks the emitting facet rather than a town at the same coordinates on the owner\'s new facet. The exact cross-facet regression fails on the preserved earlier DLL and passes after the fix. Offline clocks age without replayed shots/RNG. Replaced/expired heads stop AI and use an explicit bounded native emitter lease through remaining flights and ten-frame presentation,then delete;this is a host adapter,not proof of D2R final GUID removal.',
   'Two process reloads naturally continue the original actual heads/bolts and retain real equipped gear,wounds and mana. Six new and183retained server wire actions pass across3boots;four audits retain decisions,71Python/573named/240catalogue/generators pass;106installed hashes unchanged.'],
  remaining=[
   'Exact modern AI acquisition/room ordering,full path/grid/movement/factory/global RNG equivalence and outer InitSteps dispatch remain unproved;native nearest-target and stable source-cell bridges are explicit adapters.',
   'CanDestroy missile interception and exact final D2R death/corpse/GUID retention are not complete. Explicit native emitter deletion cancels dependent flights;it never substitutes player identity or leaves an immortal emitter. Captured nonzero fire sunder is rejected before mana until its NPC consumer exists.',
   'Source/native item event subscribers,uninterruptible/death-delay/sand-leaper/custom incoming recipients,PvP,charged/oskill,graphical client and full old-DLL/unheld-save acceptance remain separate. No complete Hydra or class parity certificate is claimed.',
   'Final head emergence/attack/retirement and bolt/contact effects/audio,plus talent/right/quick icons,remain independent identified placeholders. No deployment or save reset.'])
 out=ROOT/'tools/reference/hydra-world-native-evidence.json';encoded=json.dumps(value,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Hydra world:655full/35focused markers,actual NPC source/credit/captured bolts and two original natural reloads,6new/183retained wire actions,four audits/71Python;106installed hashes unchanged. Modern/interception/final GUID/client/assets gaps remain explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
