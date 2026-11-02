#!/usr/bin/env python3
"""Record learned Blade Sentinel input and retained source actor/missile acceptance."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blade-cast';sha=inventory.sha
PARENT='14093f3a51e83d5af6b588cab0cf697fd7eb605a'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-3.json',STAGE/'birth-inputs.json')
 sys.path.insert(0,str(ROOT/'tests'));from check_blade_cast import configuration
 c=configuration();assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(7,9,3)
 log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==87
 assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
 for family in c['extra_families']:assert log.count(family+'-check: passed')==1 and log.count(family+'-reload-check: passed')==2,family
 for name in c['extra_checks']:assert log.count(name+': passed')==3,name
 for name in c['extra_target_input_commands']:
  assert log.count(name+'-check: passed')==log.count(name+'-result: passed')==log.count(name+': windup')==3,name
 for name,count in (('blade-cast-result',1),('blade-cast-reload-result',2),('blade-offline-result',3),('blade-missile-result',1),('blade-missile-reload-result',2),('blade-controller-result',1),('blade-controller-reload-result',2)):
  assert log.count(name+': passed')==count,name
 saved=json.loads((STAGE/'blade-cast-fixtures.json').read_text())
 assert saved['Generation']==2 and saved['Actor']['Owner']==saved['Owner'] and saved['Actor']['Creation']['Rank']==21
 assert saved['Missile']['Owner']==saved['Body'] and saved['Missile']['OriginalOwner']==saved['Owner'] and saved['Actor']['Creation']['MissileArmed']
 prior=json.loads((ROOT/'.local/class-skills/slow-missiles-cast/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
 changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'][p]!=h}
 assert changed=={'src/Sanctuary.Content/'+n for n in ('AssassinCombat.cs','ActionTiming.cs','ClassCombat.cs','BladeSentinelCasts.cs','BladeSentinelMissiles.cs','AssassinTrapRoster.cs')}
 changed={p for p,h in tested['copiedData'].items() if prior['copiedData'][p]!=h}
 assert changed=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/skill-effects.json'}
 for path in changed:
  name='data/Sanctuary/'+path.removeprefix('Data/Sanctuary/');old=json.loads(historical(name));new=json.loads((ROOT/name).read_text())
  if isinstance(old,dict):old=old['skills'];new=new['skills']
  assert [a['talentId'] for a,b in zip(old,new) if a!=b]==['assassin.bladesentinel']
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
 assert sum(x['implementation']=='handler-present' for x in status['skills'])==187 and not status['completedClasses']
 row=next(x for x in status['skills'] if x['talentId']=='assassin.bladesentinel');assert row['implementation']=='handler-present' and row['verification']=='partial-native-coverage' and not row['complete']
 for part in ('content','integration'):
  text=(STAGE/(part+'-build-3.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 negative=STAGE/'negative-parent-1';text=(negative/'birth-native-checks.log').read_text()
 assert 'blade-cast-availability-check: failed' in text and 'Learned Blade Sentinel is unavailable' in text
 assert sha(negative/'content/Sanctuary.Content.dll')==prior['artifacts']['Assemblies/Sanctuary.Content.dll']
 for name in ('AssassinCombat.cs','BladeSentinelCasts.cs'):assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
 diagnostic=STAGE/'diagnostic-ready-order-1';text=(diagnostic/'native-run-1.log').read_text()
 assert 'Server did not reach' in text and 'blade-cast-left: ready' in text
 dlog=(diagnostic/'birth-native-checks.log').read_text();assert dlog.index('blade-cast-left: ready')<dlog.index('blade-cast-left-check: passed') and 'blade-cast-result: passed' in dlog
 assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 for folder in (negative,diagnostic):
  inputs=json.loads((folder/'birth-inputs.json').read_text());assert sha(folder/'BladeCastChecks.cs')==inputs['sourceInputs']['tests/Sanctuary.Integration/BladeCastChecks.cs']
  assert sha(folder/'integration/Sanctuary.Integration.dll')==inputs['artifacts']['Assemblies/Sanctuary.Integration.dll']
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 assert audit['sourceInputs']=={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
 text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
 for name,count in (('named-item-runtime-audit.json',912),('first-loop-affix-audit.json',21)):
  new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text()) and len(new['InputHashes'])==count
  assert new['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,h in new['InputHashes'].items():assert sha(ROOT/path)==h,path
  old=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in new.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore}
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
 text=(STAGE/'catalogue-checks.log').read_text();assert all(s in text for s in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'))
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==h,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_blade_cast.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',
  *(ROOT/'docs'/n for n in ('assassin-blade-cast-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in ('pre-build-3.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','blade-cast-fixtures.json','content-build-3.log','integration-build-3.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','catalogue-checks.log'))]
 for folder in (negative,diagnostic):files.extend(folder/n for n in ('birth-inputs.json','birth-native-checks.log','BladeCastChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll'))
 files.extend(c['extra_files'].values());files.extend(c['extra_inputs'])
 data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Learned Blade Sentinel registered with scoped native acceptance; no complete class or current-engine certificate.',
  verified=dict(nativeCommandMarkers=87,boots=3,reloads=2,saves=3,wireActions=9,originalActorResumes=2,auditMarkers=2,pythonTests=71,handlerCount=187,assassinHandlers=18,unchangedInstalledArtifacts=106),
  limits=['Inspection clocks are held/rearmed for original actor/missile reload assertions; separate retained natural/offline expiry checks do not prove uninterrupted short lifetimes across downtime.',
   'Modern NPC drain/cold-sunder, source world/queue/NextHit/repair, complete elemental/event/item-granted/PvP/client behavior remain acceptance gaps. Known unsupported owner stats refuse before payment; no guessed modern replacement is introduced.',
   'Retired component status assertions only required disabled input; their numeric/source/actor/lifecycle assertions remain, with dedicated public input/history controls. Parent and ready-order diagnostic are preserved. No installed build/save mutation or final assets.'],
  tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
 out=ROOT/'tools/reference/blade-cast-native-evidence.json';text=json.dumps(data,indent=2)+'\n'
 if check:assert out.read_text()==text
 else:out.write_text(text)
 print('Learned Blade Sentinel:87native/3boots/2reloads/3saves;9wire actions/2original resumes;187handlers;2audits/71Python/106installed unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
