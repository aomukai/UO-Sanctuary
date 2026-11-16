#!/usr/bin/env python3
"""Bind captured Amazon lightning thrusts,source poison and ordered item/child delivery."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/thrown-contact';BASE='6eafad104745520f0ff7501f0a090183dab5277d'
sys.path.insert(0,str(ROOT/'tests'))
from check_thrown_contact import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):
                waits=c['extra_wait_results'][n]
                for result in ((waits,)if isinstance(waits,str)else waits):r[result]+=count
    for n in c.get('extra_checks',()):
        r[n]+=3
        if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=3
    for n in c.get('extra_pre_save_commands',()):r[n]+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/existing-melee-proc/birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    data_changed=sorted(p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d)
    assert data_changed==['Data/Sanctuary/Balance/double-throw-missiles.json'],data_changed
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/double-throw-missiles.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/double-throw-missiles.json')
    for row in after:
        for missile,fields in row['Missiles'].items():assert fields.pop('HitClass')in(2,6)
    assert after==before
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/DoubleThrowMissiles.cs','src/Sanctuary.Content/PlayerItemProcWorld.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/ThrownContactChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',2),('Integration',5)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==140
    log=e.cohort(STAGE,expected_markers(),140);assert 'pass across3boots/2reloads' in (STAGE/'native-run-4.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==47 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'thrown-contact-fixtures.json');assert saved['Generation']==2 and saved['Life']>0 and saved['Mana']==17 and saved['First']==39 and saved['Second']==40
    primary,secondary,gloves,helm=json.loads(saved['Gear']);assert primary['NamedItemId']is None and secondary['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207'
    profile=json.loads(saved['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==saved['Owner']
    assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in (195,198)]
    compiled=e.load(STAGE/'source-snapshot-5.json')['inputs'];final=e.load(STAGE/'source-snapshot-5.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs']and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items()if p.startswith(('src/','shared/')))
    integration=e.load(STAGE/'source-snapshot-5.json')['inputs']
    assert all(final['sourceInputs'].get(p)==d for p,d in integration['sourceInputs'].items()if p.endswith('.cs'))
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    files=[ROOT/'tools/record_thrown_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/thrown-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'pre-status-cohort-3.tar.gz']
    result=dict(component='Ordinary Double Throw ordered missile contact and impact-time item hit procs',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=140,markers=dict(sorted(expected_markers().items())),connections=47,newWireInputs=6,retainedWireInputs=36,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,contactScenarios=8,savedOriginalContexts=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=data_changed,contentBuild=2,integrationBuild=5,sourceSnapshot=5,nativeRun=4,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,installed=False,namedAdmissionChanged=False,backlogRows=168,missileAttackCallerVerified=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Double Throw NPC impacts now reuse the existing ordered missile damage/credit/status/kill/return pipeline and captured source poison/drain/cold. Five archived HitClass fields extend the existing ordinary throwing catalog. Successful impacts issue only domissiledamage through the approved saved actor/world streams;no launch/per-impact missile-attack event,melee attempts,weapon wear or Thorns is invented. Eight provider cases cover hit,miss,block,immunity,provider removal,physical notification,lethal contact and pierce;pre-HP callbacks,exact streams,once resources,saved originals and six learned inputs pass with retained Double Throw travel/sequence and existing melee/item controls. Item status consumers run in the successful missile event phase,including fully immune zero-life-damage and lethal hits. The earlier passing C1cohort is frozen separately;fixture carrier/compiler/runner repairs are frozen. Other projectile families,modern attack/selected-hand/provider/owner/RNG/world contexts,proc-bearing in-flight saves,exceptional/PvP and full named admission remain explicit;no class certification or deployment.')
    e.write('thrown-contact-native-evidence.json',result,files,check)
    print('Thrown contact:140results/6new inputs/8cases/2reloads;missile-attack caller and other families remain explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
