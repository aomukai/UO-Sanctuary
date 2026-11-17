#!/usr/bin/env python3
"""Bind the current combined Necromancer cohort after shared damage repairs."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/necromancer-requalification';BASE='86f417de78395f7d49d636a9991abb15534d014f'
sys.path.insert(0,str(ROOT/'tests'))
from check_necromancer_requalification import configuration
def markers():
 p=e.load(ROOT/'tools/reference/necromancer-planned-acceptance-native-evidence.json');r=Counter(p['markers']);assert sum(r.values())==929
 for f in ('defiler-health-link','hellslayer-readiness'):
  r[f+'-check']+=1;r[f+'-reload-check']+=2
 r['defiler-health-link-check-result']+=1;r['defiler-health-link-reload-check-result']+=2
 assert sum(r.values())==938
 return r
def prior(p):return subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT,text=True)
def record(check=False):
 assert 'pass across3boots/2reloads'in (STAGE/'native-run-2.log').read_text(),'Combined run incomplete'
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/gear-strike-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==['tests/Sanctuary.Integration/MissileHistoryChecks.cs','tests/Sanctuary.Integration/PlagueJavelinChecks.cs','tests/Sanctuary.Integration/PlayerPierceFixtures.cs'],changed
 a=e.load(STAGE/'source-snapshot-1.json')['inputs'];b=e.load(STAGE/'source-snapshot-2.json')['inputs'];assert a['copiedData']==b['copiedData']==born['copiedData']==parent['copiedData']
 assert sorted(p for p,d in b['sourceInputs'].items()if a['sourceInputs'].get(p)!=d)==changed
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll'):assert born['artifacts'][p]==parent['artifacts'][p]
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]
 for k in ('Content','Integration'):assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 assert all(x in (STAGE/'integration-build-1.log').read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 with tarfile.open(ROOT/'.local/class-skills/gear-strike-link/accepted-assemblies-1.tar.gz','r:gz')as t:assert hashlib.sha256(t.extractfile('content/Sanctuary.Content.dll').read()).hexdigest()==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 failed=e.load(STAGE/'failed-native-run-1-native-inputs.json');assert failed['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll'];assert failed['Assemblies/Sanctuary.Integration.dll']==parent['artifacts']['Assemblies/Sanctuary.Integration.dll']!=born['artifacts']['Assemblies/Sanctuary.Integration.dll']
 with tarfile.open(STAGE/'failed-native-run-1.tar.gz','r:gz')as t:assert 'System.InvalidOperationException: Pierce budget'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 status='data/Sanctuary/Balance/class-skill-status.json';assert (ROOT/status).read_text()==prior(status);ledger=e.load(ROOT/status);rows=[r for r in ledger['skills']if r['characterClass']=='Necromancer'];assert len(rows)==30 and all(r['implementation']=='handler-present'for r in rows)and len(ledger['skills'])==240 and not ledger['completedClasses']
 log=e.cohort(STAGE,markers(),938);assert log.count('Client: 127.0.0.1: Connected.')==281 and 'denied by connection filter'not in log
 warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l];assert all('"WardrobeSet"'in l for l in warnings)
 assert e.load(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
 approval=e.load(ROOT/'tools/reference/mage-projectile-lifetime-approval.json');assert approval['approved']and not approval['implementationVerifiedAtDecision']
 frontier=e.load(ROOT/'tools/reference/blade-sentinel-flat-drain-frontier.json');assert frontier['status']=='reachable-unresolved'and not frontier['mechanicsChanged']
 for p,d in frontier['files'].items():assert e.sha(ROOT/p)==d,p
 old=e.load(ROOT/'.local/class-skills/necromancer-planned-acceptance/birth-inputs.json');since=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and old['sourceInputs'].get(p)!=d)
 files=[ROOT/'tools/record_necromancer_requalification.py',ROOT/status,ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/necromancer-coverage-matrix.md',ROOT/'docs/necromancer-requalification-checkpoint.md',ROOT/'docs/skill-effects.md',ROOT/'docs/skill-icons.md',ROOT/'docs/mage-projectile-lifetime-decision.md',ROOT/'docs/blade-sentinel-flat-drain-frontier.md',ROOT/'tools/reference/mage-projectile-lifetime-approval.json',ROOT/'tools/reference/blade-sentinel-flat-drain-frontier.json',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-native-run-1'))]
 result=dict(component='Current combined Necromancer requalification after shared damage repairs',referenceVersion='3.3.93847',baselineCommit=BASE,contentReusedFrom='gear-strike-link Content1',integrationBuild=1,sourceSnapshot=2,nativeRun=2,nativeMarkers=938,markers=dict(sorted(markers().items())),fixtureFamilies=122,sharedChecks=23,liveActions=111,inputActions=162,connections=281,boots=3,saves=3,reloads=2,saveDeletionWarnings=len(warnings),gameplayChangedDuringRequalification=False,runtimeDataChangedDuringRequalification=False,changedSources=changed,changedSourcesSincePreviousCombined=since,artifacts=born['artifacts'],contentScope=ledger['contentScope'],installed=False,classCertified=False,goalComplete=False,approvedPendingImplementation=['Skeletal Mage already-fired projectiles continue after Mage death;Necromancer restrictions retain'],protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Retains the original929exactmarkers/all30Necromancer routes and adds6Defiler/3readiness results. Existing dedicated edge scopes remain attributed separately. Two legacy fixture scenarios select a legal four-pierce action index instead of relying on obsolete RNG injection;all downstream budgets/contact/cost assertions retain. No gameplay/data/native rebuild. New Mage lifetime approval is recorded during this run and remains pending implementation;Sentinel actualdata path reachable but no new cast/approval claimed. Wholeclass/modern parity not certified;effects/audio/icons remain separate.')
 e.write('necromancer-requalification-native-evidence.json',result,files,check)
 print('Current Necromancer:938exactresults,162inputs,281connections,2reloads;approvedMage-lifetime work remains explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
