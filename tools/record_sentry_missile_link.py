#!/usr/bin/env python3
"""Bind the shared sentry elemental-impact Health Link regression and repair."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/sentry-missile-link';BASE='c6b37882cc107e2bc2261b469f3313d397e0550d'
sys.path.insert(0,str(ROOT/'tests'))
from check_sentry_missile_link import configuration,FAMILIES,LEARNED
TALENTS=['assassin.chargedboltsentry','assassin.lightningsentry','assassin.deathsentry','assassin.wakeofinferno','assassin.wakeoffire']

def expected_markers():
    r=Counter({'iron-golem-birth-check':3,'sentry-learned-availability-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*FAMILIES):r[f+'-check']=1;r[f+'-reload-check']=2
    r['defiler-health-link-check-result']=1;r['defiler-health-link-reload-check-result']=2
    for f in LEARNED:
        r[f+'-mechanics-check']=3;r[f+'-result']=1;r[f+'-reload-result']=2
        for mode in ('left','right','quick'):r[f+'-'+mode+'-check']=3;r[f+'-'+mode+'-result']=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/sentry-explosion-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AssassinSentryBolts.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs'],changed
    data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    old=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==sorted(TALENTS)
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
    assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240
    prior=subprocess.check_output(['git','show',BASE+':src/Sanctuary.Content/AssassinSentryBolts.cs'],cwd=ROOT,text=True)
    replacement='        var parts=element=="fire"?new DirectDamageParts(0,0,result.Damage256,0,0):new(0,0,0,0,result.Damage256);\n        int loss=SkillHealth.TypedDamage(body,target,result.Damage256,parts);'
    assert (ROOT/'src/Sanctuary.Content/AssassinSentryBolts.cs').read_text()==prior.replace('        int loss=SkillHealth.Damage(body,target,result.Damage256);',replacement,1)
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for kind,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),171);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==63 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
    assert log.count('after having been attached to a deleted mobile')==15
    negatives=[l for l in log.splitlines()if ' WRN]'in l and 'Defiler link sharing refused unsupported context'in l]
    assert len(negatives)==3 and all('Direct damage lacks settled typed components'in l for l in negatives)
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for kind in ('Content','Integration'):assert hashlib.sha256(a.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as a:
        text=a.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
        assert 'Sentry ChargedBoltSentry/normal typed link:fault=Direct damage lacks settled typed components;primary=2431/2431;secondary=0/1288;events=1;typed=False'in text
    baseline=e.load(STAGE/'failed-baseline-1-native-inputs.json')
    assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
    for name in ('defiler-health-link',*LEARNED,'wake-fire-missile'):assert e.load(STAGE/(name+'-fixtures.json'))['Generation']==2,name
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
    relocation=e.load(ROOT/'tools/reference/sentry-missile-storage-relocation.json')
    assert len(relocation['worlds'])==6 and sum(r['files']for r in relocation['worlds'])==1502 and not relocation['installedServerTouched']
    for r in relocation['worlds']:
        assert e.sha(__import__('pathlib').Path(r['archive']))==r['archiveSha256'] and e.sha(__import__('pathlib').Path(r['manifest']))==r['manifestSha256']
    files=[ROOT/'tools/reference/sentry-missile-storage-relocation.json',ROOT/'tools/record_sentry_missile_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/sentry-missile-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/assassin-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/p for p in ('accepted-assemblies-1.tar.gz','failed-baseline-1.tar.gz'))]
    result=dict(component='All five stationary sentry missile contacts publish their settled element to approved Defiler Health Link',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=171,markers=dict(sorted(expected_markers().items())),newLinkScenariosPerBoot=15,retainedNecromancerSpellScenariosPerBoot=9,retainedMageScenariosPerBoot=14,retainedExplosionScenariosPerBoot=5,affectedTalents=TALENTS,retainedWireInputs=45,successfulRightQuickCasts=30,expectedLeftBindingRejections=15,newWireInputs=0,connections=63,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,failedCorrectedNativeAttempts=0,negativeBaseline='Old accepted content DLL:CBS primary2431/2431FP256,secondary0/1288,one untyped event;approved link faults. Frozen before repair.',fixtureCompileFailures=0,driverPreflightFailures=0,knownWardrobeSaveWarnings=0,intentionalUntypedRefusalWarnings=3,informationalDeletedFixtureMobilePacketNotices=15,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Shared contact supplies the already-settled lightning/fire lane;primary totals,block,absorption,recovery,source scaling and projectile RNG retain.15prepared-actor contact/link cases per boot cover5routes with normal,resisted-secondary and immune-primary behavior.5existing projectile/controller and5learned-input/original-save cohorts separately retain real travel,wave grouping,retirement,payment,cancellation and all45wire actions. The new link matrix invokes contact directly. Three scalar-refusal warnings are intentional;15late-packet notices follow fixture player deletion. Other Assassin spell/Martial/Blade direct scalar paths still need qualification;periodic/self/return exclusions remain. Exact modern global caller/PvP/full items/current whole class/client/assets remain separate. No new design approval is required.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/sentry-missile-link')
    e.write('sentry-missile-link-native-evidence.json',result,files,check)
    print('Sentry typed impacts:171results/15link cases per boot/45retained inputs/2reloads;all5stationary routes qualify.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
