#!/usr/bin/env python3
"""Bind approved ordinary-player Pierce action admission and current verification."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_pierce_action_policy import expected_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pierce-action-adoption';BASE='7e0795257cdf289d845fb8def85194927d227d46'
sys.path.insert(0,str(ROOT/'tests'))
from check_pierce_action_adoption import configuration
TALENTS=['amazon.magicarrow','amazon.multipleshot','amazon.firearrow','amazon.coldarrow','amazon.explodingarrow','amazon.icearrow','amazon.guidedarrow','amazon.freezingarrow','amazon.immolationarrow','amazon.strafe','amazon.poisonjavelin','amazon.lightningbolt','amazon.plaguejavelin','amazon.lightningfury','amazon.pierce']

def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/assassin-spell-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==['src/Sanctuary.Content/PlayerPierceActions.cs','tests/Sanctuary.Integration/PierceActionChecks.cs'],changed
 path='src/Sanctuary.Content/PlayerPierceActions.cs';old=subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
 prior='// Explicit candidate for the new-protocol owner-index mapping. Saved counter\n// rows never enroll an owner;online admission requires a separate approval.'
 current='// Approved Sanctuary player action-index mapping. Current Amazon callers\n// share one captured index per emitted fan/series;other producers stay separate.'
 assert (ROOT/path).read_text()==old.replace(prior,current).replace('Approved=>false','Approved=>true',1)
 fixture=(ROOT/'tests/Sanctuary.Integration/PierceActionChecks.cs').read_text();assert 'PlayerPierceActions.Candidate('not in fixture and 'Approved ordinary owner admission/default index'in fixture and 'Approved ordinary original owner admission'in fixture
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==sorted(TALENTS)
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240
 approval=e.load(ROOT/'tools/reference/pierce-action-policy-approval.json');assert approval['approved']
 assert hashlib.sha256(subprocess.check_output(['git','show',approval['testedCommit']+':'+approval['proposal']],cwd=ROOT)).hexdigest()==approval['proposalAtApprovalSha256']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind in ('Content','Integration'):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,expected_markers(),33);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==6 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 s=e.load(STAGE/'pierce-action-fixtures.json');assert s['Generation']==2 and s['Index']==3 and s['Life']>0 and s['Mana']==17 and s['Ammo']==99
 assert len(s['Gear'])==3 and s['Flight']['Owner']==s['Owner'] and s['Flight']['Ability']==28
 seed=(666<<32)|3;budget=0
 while budget<4:
  seed=0x6AC690C5*(seed&0xffffffff)+(seed>>32)
  if (seed&0xffffffff)%100>=56:break
  budget+=1
 assert s['Flight']['Pierces']==budget==2
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
 previous=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audit['Definitions']==previous['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
 files=[ROOT/'tools/record_pierce_action_adoption.py',ROOT/'tools/record_pierce_action_policy.py',ROOT/'tools/record_pierce_source_seed.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/pierce-action-adoption-checkpoint.md',ROOT/'docs/pierce-action-policy-proposal.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/amazon-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),STAGE/'accepted-assemblies-1.tar.gz']
 result=dict(component='Approved shared player Pierce action-index policy enabled for ordinary Amazon ranged routes',referenceVersion='3.3.93847',baselineCommit=BASE,policyStatus='approved-and-enabled',approval='tools/reference/pierce-action-policy-approval.json',nativeMarkers=33,markers=dict(sorted(expected_markers().items())),routedAmazonTalents=14,affectedStatusRows=TALENTS,retainedWireInputs=6,newWireInputs=0,connections=6,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,failedBuilds=0,failedNativeAttempts=0,knownWardrobeSaveWarnings=0,onlineEnabled=True,currentFixturesUseCandidateAdmission=False,newSavedFields=[],existingProfile=dict(name='SanctuaryPlayerPierce',priority=258,format=0),existingProfileVersionsChanged=False,releasedFlightFormatsChanged=False,nativeRebuilt=False,installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='User-approved initial0/saved uint counter/release commit/fan-Strafe mapping now applies through ordinary Amazon callers. Gameplay changes only approval gate/comment;no new caller,profile,field,serializer or numeric table. Current fixture no longer enrolls candidates and proves all14routes,critical-only launch draws,fan/series capture,cancel/invalid aim,stale/repeated tickets,atomic restore,unsigned wrap,original gear/procs/wounds/fractional mana/ammo/index/flight and6network inputs across2reloads. Retains936compiled seed vectors and generic learned Pierce continuation. Does not rerun allAmazon damage/input variants or certify current whole class. Other player producers,NPC index,allocation fault injection,allheld/left/PvP/fullitems/current-executable exactness/client/assets remain separate. Approved adaptation,not newly recovered current D2R callback behavior.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/pierce-action-adoption')
 e.write('pierce-action-adoption-native-evidence.json',result,files,check);print('Approved ordinary Pierce:33results/14routes/6inputs/2reloads;profile and existing resources retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
