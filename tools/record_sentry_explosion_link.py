#!/usr/bin/env python3
"""Bind Death Sentry's reproduced scalar fault and typed corpse contact repair."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/sentry-explosion-link';BASE='a81dc40792f3040452a90cd0b0eb7f34d53c1248'
sys.path.insert(0,str(ROOT/'tests'))
from check_sentry_explosion_link import configuration

def expected_markers():
    r=Counter({'iron-golem-birth-check':3,'death-learned-mechanics-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks','defiler-health-link','hellslayer-readiness','death-explosion','death-control','death-learned'):
        r[f+'-check']=1;r[f+'-reload-check']=2
    r['defiler-health-link-check-result']=1;r['defiler-health-link-reload-check-result']=2
    r['death-learned-result']=1;r['death-learned-reload-result']=2
    for mode in ('left','right','quick'):
        r['death-learned-'+mode+'-check']=3;r['death-learned-'+mode+'-result']=3
    return r

def archived_text(name,relative):
    with tarfile.open(STAGE/name,'r:gz')as a:return a.extractfile(str(STAGE.relative_to(ROOT)/relative)).read().decode()

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/mage-spell-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    before=e.load(STAGE/'source-snapshot-3.json')['inputs'];after=e.load(STAGE/'source-snapshot-4.json')['inputs']
    assert before['projectInputs']==after['projectInputs'] and before['copiedData']==after['copiedData']
    assert [p for p in after['sourceInputs'] if before['sourceInputs'].get(p)!=after['sourceInputs'][p]]==['tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs']
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AssassinDeathSentryCorpses.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs'],changed
    data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    old=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert [b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b]==['assassin.deathsentry']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
    assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240
    prior=subprocess.check_output(['git','show',BASE+':src/Sanctuary.Content/AssassinDeathSentryCorpses.cs'],cwd=ROOT,text=True)
    assert (ROOT/'src/Sanctuary.Content/AssassinDeathSentryCorpses.cs').read_text()==prior.replace('int loss=SkillHealth.Damage(actor,target,checked(physical.Damage256+fire.Damage256));','int loss=SkillHealth.TypedDamage(actor,target,checked(physical.Damage256+fire.Damage256),new(physical.Damage256,0,fire.Damage256,0,0));',1)
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for kind,build in (('Content',1),('Integration',4)):
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),51);assert 'pass across3boots/2reloads'in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==15 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
    negatives=[l for l in log.splitlines()if ' WRN]'in l and 'Defiler link sharing refused unsupported context'in l]
    assert len(negatives)==3 and all('Direct damage lacks settled typed components'in l for l in negatives)
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for kind in ('Content','Integration'):assert hashlib.sha256(a.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    assert 'primary=0/630630;secondary=0/334232;events=0;typed=False;targets=0'in archived_text('failed-baseline-1.tar.gz','birth-native-checks.log')
    assert 'Death Sentry/mixed typed link:fault=Direct damage lacks settled typed components;primary=630630/630630;secondary=0/334232;events=1;typed=False;targets=1'in archived_text('failed-baseline-2.tar.gz','birth-native-checks.log')
    assert 'primary=630630/630630;secondary=334232/334232;events=1;typed=True;targets=1'in archived_text('failed-native-1.tar.gz','birth-native-checks.log')
    baseline=e.load(STAGE/'failed-baseline-2-native-inputs.json');failed=e.load(STAGE/'failed-native-1-native-inputs.json')
    assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert failed['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll'] and failed['Assemblies/Sanctuary.Integration.dll']!=born['artifacts']['Assemblies/Sanctuary.Integration.dll']
    for name in ('defiler-health-link','death-learned'):assert e.load(STAGE/(name+'-fixtures.json'))['Generation']==2,name
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
    files=[ROOT/'tools/record_sentry_explosion_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/sentry-explosion-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/assassin-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/p for p in ('accepted-assemblies-1.tar.gz','failed-baseline-1.tar.gz','failed-baseline-2.tar.gz','failed-native-1.tar.gz'))]
    result=dict(component='Death Sentry settled physical/fire corpse impacts publish typed components to approved Defiler Health Link',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=51,markers=dict(sorted(expected_markers().items())),newLinkScenariosPerBoot=5,retainedNecromancerSpellScenariosPerBoot=9,retainedMageScenariosPerBoot=14,affectedTalents=['assassin.deathsentry'],retainedWireInputs=9,newWireInputs=0,connections=15,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=4,sourceSnapshot=4,nativeRun=2,failedCorrectedNativeAttempts=1,negativeBaseline='Old DLL:full primary630630FP256,secondary0instead of334232;one untyped event faults link. Frozen before repair.',fixturePlacementFailures=1,fixtureCompileFailures=0,driverPreflightFailures=0,recorderFailures=1,fixtureUnitFailure='Native1 damage/typed/link values correct;fixture compared WholeLoss displayed HP against FP256. Corrected in Integration4 without rebuilding Content1.',knownWardrobeSaveWarnings=0,intentionalUntypedRefusalWarnings=3,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='The sole gameplay line publishes already-settled physical/fire fields through existing typed health delivery. Primary damage,split-radius clipping,immunity,pierce,absorption,RNG,atomic corpse/loot and mana rules retain. Five actual linked corpse explosions per boot plus retained source packet/area/AI/controller/learned input/original-save checks pass. Existing9magic spell and14Mage link cases retain;standalone wider spell/Mage suites were not rerun. Sentry bolt scalar contacts remain the next integration gap. Modern exact callbacks/PvP/global world/full items/current class/client/assets retain separate limits. No new design approval required.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/sentry-explosion-link')
    e.write('sentry-explosion-link-native-evidence.json',result,files,check)
    print('Death Sentry typed explosion:51results/5link scenarios per boot/9retained inputs/2reloads;old-DLL scalar fault reproduced and repaired.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
