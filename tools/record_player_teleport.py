#!/usr/bin/env python3
"""Bind approved Teleport input/resource/save acceptance to exact tested artifacts."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/player-teleport'
BUILD=2
PARENT='4dab25bc594d634be24a7e0e5ceed74a6db8655f'
sha=inventory.sha
def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)

def record(check=False):
 inventory.STAGE=STAGE
 tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
 assert sum(counts.values())==681 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_player_teleport import configuration
 c=configuration(True)
 assert len(c['extra_families'])==48 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==66
 assert c['save_command']==c['save_result']=='player-teleport-save' and counts['player-teleport-save']==3
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['player-teleport-reload-result']==2
 for text in ('Player Teleport:255pinned rank costs,clamped FP mana/no revived remainder',
 'Hydra world:actual source bodies/captured hard/mastery/ordinary clock/slow source bolts/typed ordered HP and credit',
 'Player Frozen Orb:9180native endpoints/9216shared source update comparisons',
 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):assert text in log,text
 saved=json.loads((STAGE/'player-teleport-fixtures.json').read_text())
 assert saved['Generation']==2 and saved['Mana']>0 and saved['Mana']%256==17 and saved['Life']>0 and saved['Rank']>1
 assert len({saved[n] for n in ('Owner','Gear','Pack','Belt')})==4 and saved['PendingStaged'] and saved['PendingScroll']==0
 focused_dir=STAGE/f'focused-accepted-{BUILD}';focused=inventory.validate_inventory(focused_dir/'birth-inputs.json')
 assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
 flog=(focused_dir/'birth-native-checks.log').read_text()
 assert sum(markers(focused_dir/'birth-native-checks.log').values())==38 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (focused_dir/f'focused-run-{BUILD}.log').read_text()
 for kind in ('content','integration'):
  text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  name=f'{kind}/Sanctuary.{kind.title()}.dll';assert sha(STAGE/name)==sha(focused_dir/name)
 negative=STAGE/'negative-mana-cap';ni=json.loads((negative/'birth-inputs.json').read_text())
 assert 'Teleport revived a clamped fractional mana remainder' in (negative/'birth-native-checks.log').read_text() and 'player-teleport-check: failed' in (negative/'birth-native-checks.log').read_text()
 assert (negative/'Teleportation.cs').read_bytes()==historical('src/Sanctuary.Content/Teleportation.cs')
 assert sha(negative/'Teleportation.cs')==ni['sourceInputs']['src/Sanctuary.Content/Teleportation.cs']
 assert sha(negative/'PlayerTeleportChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/PlayerTeleportChecks.cs']
 assert sha(negative/'Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 parent=ROOT/'tools/reference/hydra-world-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/hydra-world/birth-inputs.json').read_text())
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][n]==prior['artifacts'][n]
 assert tested['copiedData'].keys()==prior['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
 current=next(x for x in status['skills'] if x['talentId']=='sorceress.teleport');old=next(x for x in before['skills'] if x['talentId']=='sorceress.teleport')
 assert old['verification']=='legacy-checks; rerun-needed' and current['verification']=='partial-native-coverage'
 for k in ('remaining','verification'):old[k]=current[k]
 assert status==before and not status['completedClasses']
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text())
  assert value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,digest in value['InputHashes'].items():assert sha(ROOT/p)==digest,p
  previous=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore}
  fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[905,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest,p
 files=[Path(__file__).resolve(),ROOT/'tests/check_player_teleport.py',ROOT/'tools/build_skill_roadmap.py',parent,
  *(ROOT/'docs'/n for n in ('sorceress-player-teleport-checkpoint.md','t33-teleport.md','t34-inscription.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','player-teleport-fixtures.json',
   'audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
  *(focused_dir/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{BUILD}.log','player-teleport-fixtures.json')),
  *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','Teleportation.cs','PlayerTeleportChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Teleport shared FP payment and approved player input/save acceptance',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=681,focusedMarkers=38,boots=3,reloads=2,saves=3,pinnedRankPrices=255,newServerWireActions=9,retainedServerWireActions=189,
   fixtureFamilies=48,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=[
   'The previous pushed DLL revives a clamped fractional mana remainder after a native regeneration step. Its exact native regression/source/DLL/inputs are preserved. Shared FP256 admission/payment fixes this while preserving usable fractions,once-only effect commitment and mana-free physical scroll use.',
   '255pinned rank costs,actual gear/FCR,release-only resource checks/cancellation/rank/equipment/connection/late obstruction/scroll ownership and approved18tile noLOS terrain contracts have controls. Nine actual left/right/quick casts cross an LOS-blocking wall and complete at the selected18tile point through natural action callbacks.',
   'Unpaid pending learned/scroll actions are staged immediately before actual saves;they do not resume. The same original native player retains logout map/point,gear,FP wounds/mana,effective rank,mouse/quick bindings and original backpack/belt scroll identities. Fresh natural casts complete after two process reloads without consuming those scrolls.',
   'Retained T33 terrain/travel/house/field/scroll/belt checks and Hydra/spell/damage regressions pass against the same immutable build. Four audits preserve decisions;71Python/573named/240catalogue/generators pass;106installed hashes unchanged.'],
  remaining=[
   'The approved Sanctuary18tile noLOS/native landing/timing and left/right/quick/scroll controls override differing D2 engine world/input policies. Current table suppliers and bounded native tests are not proof of complete modern D2R global reposition/path/grid/collision/event execution.',
   'Fresh graphical-client,charged/oskill,special native movement subscriptions and full old-DLL/unheld-save equivalence remain separate. Existing T33 client acceptance is historical. No class certificate or deployment is claimed;Inferno remains individually deferred and the class roadmap continues.',
   'Final cast/departure/arrival/body/audio and talent/left/right/quick artwork remain independent identified placeholders.'])
 out=ROOT/'tools/reference/player-teleport-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Teleport:681full/38focused markers,255rank costs,capped FP payment negative/positive,9new/189retained wire actions,two original saved actor/scroll/resource reloads,four audits/71Python;106installed hashes unchanged;no class completion/deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
