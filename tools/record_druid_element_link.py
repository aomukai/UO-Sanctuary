#!/usr/bin/env python3
"""Bind known Druid elemental channels while preserving periodic exclusions."""
import argparse,hashlib,json,re,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/druid-element-link';BASE='5e5f0926ef0f08f8110d60e4e666d8bb90db50ee'
sys.path.insert(0,str(ROOT/'tests'))
from check_druid_element_link import configuration
from check_druid_planned_acceptance import ELEMENT_INPUTS
TALENTS=['druid.'+n for n in ('twister','fissure','volcano','tornado','shockwave','moltenboulder')]
def expected_markers():
    config = configuration()
    expected = Counter()

    def add(command, count):
        expected[command] += count
        results = config['extra_wait_results'].get(command, ())
        for name in ((results,) if isinstance(results, str) else results):
            expected[name] += count

    add('iron-golem-birth-check', 3)
    for family in ('iron-golem-items', 'npc-resource-callbacks', *config['extra_families']):
        add(family + '-check', 1)
        add(family + '-reload-check', 2)
    for command in config['extra_checks']:
        add(command, 3)
    for command in config['extra_target_input_commands']:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    for command in config['extra_live_commands']:
        if command in config.get('checked_live_commands', ()):
            expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    for command in config['extra_pre_save_commands']:
        expected[command] += 3
    for name in ELEMENT_INPUTS:
        expected[name + '-input-check'] += 3
        expected[name + '-input-result'] += 3
    for name in ('shock-wave-live-check', 'shock-wave-live-result',
                 'arctic-held-input-save-stage-check', 'druid-fury-stage-check',
                 'druid-fury-stage-result'):
        expected[name] += 3
    for source in (0, 1, 9):
        expected[f'arctic-held-wire-{source}-result'] += 3
    assert sum(expected.values()) == 684
    return expected

def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/sorceress-spell-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 names=('DruidTwister','DruidFissure','DruidVolcano','DruidTornado','DruidShockWave','DruidMoltenBoulder')
 assert changed==sorted([*(f'src/Sanctuary.Content/{n}.cs'for n in names),'tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/DruidElementLinkChecks.cs']),changed
 for n in names:
  p=f'src/Sanctuary.Content/{n}.cs';old=prior(p)
  if n=='DruidMoltenBoulder':
   new=old.replace('SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total),s.Missile==455?SkillDamageKind.Periodic:SkillDamageKind.Direct)','SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts),s.Missile==455?SkillDamageKind.Periodic:SkillDamageKind.Direct)',1)
  else:
   new,count=re.subn(r'SkillHealth\.Damage\(owner,\s*target,\s*NamedElements\.To256\(parts.Total\)\)','SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts))',old)
   assert count==(2 if n=='DruidTornado'else 1)
  assert (ROOT/p).read_text()==new,p
 # Content1 predates only the repaired Integration fixture,not a gameplay change.
 before=e.load(STAGE/'source-snapshot-2.json')['inputs'];after=e.load(STAGE/'source-snapshot-5.json')['inputs']
 assert before['copiedData']==after['copiedData'] and before['projectInputs']==after['projectInputs']
 assert [p for p,d in before['sourceInputs'].items()if after['sourceInputs'][p]!=d]==['tests/Sanctuary.Integration/DruidElementLinkChecks.cs']
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(prior('data/Sanctuary/Balance/class-skill-status.json'));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==sorted(TALENTS)
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240 and not new['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',5)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,expected_markers(),684);assert 'pass across3boots/2reloads'in (STAGE/'native-run-3.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==204 and 'denied by connection filter'not in log
 assert log.count('Warning: Attempted to delete')==5
 assert sum(' WRN]'in line and 'Defiler link sharing refused unsupported context'in line for line in log.splitlines())==6
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:
  text=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'Druid twister/normal typed link:fault=Direct damage lacks settled typed components;primary=4096/4096;secondary=0/2170;events=1;typed=False'in text
 baseline=e.load(STAGE/'failed-baseline-1-native-inputs.json');assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 with tarfile.open(STAGE/'failed-native-run-1.tar.gz','r:gz')as t:
  text=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'Invalid Fissure crack damage/lifetime'in text
 failed=e.load(STAGE/'failed-native-run-1-native-inputs.json');assert failed['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 with tarfile.open(STAGE/'failed-native-run-2.tar.gz','r:gz')as t:
  assert 'Boulder link changed guarded source seed'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 with tarfile.open(STAGE/'failed-integration-build-4.tar.gz','r:gz')as t:
  assert 'CS0136'in t.extractfile(str(STAGE.relative_to(ROOT)/'integration-build-4.log')).read().decode()
 assert e.load(STAGE/'failed-native-run-2-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 c=configuration();assert len(c['extra_target_input_commands'])==43 and c['extra_pre_save_commands'] and c['extra_pre_save_callbacks']
 files=[ROOT/'tools/record_druid_element_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/druid-element-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/druid-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*c['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1','failed-native-run-1','failed-native-run-2','failed-integration-build-4'))]
 result=dict(component='Known Druid elemental damage preserves approved Health Link and periodic ground exclusion',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=684,markers=dict(sorted(expected_markers().items())),newDirectCasesPerBoot=32,newPeriodicControlsPerBoot=4,affectedTalents=TALENTS,retainedOrdinaryWireInputs=156,retainedHeldWireSequences=9,newWireInputs=0,connections=log.count('Client: 127.0.0.1: Connected.'),boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=5,sourceSnapshot=5,nativeRun=3,failedCorrectedNativeAttempts=2,fixtureCompileFailures=1,fixtureRepair='Original constructed Fissure crack had Cast==Id and noncanonical duration;repaired to CastId-1 and exactly83*40ms atFrame0. Then retained the source intrinsic rolling knockback RNG draw even under the uninterruptible guard;one duplicate-local compile failed before that correction built. Content1 unchanged throughout;all exact failed artifacts frozen.',nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=log.count('Warning: Attempted to delete'),unsupportedContextWarnings=sum(' WRN]'in l and 'Defiler link sharing refused unsupported context'in l for l in log.splitlines()),protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Seven known health calls across six spells publish existing settled non-poison parts. Boulder ground keeps Periodic kind and never starts sharing.32controlled direct cases plus4periodic controls perboot use actual prepared actors/private receivers,Fissure field burn,Tornado copied area and shared ShockWave receiver with controlled raw/modifier packets. Root/recipient exact FP8 loss and defenses,event source/channel/no recursion,immunity,potion poison exclusion,contact-level mana and existing seed checks pass. Scoped test uninterruptible hooks avoid unrelated knockback;retained source/flight/terrain/status/animation/input/save suites preserve those independent controls. All original Druid composed families retained,including beast/vine/form/held input/Fury save. No pet melee upgrade in this checkpoint,full class/current engine/PvP/assets certification or new numeric rules. Two frozen native fixture failures and one duplicate-local compile failure;no gameplay repair hidden.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/druid-element-link')
 e.write('druid-element-link-native-evidence.json',result,files,check);print('Druid elements:684results/32direct+4periodic cases perboot/156ordinary inputs/9held sequences/2reloads;pet melee remains separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
