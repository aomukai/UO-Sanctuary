#!/usr/bin/env python3
"""Bind shared physical spell components with preserved War Cry event order."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/warcry-link';BASE='0e18a8ce581d88b7daf25b6fc7ddd77b6e66630a'
sys.path.insert(0,str(ROOT/'tests'))
from check_warcry_link import configuration
def markers():
 c=configuration();r=Counter({'iron-golem-birth-check':3})
 def add(n,count):
  r[n]+=count;results=c.get('extra_wait_results',{}).get(n,())
  for result in ((results,)if isinstance(results,str)else results):r[result]+=count
 for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
 for n in c['extra_checks']:add(n,3)
 for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
 assert sum(r.values())==57
 return r
def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/armageddon-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==sorted(['src/Sanctuary.Content/BarbarianWarCry.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/WarCryLinkChecks.cs'])
 p='src/Sanctuary.Content/BarbarianWarCry.cs';old=prior(p);needle='SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total))';assert old.count(needle)==1
 assert (ROOT/p).read_text()==old.replace(needle,'SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts))',1)
 assert 'BarbarianWarCry.PhysicalHit(owner,target,values.PhysicalDamage256(false),values.PhysicalDamage256(true),AreaRoll)'in (ROOT/'src/Sanctuary.Content/BarbarianLeapAttack.cs').read_text()
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(prior('data/Sanctuary/Balance/class-skill-status.json'));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');talents=['barbarian.leapattack','barbarian.warcry']
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==talents
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240 and not new['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',2)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),57);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==12 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
 assert sum(' WRN]'in line and 'Defiler link sharing refused unsupported context'in line for line in log.splitlines())==6
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:
  assert 'War Cry stun/normal typed link:fault=Direct damage lacks settled typed components;primary=4097/4097;secondary=0/2171;events=1;typed=False;stun=True;draws=1'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 baseline=e.load(STAGE/'failed-baseline-1-native-inputs.json');assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 files=[ROOT/'tools/record_warcry_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/warcry-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/barbarian-coverage-matrix.md',ROOT/'docs/t29-combat-potions.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'src/Sanctuary.Content/BarbarianLeapAttack.cs',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1'))]
 result=dict(component='War Cry and approved Leap landing settled physical components reach Health Link without changing stun/defense order',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=57,markers=dict(sorted(markers().items())),newLinkContextsPerBoot=10,affectedTalents=talents,retainedLeapAttackWireInputs=9,newWireInputs=0,connections=12,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,failedCorrectedNativeAttempts=0,fixtureCompileFailures=0,driverPreflightFailures=0,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=0,intentionalScalarRefusalWarnings=6,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='One shared health call publishes existing settled physical parts. Ten controlled contexts cover WarCry actual Hit/stun callback and shared no-stun landing receiver x normal/primary resistance/recipient resistance/immunity/block;damage draw/order,exact FP8 loss/sharing,no recursion,stun before health and no extra mana retain. Temporary actual skeleton uses existing shield adapter;original Defiler group/shields/globals restored. Shared landing contact matrix bypasses flight;retained approved landing/weapon separation and nine actual left/right/quick inputs plus Leap/WarCry source/cast/ray/sequence/original-save tests cover it separately. No new WarCry wire-input claim. Known T29 WarCry potion additions are absent and newly flagged as unfinished integration under the generic approved spell rule,not an approved exclusion;shared Leap recipient must not receive a blanket change. No whole class/current-engine/PvP/assets certificate or numeric/AI/geometry/potion policy change.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/warcry-link')
 e.write('warcry-link-native-evidence.json',result,files,check);print('War Cry/shared landing:57results/10new link contexts perboot/9retained Leap inputs/2reloads;stun order retains,T29 gap explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
