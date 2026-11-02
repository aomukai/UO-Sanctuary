#!/usr/bin/env python3
"""Bind prepared Slow Missiles release and atomic restore to disposable native evidence."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/slow-missiles-cast';sha=inventory.sha
PARENT='05f5722370e9318798bb55c2ee16478f876e4ed0'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-6.json',STAGE/'birth-inputs.json')
 log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==25
 assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
 for name in ('slow-missiles-cast','slow-missiles-component','slow-missiles-bolts'):
  assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
 assert log.count('slow-missiles-bolts-result: passed')==3 and log.count('slow-missiles-component-result: passed')==1
 assert log.count('slow-missiles-restore-check: passed')==3 and 'captured actual8bolt launches;availability remains gated' in log
 saved=json.loads((STAGE/'slow-missiles-cast-fixtures.json').read_text());assert saved['Generation']==2 and saved['Mana']==0
 prior=json.loads((ROOT/'.local/class-skills/barbarian-bindings/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
 changed={name for name,h in tested['copiedData'].items() if prior['copiedData'][name]!=h}
 assert changed=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/skill-effects.json'}
 for name in changed:
  path='data/Sanctuary/'+name.removeprefix('Data/Sanctuary/');old=json.loads(historical(path));new=json.loads((ROOT/path).read_text())
  if isinstance(old,dict):old=old['skills'];new=new['skills']
  assert [a['talentId'] for a,b in zip(old,new) if a!=b]==['amazon.slowmissiles']
 production={p for p in tested['sourceInputs'] if p.startswith(('src/','shared/'))};changed=set()
 for path in production:
  if path not in prior['sourceInputs'] or prior['sourceInputs'][path]!=tested['sourceInputs'][path]:changed.add(path)
 assert changed=={'src/Sanctuary.Content/AmazonSlowMissiles.cs','src/Sanctuary.Content/AmazonSlowMissilesCast.cs'}
 for part in ('content','integration'):
  s=(STAGE/(part+'-build-6.log')).read_text();assert all(v in s for v in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 negative=STAGE/'negative-parent-restore-1';s=(negative/'birth-native-checks.log').read_text()
 assert 'slow-missiles-restore-check: failed' in s and 'Slow Missiles restore was not atomic' in s
 assert sha(negative/'content/Sanctuary.Content.dll')==prior['artifacts']['Assemblies/Sanctuary.Content.dll']
 assert (negative/'AmazonSlowMissiles.cs').read_bytes()==historical('src/Sanctuary.Content/AmazonSlowMissiles.cs')
 for name in ('diagnostic-reload-1','diagnostic-first-mana-tick-2'):
  d=STAGE/name;inputs=json.loads((d/'birth-inputs.json').read_text());assert 'slow-missiles-cast-reload-check: failed' in (d/'birth-native-checks.log').read_text()
  assert sha(d/'SlowMissilesCastChecks.cs')==inputs['sourceInputs']['tests/Sanctuary.Integration/SlowMissilesCastChecks.cs']
  assert sha(d/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 assert 'mana=256,expectedMana=0' in (STAGE/'diagnostic-first-mana-tick-2/birth-native-checks.log').read_text()
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 assert audit['sourceInputs']=={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
 log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==2
 for name,count in (('named-item-runtime-audit.json',912),('first-loop-affix-audit.json',21)):
  new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text()) and len(new['InputHashes'])==count
  assert new['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,h in new['InputHashes'].items():assert sha(ROOT/path)==h,path
  old=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in new.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore}
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
 text=(STAGE/'catalogue-checks.log').read_text();assert all(v in text for v in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'))
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==h,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_slow_missiles_cast.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',
  *(ROOT/'docs'/n for n in ('amazon-slow-missiles-cast-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in ('pre-build-6.json','birth-inputs.json','birth-native-checks.log','native-run-3.log','slow-missiles-cast-fixtures.json','content-build-6.log','integration-build-6.log','audit-inputs.json','audit-native-checks.log','audit-run-3.log','python-tests.log','catalogue-checks.log'))]
 for name in ('negative-parent-restore-1','diagnostic-reload-1','diagnostic-first-mana-tick-2'):
  files.extend(STAGE/name/n for n in ('birth-inputs.json','birth-native-checks.log','SlowMissilesCastChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll'))
 data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Prepared release and atomic state restore; public talent still individually gated.',
  verified=dict(nativeCommandMarkers=25,boots=3,reloads=2,saves=3,auditMarkers=2,pythonTests=71,unchangedInstalledArtifacts=106),
  limits=['No learned SC/input availability, complete native hostile ranged/spell/breath/child consumers, exact modern damage order, complete source target/search geometry, item-granted/PvP/client or class certificate.',
   'Existing controlled source/observation components remain bounded. Eight-bolt launch check observes payload capture; retained encounter fixtures separately observe real timer-driven travel/damage.',
   'Saved new recipient deadline is held for inspection. Natural 12-second rank1 expiration is a separate retained fixture. Named constructor/reload mana interval is fixture-only; diagnostic production DLLs match accepted production.'],
  tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
 out=ROOT/'tools/reference/slow-missiles-cast-native-evidence.json';text=json.dumps(data,indent=2)+'\n'
 if check:assert out.read_text()==text
 else:out.write_text(text)
 print('Prepared Slow Missiles release:25native/3boots/2reloads/3saves;parent restore failure;2audits/71Python/106installed unchanged;handler remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
