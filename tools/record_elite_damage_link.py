#!/usr/bin/env python3
"""Bind elite direct components and real periodic aura exclusions to current evidence."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/elite-damage-link';BASE='1fe6af58d3a8b001e7965824eda90d2e73627bdd'
sys.path.insert(0,str(ROOT/'tests'))
from check_elite_damage_link import configuration
def markers():
 c=configuration();r=Counter({'iron-golem-birth-check':3})
 def add(n,count):
  r[n]+=count;results=c.get('extra_wait_results',{}).get(n,())
  for result in ((results,)if isinstance(results,str)else results):r[result]+=count
 for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
 for n in c['extra_checks']:add(n,3)
 assert sum(r.values())==45
 return r
def prior(p):return subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/hostile-bolt-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==['src/Sanctuary.Content/EliteAuras.cs','src/Sanctuary.Content/EliteCombat.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/EliteDamageLinkChecks.cs'],changed
 a=e.load(STAGE/'source-snapshot-1.json')['inputs'];b=e.load(STAGE/'source-snapshot-2.json')['inputs'];assert a['copiedData']==b['copiedData']==born['copiedData']==parent['copiedData']
 assert [p for p,d in a['sourceInputs'].items()if b['sourceInputs'][p]!=d]==['src/Sanctuary.Content/EliteAuras.cs','src/Sanctuary.Content/EliteCombat.cs']
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 status='data/Sanctuary/Balance/class-skill-status.json';assert (ROOT/status).read_text()==prior(status);ledger=e.load(ROOT/status);assert len(ledger['skills'])==240 and not ledger['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',2)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),45);assert 'pass across3boots/2reloads'in (STAGE/'native-run-2.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==6 and 'denied by connection filter'not in log
 assert log.count('Warning: Attempted to delete')==1 and '"WardrobeSet" during world save'in log
 assert sum(' WRN]'in line and 'Defiler link sharing refused unsupported context'in line for line in log.splitlines())==6
 assert e.load(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
 assert e.load(STAGE/'slow-missiles-rules-fixtures.json')['Generation']==2
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:assert 'Elite direct/normal typed link:fault=Direct damage lacks settled typed components;primary=7648/7648;secondary=0/4051;events=1;typed=False'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 assert e.load(STAGE/'failed-baseline-1-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 failed=e.load(STAGE/'failed-native-run-1-native-inputs.json');assert failed==born['artifacts']
 with tarfile.open(STAGE/'failed-native-run-1.tar.gz','r:gz')as t:
  failedlog=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert ' ERR]'not in failedlog and ': failed'not in failedlog and 'cyclone-armor-check: passed'in failedlog
  assert 'FileNotFoundError'in t.extractfile(str(STAGE.relative_to(ROOT)/'native-run-1.log')).read().decode()
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 files=[ROOT/'tools/record_elite_damage_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/elite-damage-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/warlock-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/status,*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1','failed-native-run-1'))]
 result=dict(component='Existing discrete elite components reach approved Health Link;actual damaging aura pulses remain periodic',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=45,markers=dict(sorted(markers().items())),newLinkContextsPerBoot=19,newDirectContextsPerBoot=16,newActualAuraExclusionsPerBoot=3,wireInputs=0,connections=6,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,failedCorrectedNativeAttempts=0,driverExportFailures=1,fixtureCompileFailures=0,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=1,intentionalScalarRefusalWarnings=6,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Two gameplay files preserve shield/component math,sum/evaluation order and composite commit/callbacks. Direct is default for hits/bolts/death;actual HolyFire/Freeze/Shock caller supplies Periodic.19contexts/boot include16receiverflags x defenses/immunity with residualmagic retained plus3actual aura emitters with real Conversion allegiance,positive primary damage,zero notifications/shares and original positions/clock restoration. Matrix deathflag does not certify new NPC-to-NPC deathcandidate logic. Retained actualencounter modifiers/arbitration/projectiles/death/rewards/navigation,health/shields/Amplify/slowbolts/Cyclone and originalsaved actors/packets cover independent scopes. Driver failure only manifestexport aliases,identical source/data/DLLs afterward. GearEffects compatibility/override and SentinelextraNPCdrain remain classified separately;no wholeclass or modern parity claim.')
 e.write('elite-damage-link-native-evidence.json',result,files,check)
 print('Elite receiver:45results,16direct+3actualperiodiccases/boot,6connections,2reloads;no repeated source compiler or new input claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
