#!/usr/bin/env python3
"""Bind tested existing paid Impale bypass as a pending explicit Sanctuary rule."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_impale_contact import expected_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/impale-defense-policy';BASE='97a6a6b37972e78fe67b8cba491aeaf5e13ec5bb'
sys.path.insert(0,str(ROOT/'tests'))
from check_impale_defense_policy import configuration

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/amazon-current-acceptance/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['tests/Sanctuary.Integration/ImpaleContactChecks.cs'],changed
    for p,d in born['artifacts'].items():
        if p!='Assemblies/Sanctuary.Integration.dll':assert d==parent['artifacts'][p],p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]
    for kind in ('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    build=(STAGE/'integration-build-1.log').read_text();assert all(t in build for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==123;log=e.cohort(STAGE,expected_markers(),123);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==38 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert all('WardrobeSet'in l for l in warnings)
    assert not list(STAGE.glob('failed*.tar.gz'))
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for kind in ('Content','Integration'):assert hashlib.sha256(a.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    s=e.load(STAGE/'impale-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['Mana']==17 and s['Durability']>0
    weapon,gloves,helm=json.loads(s['Gear']);assert weapon['NamedItemId']is None and gloves['NamedItemId']=='unique/232'and helm['NamedItemId']=='unique/207'
    profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==s['Owner']
    assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in (195,198)]
    fixture=(ROOT/'tests/Sanctuary.Integration/ImpaleContactChecks.cs').read_text()
    assert 'new[]{"hit","change","immune","range","cancel","paid-defenses","low-hit","low-miss","low-block"}'in fixture
    for text in ('Paid Impale consulted accuracy','Paid Impale consulted shield/avoidance','NpcBlockOrDodge.Try(t,false,true','rate>0','SourceAvoidance.Block'):assert text in fixture,text
    # Existing production bypass is being made a reviewable explicit policy;
    # these assertions do not turn uncertain modern behavior into source proof.
    production=(ROOT/'src/Sanctuary.Content/AmazonImpale.cs').read_text();assert production==subprocess.check_output(['git','show',BASE+':src/Sanctuary.Content/AmazonImpale.cs'],cwd=ROOT,text=True)
    assert 'contact=new(SourceMeleeResult.Successful|SourceMeleeResult.GetHit' in production
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert status==json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    files=[ROOT/'tools/record_impale_defense_policy.py',ROOT/'tools/record_impale_contact.py',ROOT/'docs/impale-defense-policy-proposal.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/amazon-coverage-matrix.md',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Existing paid Impale defense bypass,concrete pending Sanctuary adaptation',referenceVersion='3.3.93847',baselineCommit=BASE,policyStatus='pending-approval',nativeMarkers=123,markers=dict(sorted(expected_markers().items())),contactScenariosPerBoot=9,addedScenario='An active source-mapped shield blocks through the ordinary adapter;paid Impale succeeds with throwing accuracy/shield delegates. Existing low-mana miss/block controls remain.',connections=38,newWireInputs=0,retainedWireInputs=36,boots=3,saves=3,reloads=2,savedOriginalContexts=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],gameplayChanged=False,contentRebuilt=False,integrationBuild=1,sourceSnapshot=1,nativeRun=1,failedAttempts=0,knownWardrobeSaveWarnings=len(warnings),nativeRebuilt=False,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Proves the current Sanctuary implementation and its ordinary fallback,not modern D2R block/dodge behavior. Existing forced Successful/GetHit/HitFlags1 bypass is unchanged online pending explicit adoption. Actual NPC shield control,throwing paid accuracy/block delegates,nine startup/capture/HP/poison/proc/wear/payment/slow/range/cancel/low-mana scenarios and original gear/proc/wounds/resources through two reloads retain. Native dodge-bearing pet/player recipients are not admitted or claimed as tested;source structure bypasses that resolver. Exact modern damage/stat/break/timing/RNG/owner/PvP,additional recipient/item scopes and final assets remain separately recorded.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/impale-defense-policy')
    e.write('impale-defense-policy-native-evidence.json',result,files,check);print('Impale defense candidate:123results/9scenarios/36retained inputs/2reloads;approval pending,no gameplay change.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
