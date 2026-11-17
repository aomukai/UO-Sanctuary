#!/usr/bin/env python3
"""Bind Armageddon terminal components and preserved periodic child behavior."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/armageddon-link';BASE='db9da68d94d05d854e680fcfe436170deec029f0'
sys.path.insert(0,str(ROOT/'tests'))
from check_armageddon_link import configuration
def markers():
 r=Counter({'iron-golem-birth-check':3,'defiler-health-link-check-result':1,'defiler-health-link-reload-check-result':2,'armageddon-live-check':3,'armageddon-live-result':3,'armageddon-input-check':3,'armageddon-input-result':3})
 for f in ('iron-golem-items','npc-resource-callbacks',*configuration()['extra_families']):r[f+'-check']=1;r[f+'-reload-check']=2
 assert sum(r.values())==33
 return r
def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/druid-pet-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==sorted(['src/Sanctuary.Content/DruidArmageddon.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/ArmageddonLinkChecks.cs'])
 p='src/Sanctuary.Content/DruidArmageddon.cs';old=prior(p);needle='SkillHealth.Damage(owner, target, NamedElements.To256(parts.Total));';assert old.count(needle)==1
 assert (ROOT/p).read_text()==old.replace(needle,'SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts));',1)
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(prior('data/Sanctuary/Balance/class-skill-status.json'));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert [b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b]==['druid.armageddon']
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240 and not new['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',2)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),33);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==9 and 'denied by connection filter'not in log
 assert log.count('Armageddon:55080complete-source endpoint comparisons pass')==3
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:
  text=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'Armageddon terminal/normal typed link:fault=Direct damage lacks settled typed components;primary=4096/4096;secondary=0/2170;events=1;typed=False'in text
 assert e.load(STAGE/'failed-baseline-1-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 files=[ROOT/'tools/record_armageddon_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/armageddon-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/druid-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1'))]
 result=dict(component='Armageddon terminal settled components reach approved Health Link while ground retains Periodic exclusion',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=33,markers=dict(sorted(markers().items())),newTerminalCasesPerBoot=4,newPeriodicControlsPerBoot=4,affectedTalents=['druid.armageddon'],retainedWireInputs=3,retainedNaturalLiveChecks=3,connections=9,boots=3,saves=3,reloads=2,cachedEndpointComparisonsPerBoot=55080,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,failedCorrectedNativeAttempts=0,fixtureCompileFailures=0,driverPreflightFailures=0,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=log.count('Warning: Attempted to delete'),unsupportedContextWarnings=sum(' WRN]'in l and 'Defiler link sharing refused unsupported context'in l for l in log.splitlines()),protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='One terminal health call publishes existing settled non-poison components. Primary totals,mitigation,potion poison share exclusion and independently captured child fire fields retain;ground health call stays Periodic and unchanged.8controlled contexts perboot use real actor factory/private terminal and ground advancement,exact root/share events/FP8 damage and no extra mana. Focused existing Armageddon endpoint/emitter/cast/expiry/geometry/malformed-worker/input/natural-weather and original actor/seed/wound/binding/unpaid-windup reload checks retained. No repeated full Druid forms/vines suites or old reference compiler. Other Defiler matrices retained in shared scene. First corrected native run passes without repair. No whole class/current-engine/PvP/assets certificate or numeric/AI/geometry policy change.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/armageddon-link')
 e.write('armageddon-link-native-evidence.json',result,files,check);print('Armageddon link:33results/8new direct-ground contexts perboot/3wire casts/3natural checks/2reloads;periodic exclusion retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
