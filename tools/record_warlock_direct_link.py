#!/usr/bin/env python3
"""Bind four known Warlock direct receivers to approved Health Link."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/warlock-direct-link';BASE='084a1c49e0a97f817a3cd62b5f43fa74953848a9'
sys.path.insert(0,str(ROOT/'tests'))
from check_warlock_direct_link import configuration
def markers():
 c=configuration();r=Counter({'iron-golem-birth-check':3})
 def add(n,count):
  r[n]+=count;results=c.get('extra_wait_results',{}).get(n,())
  for result in ((results,)if isinstance(results,str)else results):r[result]+=count
 for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
 for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
 assert sum(r.values())==141
 return r
def prior(p):return subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/warcry-potion/birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 production=['src/Sanctuary.Content/'+n+'.cs'for n in ('ApocalypseImpact','BladeWarp','HexPurgeReactionProposal','SigilDeathProposal')]
 tests=['tests/Sanctuary.Integration/'+n+'.cs'for n in ('DefilerHealthLinkChecks','HexPurgeReactionChecks','HexPurgeCastChecks','WeaponLandedChecks','WarlockDirectLinkChecks')]
 assert changed==sorted(production+tests),changed
 a=e.load(STAGE/'source-snapshot-3.json')['inputs'];b=e.load(STAGE/'source-snapshot-5.json')['inputs'];assert a['copiedData']==b['copiedData']
 assert sorted(p for p,d in a['sourceInputs'].items()if b['sourceInputs'][p]!=d)==['tests/Sanctuary.Integration/HexPurgeCastChecks.cs','tests/Sanctuary.Integration/WeaponLandedChecks.cs']
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(prior('data/Sanctuary/Balance/class-skill-status.json'));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');talents=['warlock.apocalypse','warlock.bladewarp','warlock.hexpurge','warlock.sigildeath']
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==talents
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240 and not new['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',5)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),141);assert 'pass across3boots/2reloads'in (STAGE/'native-run-4.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==51 and 'denied by connection filter'not in log
 assert sum(' WRN]'in line and 'Defiler link sharing refused unsupported context'in line for line in log.splitlines())==6
 optins=[p.name for p in (ROOT/'src/Sanctuary.Content').glob('*.cs')if 'notifySuppressedDirect:true'in p.read_text()];assert sorted(optins)==['BladeShields.cs','HexPurgeReactionProposal.cs','SigilDeathProposal.cs'],optins
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:assert 'Warlock apocalypse/normal typed link:fault=Direct damage lacks settled typed components;primary=4300/4300;secondary=0/2279;events=1;typed=False;suppressed=False'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 baseline=e.load(STAGE/'failed-baseline-1-native-inputs.json');assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 for n in (2,3):assert e.load(STAGE/f'failed-native-run-{n}-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 for n,message in ((2,'packets=2'),(3,"Damage arrived before the applying hit's mark")):
  with tarfile.open(STAGE/f'failed-native-run-{n}.tar.gz','r:gz')as t:assert message in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 archives=['accepted-assemblies-1','failed-baseline-1','failed-integration-build-1','failed-driver-preflight-1','failed-native-run-2','failed-native-run-3']
 files=[ROOT/'tools/record_warlock_direct_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/warlock-direct-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/warlock-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in archives)]
 result=dict(component='Apocalypse,Blade Warp,Hex Purge and Sigil Death settled non-poison components reach approved Health Link',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=141,markers=dict(sorted(markers().items())),newLinkContextsPerBoot=18,affectedTalents=talents,wireInputs=33,connections=51,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,contentSourceSnapshot=3,integrationBuild=5,sourceSnapshot=5,nativeRun=4,failedCorrectedNativeAttempts=2,fixtureCompileFailures=1,driverPreflightFailures=1,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=log.count('Warning: Attempted to delete'),intentionalScalarRefusalWarnings=6,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Four terminal health calls carry existing settled non-poison parts;Purge/Sigil roots opt into health notifications inside retained item suppression,with default-suppressed secondary shares and no recursive Purge/item reactions.18controlled real-receiver contexts perboot cover allfour x normal/potion/primary/receiver defenses plus Apocalypse immunity/allocation refusal. Controlled impact setup is not claimed as actual cast input;33retained left/right/quick socket inputs and existing source/cast/corpse/charge/loot/seed/saved-original fixtures independently retain admission and persistence checks. Two older observer assumptions repaired after frozen failures,gameplay DLL unchanged. No numeric/save-format/native/installed/class-certification change.')
 e.write('warlock-direct-link-native-evidence.json',result,files,check)
 print('Four Warlock direct receivers:141results,18controlledcontexts/boot,33inputs,51connections,2reloads;root/secondary suppression explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
