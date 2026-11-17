#!/usr/bin/env python3
"""Bind four Assassin typed spell contacts and retained native/input/save checks."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/assassin-spell-link';BASE='6aa5020c466a5c39abd8d934fdbc174f58e667d0'
sys.path.insert(0,str(ROOT/'tests'))
from check_assassin_spell_link import configuration,SPELLS
TALENTS=['assassin.fireblast','assassin.shockweb','assassin.psychichammer','assassin.mindblast']

def markers():
 r=Counter({'iron-golem-birth-check':3})
 for f in ('iron-golem-items','npc-resource-callbacks','defiler-health-link','hellslayer-readiness',*SPELLS):r[f+'-check']=1;r[f+'-reload-check']=2
 r['defiler-health-link-check-result']=1;r['defiler-health-link-reload-check-result']=2
 for f in SPELLS:r[f+'-result']=1;r[f+'-reload-result']=2;r[f+'-input-check']=3;r[f+'-input-result']=3
 return r

def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/sentry-missile-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
 a=e.load(STAGE/'source-snapshot-2.json')['inputs'];b=e.load(STAGE/'source-snapshot-4.json')['inputs']
 assert a['projectInputs']==b['projectInputs'] and a['copiedData']==b['copiedData']
 assert [p for p in b['sourceInputs']if a['sourceInputs'].get(p)!=b['sourceInputs'][p]]==['tests/Sanctuary.Integration/FireBlastChecks.cs']
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 expected=sorted([*(f'src/Sanctuary.Content/{n}.cs'for n in ('FireBlasts','ShockWebs','PsychicHammers','MindBlasts')),'tests/Sanctuary.Integration/AssassinSpellLinkChecks.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/FireBlastChecks.cs']);assert changed==expected,changed
 for n in ('FireBlasts','ShockWebs','PsychicHammers','MindBlasts'):
  path=f'src/Sanctuary.Content/{n}.cs';old=subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
  assert (ROOT/path).read_text()==old.replace('SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total))','SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts))',1)
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==sorted(TALENTS)
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',4)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),66);assert 'pass across3boots/2reloads'in (STAGE/'native-run-3.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==15 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
 negatives=[l for l in log.splitlines()if ' WRN]'in l and 'Defiler link sharing refused unsupported context'in l];assert len(negatives)==3 and all('Direct damage lacks settled typed components'in l for l in negatives)
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 for name,needle in (('failed-baseline-1','Assassin FireBlast/normal typed link:fault=Direct damage lacks settled typed components;primary=1190/1190;secondary=0/630;events=1;typed=False'),('failed-native-1','Resumed saved ground packet: actual=5119232, expected=5092095'),('failed-native-2','Saved Fire Blast recipient unavailable:position=(6005, 2046, 0),center=(6005, 2046, 0),alive=True,eligible=True')):
  with tarfile.open(STAGE/(name+'.tar.gz'),'r:gz')as t:assert needle in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
  original=e.load(STAGE/(name+'-native-inputs.json'))
  assert original['Assemblies/Sanctuary.Content.dll']==(parent if name=='failed-baseline-1'else born)['artifacts']['Assemblies/Sanctuary.Content.dll']
 assert e.load(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
 previous=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audit['Definitions']==previous['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 pierce=e.load(ROOT/'tools/reference/pierce-action-policy-approval.json');assert pierce['approved']
 assert hashlib.sha256(subprocess.check_output(['git','show',BASE+':'+pierce['proposal']],cwd=ROOT)).hexdigest()==pierce['proposalAtApprovalSha256'] and pierce['testedCommit']=='3dcd8c4bfb627a075022ec1e2686a9c9fd93c7f1'
 prior=subprocess.check_output(['git','show',BASE+':docs/pierce-action-policy-proposal.md'],cwd=ROOT);assert hashlib.sha256(prior).hexdigest()==pierce['proposalAtApprovalSha256']
 files=[Path(__file__).resolve(),ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/assassin-spell-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/assassin-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/pierce-action-policy-approval.json',ROOT/'docs/pierce-action-policy-proposal.md',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1','failed-native-1','failed-native-2'))]
 result=dict(component='Fire Blast/Shock Web/Psychic Hammer/Mind Blast publish settled components to approved Health Link',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=66,markers=dict(sorted(markers().items())),newLinkCasesPerBoot=14,successfulConversionCasesPerBoot=2,affectedTalents=TALENTS,retainedWireInputs=12,newWireInputs=0,connections=15,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=4,sourceSnapshot=4,nativeRun=3,failedCorrectedNativeAttempts=2,knownWardrobeSaveWarnings=0,intentionalUntypedRefusalWarnings=3,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Four gameplay-line substitutions retain primary/status/payment/order and publish known non-poison parts.14contact/link cases perboot include potion/resistant recipients and2successful Mind Blast conversions. Existing4standalone cast/ground/status/input/original-save scopes retain. First saved-Fire-Blast missed-hit failure lacks sufficient location/eligibility evidence for an exclusive cause;next explicit guard exposed missing movement-lock setup across reload. Native Skeleton identity,life and unchanged damage expectations retain;fixture locks are now restored on WorldLoad and exact recipient witnesses asserted. Content1 is identical across all3native attempts. Initial failure is preserved and no exact-engine conclusion invented. OtherAssassinMartial/Blade/currentclass/PvP/fullitems/client/assets remain separate. UserPierceapproval is recorded,not enabled by this checkpoint;its14ordinary Amazon routes are next.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/assassin-spell-link')
 e.write('assassin-spell-link-native-evidence.json',result,files,check);print('Assassin spell links:66results/14new cases per boot/12inputs/2reloads;frozen failures retained;Pierce approval recorded.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
