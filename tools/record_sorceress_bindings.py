#!/usr/bin/env python3
"""Bind Sorceress slot-policy review to exact native,legacy and retained evidence."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from build_sorceress_bindings import build as reference
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-bindings';BUILD=2
PARENT='ae2106787d301da1d0fcc050d4159aa32b1a9be4';sha=inventory.sha
def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
 assert sum(counts.values())==684 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_sorceress_bindings import configuration
 c=configuration(True);assert len(c['extra_families'])==49 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==66
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['player-teleport-save']==3 and counts['player-teleport-reload-result']==2
 assert 'Sorceress bindings:30pinned rows,60mouse checks,direct/passive/quick availability,18legacy migrations/idempotence' in log
 saved=json.loads((STAGE/'sorceress-binding-fixtures.json').read_text());assert saved['Generation']==2 and saved['Owner']>0
 ref=ROOT/'tools/reference/sorceress-binding-reference.json';assert json.loads(ref.read_text())==reference()
 fd=STAGE/f'focused-accepted-{BUILD}';focus=inventory.validate_inventory(fd/'birth-inputs.json')
 assert focus['artifacts']==tested['artifacts'] and focus['copiedData']==tested['copiedData']
 flog=(fd/'birth-native-checks.log').read_text();assert sum(markers(fd/'birth-native-checks.log').values())==15 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert markers(fd/'birth-native-checks.log')['necromancer-binding-reload-check']==2
 assert 'pass across3boots/2reloads' in (fd/f'focused-run-{BUILD}.log').read_text()
 for kind in ('content','integration'):
  text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  path=f'{kind}/Sanctuary.{kind.title()}.dll';assert sha(STAGE/path)==sha(fd/path)
 nd=STAGE/'negative-mouse-policy';ni=json.loads((nd/'birth-inputs.json').read_text())
 assert 'Sorceress mouse policy differs: Frost Nova' in (nd/'birth-native-checks.log').read_text() and 'sorceress-binding-check: failed' in (nd/'birth-native-checks.log').read_text()
 assert (nd/'SkillBindings.cs').read_bytes()==historical('src/Sanctuary.Content/SkillBindings.cs')
 assert sha(nd/'SkillBindings.cs')==ni['sourceInputs']['src/Sanctuary.Content/SkillBindings.cs']
 assert sha(nd/'SorceressBindingChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/SorceressBindingChecks.cs']==sha(ROOT/'tests/Sanctuary.Integration/SorceressBindingChecks.cs')
 assert sha(nd/'Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 parent=ROOT/'tools/reference/player-teleport-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 old=json.loads((ROOT/'.local/class-skills/player-teleport/birth-inputs.json').read_text())
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert old['artifacts'][n]==tested['artifacts'][n]
 assert old['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if old['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
 changed={'sorceress.'+n for n in ('frostnova','nova','staticfield','telekinesis','thunderstorm','hydra')}
 seen=set()
 for now,prior in zip(status['skills'],before['skills'],strict=True):
  if now!=prior:assert now['talentId'] in changed;seen.add(now['talentId']);prior['remaining']=now['remaining']
 assert seen==changed and status==before and not status['completedClasses']
 sorc=[x for x in status['skills'] if x['characterClass']=='Sorceress'];assert len(sorc)==30 and sum(x['implementation']=='handler-present' for x in sorc)==29
 assert next(x for x in sorc if x['talentId']=='sorceress.inferno')['implementation']=='blocked'
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text())
  assert value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,h in value['InputHashes'].items():assert sha(ROOT/p)==h,p
  previous=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[905,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.','Sorceress:30pinned mouse rows;approved Teleport left exception retained.'):assert message in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
 files=[Path(__file__).resolve(),ROOT/'tools/build_sorceress_bindings.py',ROOT/'tests/check_sorceress_bindings.py',ROOT/'tools/build_skill_roadmap.py',ref,parent,
  *(ROOT/'docs'/n for n in ('sorceress-coverage-review.md','t33-teleport.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','sorceress-binding-fixtures.json','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
  *(fd/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{BUILD}.log','sorceress-binding-fixtures.json','necromancer-binding-fixtures.json')),
  *(nd/n for n in ('birth-inputs.json','birth-native-checks.log','SkillBindings.cs','SorceressBindingChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Sorceress30talent coverage and mouse-binding normalization',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=684,focusedMarkers=15,boots=3,reloads=2,saves=3,pinnedSkills=30,mousePolicyChecks=60,legacyMigrations=18,newServerWireActions=0,retainedServerWireActions=198,fixtureFamilies=49,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The previous pushed DLL fails the pinned Frost Nova left-slot regression. Shared mouse admission,UI buttons,canonical reads and post-load normalization now use all30Sorceress table rows,with the approved T33Teleport left exception.',
  'Six previously unrestricted right-only skills now reject left assignment. Passive/unregistered availability stays independent;quick casting keeps its own availability gate. Eighteen version0/1migration contexts preserve valid right/quick,normalize forbidden left and retain valid Fire Bolt/Teleport left;serialization is idempotent.',
  'Original valid Sorceress bindings persist through two process reloads. Focused Necromancer binding regression passes alongside the Sorceress policy. The full retained spell/Teleport/Hydra cohort keeps198wire actions;four audits preserve decisions,71Python/573named/240catalogue/generators pass;106installed hashes unchanged.'],
  remaining=['Sorceress has29handlers/25active/fourpassive talents;Inferno remains individually gated by its recorded modern channel/input/payment-frequency questions. The30talent review records bounded scopes,not a class completion certificate.',
  'Per-skill modern engine/world/path/collision/event/lifecycle,special/PvP/charged/oskill,graphical-client and full old-DLL/unheld-save limits remain documented. These binding controls do not establish damage or animation parity.',
  'Final effects/audio and talent/hotkey artwork remain independent placeholders. This input-policy correction introduces no new effect or icon. No installation or save reset.'])
 out=ROOT/'tools/reference/sorceress-binding-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Sorceress review:684full/15focused markers,30rows/60mouse checks/18legacy migrations,two original binding reloads,198retained wire actions,four audits/71Python;106installed hashes unchanged;Inferno deferred,no class certificate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
