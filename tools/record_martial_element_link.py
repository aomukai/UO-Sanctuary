#!/usr/bin/env python3
"""Bind known Martial cold/lightning components and approved Health Link delivery."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/martial-element-link';BASE='30b7575f682ea3847ec30462957b87ab964e7174'
sys.path.insert(0,str(ROOT/'tests'))
from check_martial_element_link import configuration
TALENTS=['assassin.bladesofice','assassin.clawsofthunder','assassin.phoenixstrike']

def markers():
 r=Counter({'iron-golem-birth-check':3})
 for f in ('iron-golem-items','npc-resource-callbacks','defiler-health-link','hellslayer-readiness','thunder-release','ice-release','phoenix-release'):r[f+'-check']=1;r[f+'-reload-check']=2
 r['defiler-health-link-check-result']=1;r['defiler-health-link-reload-check-result']=2
 for f in ('thunder-natural','ice-natural','phoenix-natural'):r[f+'-check']=3;r[f+'-result']=3
 return r

def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/pierce-action-adoption/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 expected=sorted([*(f'src/Sanctuary.Content/{n}.cs'for n in ('MartialCold','MartialLightning','MartialPhoenix')),'tests/Sanctuary.Integration/MartialElementLinkChecks.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs']);assert changed==expected,changed
 for name,parts in (('MartialCold','new(0,0,0,reduced.Damage256,0)'),('MartialLightning','new(0,0,0,0,reduced.Damage256)'),('MartialPhoenix','f.Cold?new(0,0,0,reduced.Damage256,0):new(0,0,0,0,reduced.Damage256)')):
  path=f'src/Sanctuary.Content/{name}.cs';old=subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
  assert (ROOT/path).read_text()==old.replace('SkillHealth.Damage(owner,target,reduced.Damage256)','SkillHealth.TypedDamage(owner,target,reduced.Damage256,'+parts+')',1)
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==sorted(TALENTS)
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',2)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),45);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==12 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
 negatives=[l for l in log.splitlines()if ' WRN]'in l and 'Defiler link sharing refused unsupported context'in l];assert len(negatives)==3 and all('Direct damage lacks settled typed components'in l for l in negatives)
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:
  text=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
  assert 'Martial ice/normal typed link:fault=Direct damage lacks settled typed components;primary=3840/3840;secondary=0/2035;events=1;typed=False'in text
 baseline=e.load(STAGE/'failed-baseline-1-native-inputs.json');assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 assert e.load(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
 previous=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audit['Definitions']==previous['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 files=[ROOT/'tools/record_martial_element_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/martial-element-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/assassin-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1'))]
 result=dict(component='Known Martial player cold/lightning contacts publish settled components to approved Health Link',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=45,markers=dict(sorted(markers().items())),newLinkCasesPerBoot=15,affectedTalents=TALENTS,retainedLiveCombatScenes=9,newWireInputs=0,connections=12,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,failedCorrectedNativeAttempts=0,negativeBaseline='Old accepted DLL:Ice primary3840/3840FP256,secondary0/2035,untyped event faults group;frozen before repair.',fixtureCompileFailures=0,driverPreflightFailures=0,knownWardrobeSaveWarnings=0,intentionalUntypedRefusalWarnings=3,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Three gameplay-line replacements supply final cold/lightning fields.15contact/link cases perboot coverIce/ThunderNova/Bolt/PhoenixLightning/Cold with normal/resistant-secondary/immune-primary behavior,full primary,typed source/target,nonrecursive sharing,no zero-damage event,returned RNG,160ms lease boundaries,no mana/charge changes at child boundary. Uses controlled packets and actual emitters,not new charge/input proof. Three retained source/release/parent/status/travel/ground/original-save cohorts and9connected normal-clock scenes pass. Existing NPC-specific paths,meteor FireImpact and periodic ground remain. No current full-class/client certification. Sentinel extra flat LifeDrain has no established typed classification;do not silently invent it. OtherBlade,fullitems/PvP/current-executable gaps and separate assets remain.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/martial-element-link')
 e.write('martial-element-link-native-evidence.json',result,files,check);print('Martial elemental links:45results/15new cases per boot/9retained live scenes/2reloads;three known elemental paths repaired.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
