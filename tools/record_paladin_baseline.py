#!/usr/bin/env python3
"""Bind the Paladin baseline,slot correction and retained cohort to exact artifacts."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from build_paladin_bindings import build as reference
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/paladin-baseline';BUILD=3
PARENT='4e592abddea632cea5e2497c4cb7ff1ced7d2082';sha=inventory.sha
def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
 assert sum(counts.values())==785 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_paladin_baseline import configuration,FAMILIES,ASYNC
 c=configuration(True);assert len(c['extra_families'])==69 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==66
 for n in c['extra_families']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2,n
 for n in c['extra_checks']:assert counts[n]==3,n
 for n in c['extra_target_input_commands']:
  assert counts[n+'-check']==counts[n+'-result']==3,n
  assert log.count(' '+n+': windup')==3,n
 for n in ASYNC:assert counts[n+'-result']==1 and counts[n+'-reload-result']==2,n
 assert counts['paladin-binding-check']==1 and counts['paladin-binding-reload-check']==2
 assert 'Paladin bindings:30pinned rows,60mouse checks,direct/quick availability,18legacy migrations/idempotence' in log
 ref=ROOT/'tools/reference/paladin-binding-reference.json';assert json.loads(ref.read_text())==reference()
 saved=json.loads((STAGE/'paladin-binding-fixtures.json').read_text());assert saved['Generation']==2 and saved['Owner']>0
 fd=STAGE/f'focused-accepted-{BUILD}';focused=inventory.validate_inventory(fd/'birth-inputs.json')
 assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
 flog=(fd/'birth-native-checks.log').read_text();assert sum(markers(fd/'birth-native-checks.log').values())==110 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (fd/'run-3b.log').read_text()
 for n in FAMILIES:assert markers(fd/'birth-native-checks.log')[n+'-check']==1 and markers(fd/'birth-native-checks.log')[n+'-reload-check']==2,n
 for part in ('content','integration'):
  text=(STAGE/f'{part}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  n=f'{part}/Sanctuary.{part.title()}.dll';assert sha(STAGE/n)==sha(fd/n)
 nd=STAGE/'negative-binding-policy';ni=json.loads((nd/'birth-inputs.json').read_text())
 assert 'Paladin mouse policy differs: Prayer' in (nd/'birth-native-checks.log').read_text() and 'paladin-binding-check: failed' in (nd/'birth-native-checks.log').read_text()
 assert (nd/'SkillBindings.cs').read_bytes()==historical('src/Sanctuary.Content/SkillBindings.cs')
 assert sha(nd/'SkillBindings.cs')==ni['sourceInputs']['src/Sanctuary.Content/SkillBindings.cs']
 assert sha(nd/'PaladinBindingChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/PaladinBindingChecks.cs']==sha(ROOT/'tests/Sanctuary.Integration/PaladinBindingChecks.cs')
 assert sha(nd/'Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 baseline=STAGE/'baseline-accepted-2';blog=(baseline/'birth-native-checks.log').read_text()
 assert sum(markers(baseline/'birth-native-checks.log').values())==107 and ': failed' not in blog and ' ERR]' not in blog
 assert blog.count('Shutting down')==blog.count('Writing world save snapshot done')==3
 assert 'Golem additive aura damage' in (STAGE/'failed-baseline-0/birth-native-checks.log').read_text()
 assert 'Lower Resist fixture' in (STAGE/'failed-baseline-1/birth-native-checks.log').read_text()
 parent=ROOT/'tools/reference/sorceress-binding-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 old=json.loads((ROOT/'.local/class-skills/sorceress-bindings/birth-inputs.json').read_text())
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert old['artifacts'][n]==tested['artifacts'][n]
 assert old['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if old['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=set()
 for now,prior in zip(status['skills'],before['skills'],strict=True):
  if now!=prior:
   assert now['talentId'] in {'paladin.thorns','paladin.conviction','paladin.blessedhammer'} and prior['verification']=='build-only' and now['verification']=='partial-native-coverage'
   changed.add(now['talentId']);prior['verification']=now['verification']
 assert len(changed)==3 and status==before and not status['completedClasses']
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
 for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/n).read_text());assert value==json.loads((ROOT/'tools/reference'/n).read_text())
  assert value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,h in value['InputHashes'].items():assert sha(ROOT/p)==h,p
  previous=json.loads(historical('tools/reference/'+n));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[905,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.','Paladin:30pinned mouse rows;right-only auras and Holy Shield retained.'):assert message in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
 files=[Path(__file__).resolve(),ROOT/'tools/build_paladin_bindings.py',ROOT/'tests/check_paladin_baseline.py',ROOT/'tools/build_skill_roadmap.py',ref,parent,
  *(ROOT/'docs'/n for n in ('paladin-baseline-checkpoint.md','clay-upgrades-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','paladin-binding-fixtures.json','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
  *(fd/n for n in ('birth-inputs.json','birth-native-checks.log','run-3b.log','paladin-binding-fixtures.json')),
  *(nd/n for n in ('birth-inputs.json','birth-native-checks.log','SkillBindings.cs','PaladinBindingChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll')),
  *(baseline/n for n in ('birth-inputs.json','birth-native-checks.log','run-2.log')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Paladin30talent baseline and shared mouse/save policy',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=785,focusedMarkers=110,originalBaselineMarkers=107,boots=3,reloads=2,saves=3,pinnedSkills=30,mousePolicyChecks=60,legacyMigrations=18,newServerWireActions=0,retainedServerWireActions=198,fixtureFamilies=69,sharedChecks=10,paladinFamilies=20,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The existing19Paladin aura/melee/projectile families retain107native markers across three boots/two reloads. Clay\'s fixture uses the approved Skeleton/Golem Mastery curve and exact Might endpoints;Conviction\'s Lower Resist interaction uses a mapped species while preserving immunity/pierce/defense arithmetic and unclassified boundary controls.',
  'The prior DLL fails Prayer\'s left-slot policy regression. Paladin now shares the table validator for assignment/menu/canonical reads/post-load normalization.30rows/60flags,18version0/1migrations,idempotent encoding and two original valid-binding reloads pass without changing gameplay costs/timing/damage or save format.',
  'The20family focused Paladin cohort and retained Sorceress/resource/damage/spell/input cohort pass against one immutable build. Four audits preserve decisions;71Python/573named/240catalogue/generators pass;106installed hashes unchanged. Existing build-only labels forThorns/Conviction/Blessed Hammer now record partial native coverage;no class certificate is issued.'],
  remaining=['Baseline scopes remain bounded native/direct fixtures;no new Paladin wire or graphical-client certificate is claimed. Modern source/world/terrain/event/NPC damage/mode,special/PvP/charged/oskill and full old-DLL/unheld-save equivalence remain per-skill work.',
  'Unclassified native UO species still have no invented D2curse-trait fallback. The mapped Lower Resist interaction does not claim broader Ogre/species support. Approved pet damage and other Sanctuary deviations remain authoritative.',
  'Continue Paladin skill-by-skill acceptance starting with Sacrifice and shared ordered damage reuse. Final effects/audio and talent/hotkey icons remain independent placeholders;the binding fix adds no new asset. No installation or save reset.'])
 out=ROOT/'tools/reference/paladin-baseline-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Paladin baseline:785full/110focused markers,20Paladin families/30rows/18legacy migrations,two reloads,198retained wire actions,four audits/71Python;106installed hashes unchanged;no class completion/deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
