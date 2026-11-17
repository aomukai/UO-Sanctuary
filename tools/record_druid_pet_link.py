#!/usr/bin/env python3
"""Bind actual Druid pet channels while retaining the cached Grizzly health total."""
import argparse,hashlib,json,re,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/druid-pet-link';BASE='92dfee43314973bf5d14806214164e86cc8c2969'
sys.path.insert(0,str(ROOT/'tests'))
from check_druid_pet_link import configuration
from check_druid_planned_acceptance import ELEMENT_INPUTS
TALENTS=['druid.'+n for n in ('summonspiritwolf','summondirewolf','summongrizzly')]
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
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/druid-element-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-7.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 names=('DruidSpiritWolfHit','DruidDireWolfHit','DruidGrizzlyHit')
 assert changed==sorted([*(f'src/Sanctuary.Content/{n}.cs'for n in names),'tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/DruidPetLinkChecks.cs']),changed
 for n in names[:2]:
  p=f'src/Sanctuary.Content/{n}.cs';old=prior(p);needle='SkillHealth.Damage(body,target,NamedElements.To256(parts.Total))';assert old.count(needle)==1
  assert (ROOT/p).read_text()==old.replace(needle,'SkillHealth.TypedDamage(body,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts))')
 p='src/Sanctuary.Content/DruidGrizzlyHit.cs';old=prior(p);assert (ROOT/p).read_text()==old.replace('int lost=SkillHealth.Damage(body,target,packet.Total256);','// The cached total predates BearSmite\'s extra physical field. Its\n        // unchanged elemental lanes leave exactly the committed physical loss.\n        var direct=new DirectDamageParts(checked(packet.Total256-packet.Fire256-packet.Cold256-packet.Lightning256),0,packet.Fire256,packet.Cold256,packet.Lightning256);\n        int lost=SkillHealth.TypedDamage(body,target,packet.Total256,direct);',1)
 data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 old=json.loads(prior('data/Sanctuary/Balance/class-skill-status.json'));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==sorted(TALENTS)
 assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
 assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240 and not new['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',7)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,expected_markers(),684);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==204 and 'denied by connection filter'not in log
 assert log.count('Warning: Attempted to delete')==5
 assert sum(' WRN]'in line and 'Defiler link sharing refused unsupported context'in line for line in log.splitlines())==6
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-2.tar.gz','r:gz')as t:
  text=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'Druid pet spirit/normal typed link:fault=Direct damage lacks settled typed components;primary=921/921;secondary=0/488;events=1;typed=False;outcome=True;seed=True'in text
 baseline=e.load(STAGE/'failed-baseline-2-native-inputs.json');assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:
  assert 'Invalid source shield'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 for build,error in ((2,'CS0411'),(3,'CS1929'),(4,'CS1579')):
  with tarfile.open(STAGE/f'failed-integration-build-{build}.tar.gz','r:gz')as t:assert error in t.extractfile(str(STAGE.relative_to(ROOT)/f'integration-build-{build}.log')).read().decode()
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 c=configuration();assert len(c['extra_target_input_commands'])==43 and c['extra_pre_save_commands'] and c['extra_pre_save_callbacks']
 files=[ROOT/'tools/record_druid_pet_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/druid-pet-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/druid-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*c['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),ROOT/'src/Sanctuary.Content/DruidBearSmite.cs',*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1','failed-baseline-2','failed-integration-build-2','failed-integration-build-3','failed-integration-build-4'))]
 result=dict(component='Actual Druid pet damage retains known components and Grizzly cached health total for approved Health Link',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=684,markers=dict(sorted(expected_markers().items())),newPetCasesPerBoot=32,petRoutes=['SpiritWolf','DireWolf','GrizzlyOrdinary','BearSmite'],affectedTalents=TALENTS,retainedOrdinaryWireInputs=156,retainedHeldWireSequences=9,newWireInputs=0,connections=log.count('Client: 127.0.0.1: Connected.'),boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=7,sourceSnapshot=7,nativeRun=1,failedCorrectedNativeAttempts=0,fixtureCompileFailures=3,fixtureBaselineFailures=1,fixtureRepair='Native damage ledger requires Server.Collections foreach rather than LINQ Any/Cast. Initial shared zombie has no source shield variants;temporary skeleton receiver reuses existing shield routine and restores exact original Defiler group. Knockback states are isolated/restored. All failed compiler and fixture-baseline artifacts archived before repair;actual old-DLL SpiritWolf untyped fault separately reproduced.',nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=log.count('Warning: Attempted to delete'),unsupportedContextWarnings=sum(' WRN]'in l and 'Defiler link sharing refused unsupported context'in l for l in log.splitlines()),protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Spirit/Dire publish existing settled parts;Grizzly derives physical only from its known four-channel cached health total. Its later BearSmite physical addition remains on old leech/return/bookkeeping paths and never becomes extra health loss/sharing.32contexts perboot cover4actual NPC receivers x normal/Enchant/recipient resistance/primary resistance/immunity/critical/miss/block. Native owner responsibility ledger,source identity,RNG,FP8 primary/share mitigation/no recursion and no extra owner mana pass. Real prepared body/mode stats and hit services used;new contact matrix bypasses AI scheduling. Existing full Druid cohort retains source packets,sequences,upgrades,AI,captured ranks,parking,input/Fury and held-Arctic/original-save scopes. No new numeric/AI policy,pet potion inheritance,full class/current-engine/PvP/assets certificate. Existing scalar/legacy/ground and separate decisions remain explicit.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/druid-pet-link')
 e.write('druid-pet-link-native-evidence.json',result,files,check);print('Druid pet links:684results/32new NPC cases perboot/156ordinary inputs/9held sequences/2reloads;cached Grizzly total retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
