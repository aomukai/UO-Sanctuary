#!/usr/bin/env python3
"""Bind Double Throw real-flight collection,collision and saved learned delivery."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/double-throw-missiles';BASE='51eebeb02f5269ae69a69782e7fc8dce001b5866'
sys.path.insert(0,str(ROOT/'tests'))
from check_double_throw_missiles import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c.get('extra_checks',()):r[n]+=3
    for n in c.get('extra_pre_save_commands',()):r[n]+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/whirlwind-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    changed_data=sorted(p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d);assert changed_data==['Data/Sanctuary/Balance/double-throw-missiles.json'],changed_data
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/BarbarianDoubleThrow.cs','src/Sanctuary.Content/BowLaunch.cs','src/Sanctuary.Content/DoubleThrowMissiles.cs','src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/DoubleThrowChecks.cs','tests/Sanctuary.Integration/DoubleThrowMissileChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',2),('Integration',5)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-2.json')['inputs'];final=e.load(STAGE/'source-snapshot-5.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items()if p.startswith(('src/','shared/')))
    assert sum(expected_markers().values())==140
    log=e.cohort(STAGE,expected_markers(),140);assert 'pass across3boots/2reloads' in (STAGE/'native-run-5.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==44 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines()if 'Warning: Attempted to delete' in line];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'double-throw-missile-fixtures.json');assert saved['Generation']==2 and saved['Life']>0 and saved['Mana']>=0 and saved['First']==8 and saved['Second']==11
    primary,secondary,gloves=json.loads(saved['Gear']);assert primary['NamedItemId']=='unique/335' and secondary['NamedItemId']is None and gloves['NamedItemId']=='unique/232'
    f=saved['Flight'];assert f['Owner']==saved['Owner']and f['Throwing']==dict(Weapon=saved['Primary'],ArmorDelta=0)and f['Frames']==20 and f['Frame']==0 and f['Missile']=='throwaxe'and f['Damage']['Elements']['PoisonMin']==153 and f['Damage']['SkillAttackPercent']==20
    catalog=e.load(ROOT/'data/Sanctuary/Balance/double-throw-missiles.json')[0];assert len(catalog['Weapons'])==30 and len(catalog['Missiles'])==5
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    files=[ROOT/'tools/record_double_throw_missiles.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/double-throw-missiles-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',*[STAGE/f'failed-run-{i}.tar.gz'for i in range(1,5)]]
    result=dict(component='Double Throw travelling weapon missiles and saved learned releases',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=140,markers=dict(sorted(expected_markers().items())),connections=44,newWireInputs=6,retainedWireInputs=30,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,contactScenarios=11,savedOriginalContexts=1,weaponMappings=30,missileRows=5,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=changed_data,contentBuild=2,integrationBuild=5,sourceSnapshot=5,nativeRun=5,nativeRebuilt=False,persistenceVersionChanged=False,optionalFlightMetadataAdded=True,installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Pinned archived ordinary throwing rows and complete older emission/hit functions reuse shared collection,vectors,source-grid travel,collision,pierce and saves. Actual release captures each hand;travel delays HP and supports moving recipients. Native Ogre fallback reuses existing no-invented-shield compatibility;source poison includes initial rate/clock. Eleven focused cases,two input modes,originals/ammunition/flight reloads and retained Double Throw/Magic Arrow/Fury/Lightning Bolt/Mirrored controls pass. Four attempts are frozen:native Ogre shield compatibility,legacy instant-kill expectation,fixture corridor and reload recipient setup. Production unchanged after Content2. Modern payment/timing/RNG/stat/callback/geometry,player and exceptional recipients and missile item-proc/full named admission remain explicit. No deployment or updated reference read.')
    e.write('double-throw-missile-native-evidence.json',result,files,check)
    print('Double Throw:140results/6new inputs/11cases/30weapon mappings/5missile rows/2reloads;modern and item-event gaps remain explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
