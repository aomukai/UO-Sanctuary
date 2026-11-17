#!/usr/bin/env python3
"""Bind War Cry T29 capture, live potion and bounded legacy saved rays."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/warcry-potion';BASE='68bbc8ecddc5f563be4b857b88246a5549e4497f'
sys.path.insert(0,str(ROOT/'tests'))
from check_warcry_potion import configuration
def markers():
 c=configuration();r=Counter({'iron-golem-birth-check':3})
 def add(n,count):
  r[n]+=count;results=c.get('extra_wait_results',{}).get(n,())
  for result in ((results,)if isinstance(results,str)else results):r[result]+=count
 for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
 for n in c['extra_checks']:add(n,3)
 for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
 assert sum(r.values())==60
 return r
def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/warcry-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-7.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==sorted(['src/Sanctuary.Content/BarbarianWarCry.cs','src/Sanctuary.Content/BarbarianShout.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/WarCryPotionChecks.cs'])
 a=e.load(STAGE/'source-snapshot-2.json')['inputs'];b=e.load(STAGE/'source-snapshot-7.json')['inputs'];assert a['copiedData']==b['copiedData']
 assert [p for p,d in a['sourceInputs'].items()if b['sourceInputs'][p]!=d]==['tests/Sanctuary.Integration/WarCryPotionChecks.cs']
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(prior('data/Sanctuary/Balance/class-skill-status.json'));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert [b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b]==['barbarian.warcry']
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240 and not new['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',6)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),60);assert 'pass across3boots/2reloads'in (STAGE/'native-run-5.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==12 and 'denied by connection filter'not in log
 assert log.count('Legacy War Cry ray')==1 and 'contact refused'in log
 assert sum(' WRN]'in l and 'Defiler link sharing refused unsupported context'in l for l in log.splitlines())==6
 saved=e.load(STAGE/'warcry-potion-fixtures.json');assert saved['Generation']==2 and saved['Ray']['Modifiers']['Potion']==0 and saved['Ray']['Modifiers']['ColdMastery']>0
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:assert 'T29 War Cry damage:25600/35840FP256'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 assert e.load(STAGE/'failed-baseline-1-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']
 for n in (1,2,3,4):assert e.load(STAGE/f'failed-native-run-{n}-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 with tarfile.open(STAGE/'failed-native-run-2.tar.gz','r:gz')as t:assert 'mana=27648/27631'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
 files=[ROOT/'tools/record_warcry_potion.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/warcry-potion-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/barbarian-coverage-matrix.md',ROOT/'docs/t29-combat-potions.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1',*(f'failed-native-run-{i}'for i in (1,2,3,4))))]
 result=dict(component='War Cry T29 additions use captured elemental modifiers and live potion without changing Leap landing',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=60,markers=dict(sorted(markers().items())),newPLinkContextsPerBoot=3,retainedLeapAttackWireInputs=9,newWireInputs=0,connections=12,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,contentSourceSnapshot=2,integrationBuild=6,sourceSnapshot=7,nativeRun=5,failedCorrectedNativeAttempts=4,fixtureCompileFailures=0,driverPreflightFailures=0,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=['Optional null-omitted ShoutRay.Modifiers;WarCry only,Potion0;profile140 remainsv0'],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=log.count('Warning: Attempted to delete'),intentionalScalarRefusalWarnings=6,intentionalLegacyPotionRefusals=1,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Captured elemental modifiers at release,Potion0;live impact-time potion uses existing shared addition/mitigation,combines components once and adds no chill/poison status. WarCry callback keeps stun/block/immune order;Leap shared receiver defaultsnull and explicit active-potion negative retains physical-only result. Real release payment,late drinking,unequipped actual Nightwing masteries,independent defenses,expiry/replacement,block,zero hit,new-field atomic rejection,legacy JSON/no-P base and active-P explicit refusal verified.3mixed typed/link contexts perboot omit added poison from sharing. Original held ray,item metadata/backpack,wound and fractional mana survive2reloads with postload boosted hits. New fields absent from old JSON;actual old-binary world upgrade remains unclaimed. Four exact frozen fixture save attempts:generic balance diagnostic,isolated regen guard then initialization-phase correction,and offline caster world resume/receiver normalization. SameContent1 throughout;no hidden gameplay repair. Full class/exact modern engine/PvP/items/assets remain separate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/warcry-potion')
 e.write('warcry-potion-native-evidence.json',result,files,check);print('War Cry T29:60results/3new mixed link contexts perboot/9retainedLeapinputs/2reloads;legacy active-potion refusal explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
