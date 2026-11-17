#!/usr/bin/env python3
"""Bind Blade root-direct Health Link delivery while preserving item suppression."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/blade-link';BASE='f19be0173be533488dba044a5aac4ca15b55bb90'
sys.path.insert(0,str(ROOT/'tests'))
from check_blade_link import configuration
TALENTS=['assassin.bladeshield','assassin.bladesentinel','assassin.bladefury']

def markers():
 c=configuration();r=Counter({'iron-golem-birth-check':3})
 for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):r[f+'-check']=1;r[f+'-reload-check']=2
 for n in c['extra_checks']:r[n]+=3
 for n,results in c['extra_wait_results'].items():
  for result in ((results,)if isinstance(results,str)else results):r[result]+=r[n]
 for n in c['extra_target_input_commands']:r[n+'-check']=3;r[n+'-result']=3
 assert sum(r.values())==111
 return r

def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/martial-element-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 expected=sorted([*(f'src/Sanctuary.Content/{n}.cs'for n in ('BladeSentinelMissiles','BladeShields','SkillDamageEvents','SkillHealth','WeaponMissiles')),*(f'tests/Sanctuary.Integration/{n}.cs'for n in ('BladeLinkChecks','BladeMissileChecks','DefilerHealthLinkChecks'))]);assert changed==expected,changed
 p='src/Sanctuary.Content/SkillDamageEvents.cs';old=prior(p);assert (ROOT/p).read_text()==old.replace('SkillDamageKind kind,DirectDamageParts? parts=null)','SkillDamageKind kind,DirectDamageParts? parts=null,bool notifySuppressedDirect=false)').replace('&&CombatItemEvents.Allowed(source,target))','&&(notifySuppressedDirect||CombatItemEvents.Allowed(source,target)))')
 p='src/Sanctuary.Content/SkillHealth.cs';old=prior(p);new=old.replace('DirectDamageParts parts,SkillDamageKind kind=SkillDamageKind.Direct)\n        =>DamageCore(source,target,damage256,kind,parts);','DirectDamageParts parts,SkillDamageKind kind=SkillDamageKind.Direct,bool notifySuppressedDirect=false)\n        =>DamageCore(source,target,damage256,kind,parts,notifySuppressedDirect);').replace('SkillDamageKind kind,DirectDamageParts? parts)','SkillDamageKind kind,DirectDamageParts? parts,bool notifySuppressedDirect=false)').replace('SkillDamageEvents.Notify(source,target,damage256,kind,parts)','SkillDamageEvents.Notify(source,target,damage256,kind,parts,notifySuppressedDirect)');assert (ROOT/p).read_text()==new
 p='src/Sanctuary.Content/BladeShields.cs';assert (ROOT/p).read_text()==prior(p).replace('SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total))','SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts),notifySuppressedDirect:true)',1)
 p='src/Sanctuary.Content/WeaponMissiles.cs';assert (ROOT/p).read_text()==prior(p).replace(':SkillHealth.Damage(owner,selected,NamedElements.To256(parts.Total));',':SkillHealth.TypedDamage(owner,selected,NamedElements.To256(parts.Total),DirectDamageParts.From(parts));',1)
 p='src/Sanctuary.Content/BladeSentinelMissiles.cs';assert (ROOT/p).read_text()==prior(p).replace('int loss=SkillHealth.Damage(body,target,direct);','// Preserve scalar refusal while the extra flat NPC drain has no established channel.\n            int loss=state.Damage.Elements.LifeDrain>0?SkillHealth.Damage(body,target,direct):SkillHealth.TypedDamage(body,target,direct,DirectDamageParts.From(parts));',1)
 assert sum(p.read_text().count('notifySuppressedDirect:true')for p in (ROOT/'src/Sanctuary.Content').glob('*.cs'))==1
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(prior('data/Sanctuary/Balance/class-skill-status.json'));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==sorted(TALENTS)
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',2),('Integration',4)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),111);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==15 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
 negatives=[l for l in log.splitlines()if ' WRN]'in l and 'Defiler link sharing refused unsupported context'in l];assert len(negatives)==6 and all('Direct damage lacks settled typed components'in l for l in negatives)
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:
  text=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'Blade shield/normal typed link:fault=;primary=9361/9361;secondary=0/4961;events=0;typed=False'in text
 baseline=e.load(STAGE/'failed-baseline-1-native-inputs.json');assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 with tarfile.open(STAGE/'failed-integration-build-1.tar.gz','r:gz')as t:assert 'CS0128'in t.extractfile(str(STAGE.relative_to(ROOT)/'integration-build-2.log')).read().decode()
 assert e.load(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 files=[ROOT/'tools/record_blade_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/blade-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/assassin-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1','failed-integration-build-1'))]
 result=dict(component='Known Blade direct components and scoped root notification retain approved Health Link without item/secondary recursion',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=111,markers=dict(sorted(markers().items())),newPositiveLinkCasesPerBoot=9,newUnclassifiedDrainNegativesPerBoot=1,affectedTalents=TALENTS,retainedWireInputs=9,newWireInputs=0,connections=15,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=2,integrationBuild=4,sourceSnapshot=5,nativeRun=1,failedCorrectedNativeAttempts=0,negativeBaseline='Old accepted Shield DLL:primary9361/9361FP256,secondary0/4961,no direct event because item suppression also hid health notification;frozen before repair.',fixtureCompileFailures=1,driverPreflightFailures=0,knownWardrobeSaveWarnings=0,intentionalUntypedRefusalWarnings=6,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Default-false health-notification opt-in is used only for Shield root direct typed damage inside item suppression;kind/self-cost filters and default secondary suppression remain. Old Shield CB/LifeTap/item-kill negatives and new no-recursion matrix pass. Sentinel LifeDrain0 publishes known parts;positive extra flat NPC drain remains scalar and explicitly refused,with unchanged full primary/zero share negative control. Fury fallback publishes known parts;current other producers return via dedicated handlers and throwing/initial-poison expression stays exact.9positive Blade cases plus1drain negative perboot;controlled mixed/poison packets,actual Fury dispatch,Sentinel private receiver via reflection with retained real factory/movement/clock/RNG/lease checks. Existing source/weapon/poison/item event/actor/learned Sentinel/input/Fury travel/natural/original-save scopes pass. Two test helper visibility changes reuse setup. One frozen duplicate-local compile;Content1 was an intermediate success before Fury/data edit. No full class/client certificate;unclassified drain,hostile/other scalars,fullitems/PvP/current-executable and separate assets remain.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/blade-link')
 e.write('blade-link-native-evidence.json',result,files,check);print('Blade links:111results/9positive+1drain negative perboot/9retained inputs/2reloads;item and secondary suppression retain.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
