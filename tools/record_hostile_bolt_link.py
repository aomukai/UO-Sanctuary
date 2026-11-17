#!/usr/bin/env python3
"""Bind NPC firebolt settled components and retained hostile flight defenses/saves."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/hostile-bolt-link';BASE='d099149b69fe64e07d2cb9466ae1e812039f04b3'
sys.path.insert(0,str(ROOT/'tests'))
from check_hostile_bolt_link import configuration
def markers():
 c=configuration();r=Counter({'iron-golem-birth-check':3})
 def add(n,count):
  r[n]+=count;results=c.get('extra_wait_results',{}).get(n,())
  for result in ((results,)if isinstance(results,str)else results):r[result]+=count
 for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
 assert sum(r.values())==33
 return r
def prior(p):return subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/warlock-direct-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==['src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/HostileBoltLinkChecks.cs'],changed
 a=e.load(STAGE/'source-snapshot-1.json')['inputs'];b=e.load(STAGE/'source-snapshot-2.json')['inputs'];c=e.load(STAGE/'source-snapshot-3.json')['inputs']
 assert a['copiedData']==b['copiedData']==c['copiedData']==born['copiedData']==parent['copiedData']
 assert [p for p,d in a['sourceInputs'].items()if b['sourceInputs'][p]!=d]==['src/Sanctuary.Content/WeaponMissiles.cs']
 assert [p for p,d in b['sourceInputs'].items()if c['sourceInputs'][p]!=d]==['tests/Sanctuary.Integration/HostileBoltLinkChecks.cs']
 path='src/Sanctuary.Content/WeaponMissiles.cs';old='int loss=SkillHealth.Damage(source,target,NamedElements.To256(parts.Total));';new='int loss=SkillHealth.TypedDamage(source,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts));';assert prior(path).count(old)==1 and (ROOT/path).read_text()==prior(path).replace(old,new)
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 status='data/Sanctuary/Balance/class-skill-status.json';assert (ROOT/status).read_text()==prior(status);ledger=e.load(ROOT/status);assert len(ledger['skills'])==240 and not ledger['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',3)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),33);assert 'pass across3boots/2reloads'in (STAGE/'native-run-2.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==6 and 'denied by connection filter'not in log
 assert log.count('Warning: Attempted to delete')==1 and '"WardrobeSet" during world save'in log
 assert sum(' WRN]'in line and 'Defiler link sharing refused unsupported context'in line for line in log.splitlines())==6
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:assert 'Hostile firebolt/normal typed link:fault=Direct damage lacks settled typed components;primary=2049/2049;secondary=0/1085;events=1;typed=False;draws=1'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 assert e.load(STAGE/'failed-baseline-1-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 assert e.load(STAGE/'failed-native-run-1-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 with tarfile.open(STAGE/'failed-native-run-1.tar.gz','r:gz')as t:assert 'Actual producer capture/clear'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 files=[ROOT/'tools/record_hostile_bolt_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/hostile-bolt-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/warlock-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/status,*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1','failed-native-run-1'))]
 result=dict(component='NPC firebolt existing settled components reach approved Health Link with retained hostile flight behavior',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=33,markers=dict(sorted(markers().items())),newLinkContextsPerBoot=5,wireInputs=0,connections=6,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],mechanicalRuntimeDataChanged=False,contentBuild=1,contentSourceSnapshot=2,integrationBuild=3,sourceSnapshot=3,nativeRun=2,failedCorrectedNativeAttempts=1,fixtureCompileFailures=0,driverPreflightFailures=0,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=1,intentionalScalarRefusalWarnings=6,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='One HostileImpact health call carries already-settled components with actual NPC source. Five controlled real-producer/terminal contexts cover primary and receiver defenses,immunity,captured slow after clear,one draw/pre-health notification/exact sharing/no secondary recursion/no extra mana. Terminal setup does not claim ordinary NPC-to-NPC target selection. Retained actual Lich windup/overlapping slowed flights,source freeze/interrupt,damage-before-avoidance,terrain/barrier/range,legacy flight JSON,Cyclone shields and original captured-flight save/reloads retain. No new learned input or whole-class certificate. Mixed elite aura/death and legacy player scalar consumers remain separately classified.')
 e.write('hostile-bolt-link-native-evidence.json',result,files,check)
 print('NPC firebolt:33results,5controlledcontexts/boot,actualLich/slow/shield/flight tests,6connections,2reloads.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
