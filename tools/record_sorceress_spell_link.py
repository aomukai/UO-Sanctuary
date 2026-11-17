#!/usr/bin/env python3
"""Bind settled Sorceress/shared spell components to approved Health Link."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/sorceress-spell-link';BASE='f0a01510ede82e20ef658a8aa31ecdfe4344fde2'
sys.path.insert(0,str(ROOT/'tests'))
from check_sorceress_spell_link import configuration
TALENTS=['sorceress.'+n for n in ('firebolt','inferno','chargedbolt','lightning','icebolt','iceblast','nova','frostnova','chillingarmor','chainlightning','thunderstorm','fireball','glacialspike','staticfield')]+['druid.arcticblast','warlock.ringoffire','warlock.flamewave']

def markers():
 c=configuration();r=Counter()
 def add(n,count):
  r[n]+=count;results=c['extra_wait_results'].get(n,())
  for result in ((results,)if isinstance(results,str)else results):r[result]+=count
 add('iron-golem-birth-check',3)
 for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
 for n in c['extra_checks']:add(n,3)
 for n in c['extra_live_commands']:
  r[n+'-result']+=3
  if n in c['checked_live_commands']:r[n+'-check']+=3
 for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
 for n in c['extra_pre_save_commands']:r[n]+=3
 r[c['save_result']]+=3
 for n in (0,1,9):r[f'inferno-held-wire-{n}-result']+=3
 assert sum(r.values())==759
 return r

def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/blade-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==sorted(['src/Sanctuary.Content/SorceressSpellMissiles.cs','src/Sanctuary.Content/StaticFields.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/SorceressSpellLinkChecks.cs']),changed
 p='src/Sanctuary.Content/SorceressSpellMissiles.cs';old=prior(p);needle='SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total))';assert old.count(needle)==2
 assert (ROOT/p).read_text()==old.replace(needle,'SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts))')
 p='src/Sanctuary.Content/StaticFields.cs';assert (ROOT/p).read_text()==prior(p).replace('SkillHealth.Damage(caster,target,loss)','SkillHealth.TypedDamage(caster,target,loss,new(0,0,0,0,loss))',1)
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(prior('data/Sanctuary/Balance/class-skill-status.json'));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==sorted(TALENTS)
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240 and not new['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',2)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),759);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==302 and 'denied by connection filter'not in log
 assert log.count('Warning: Attempted to delete')==14
 assert sum(' WRN]'in line and 'Defiler link sharing refused unsupported context'in line for line in log.splitlines())==6
 assert log.count('Ground fire unsupported context:')==3 and log.count('Frozen Orb unsupported context:')==1
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:
  text=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'Spell firebolt/normal typed link:fault=Direct damage lacks settled typed components;primary=4096/4096;secondary=0/2170;events=1;typed=False'in text
 baseline=e.load(STAGE/'failed-baseline-1-native-inputs.json');assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 assert e.load(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 c=configuration();assert c['connected_save'] and c['save_command']==c['save_result']=='player-teleport-save'
 assert c['extra_pre_save_commands']==('inferno-held-input-save-stage-check',) and len(c['extra_target_input_commands'])==68
 files=[ROOT/'tools/record_sorceress_spell_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/sorceress-spell-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/sorceress-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*c['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1','failed-roadmap-generation-1'))]
 result=dict(component='Settled Sorceress/shared direct spell and final Static lightning components preserve approved Health Link',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=759,markers=dict(sorted(markers().items())),newLinkCasesPerBoot=67,affectedTalents=TALENTS,retainedOrdinaryWireInputs=204,retainedHeldWireSequences=9,newWireInputs=0,connections=log.count('Client: 127.0.0.1: Connected.'),boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,failedCorrectedNativeAttempts=0,roadmapGenerationFailures=1,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=log.count('Warning: Attempted to delete'),unsupportedContextWarnings=sum(' WRN]'in l and 'Defiler link sharing refused unsupported context'in l for l in log.splitlines()),protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Two common player spell receivers publish existing settled non-poison parts;Static publishes final clipped lightning only. NPC explosion branch,modifiers/status/reaction/recovery/source RNG/geometry/overlap/payment remain unchanged.67controlled cases perboot:14direct/shared keys*4contexts,2real explosion callbacks*4contexts,3Static contexts. New cases call shared receivers with controlled packets;retained full Sorceress/Arctic potion/mode/Warlock Ring and FlameWave world suites supply independent flight,clock,input and original-save checks. Existing Teleport connected save and held Inferno input preserved. No new Static potion semantics/full class/current engine/PvP/assets certification. Scalar/legacy/ground and exact-event gaps remain explicit. First corrected native attempt passes;older accepted DLL reproduced untyped FireBolt/full primary/zero share. One roadmap generator key failure frozen before repair;no hidden compiler/native retries.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/sorceress-spell-link')
 e.write('sorceress-spell-link-native-evidence.json',result,files,check);print('Sorceress/shared spell links:759results/67new cases perboot/204retained inputs/9held sequences/2reloads;remaining scope explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
