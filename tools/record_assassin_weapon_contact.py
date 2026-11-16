#!/usr/bin/env python3
"""Bind source weapon-poison allocation on learned Druid form attacks."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/assassin-weapon-contact';BASE='e426285d77dd46f10ba28401f7b75d1cf7cd3a63'
sys.path.insert(0,str(ROOT/'tests'))
from check_assassin_weapon_contact import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    for n in c['extra_pre_save_commands']:r[n]+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/rabies-poison-contact/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-6.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AssassinClaws.cs','src/Sanctuary.Content/AssassinWeaponContact.cs','src/Sanctuary.Content/TigerStrikes.cs','tests/Sanctuary.Integration/AssassinWeaponContactChecks.cs','tests/Sanctuary.Integration/CobraStrikeChecks.cs','tests/Sanctuary.Integration/PlayerItemProcWorldChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',2),('Integration',6)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-3.json')['inputs'];final=e.load(STAGE/'source-snapshot-6.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    log=e.cohort(STAGE,expected_markers(),221)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-5.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==74 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):
            assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'assassin-weapon-contact-fixtures.json');assert len(saved)==6
    for row in saved:
        assert row['Generation']==2 and row['Life']>0 and row['Mana']>=0
        weapon,gloves=json.loads(row['Gear']);assert gloves['NamedItemId']=='unique/232' and len(gloves['NamedProperties'])==7
        assert weapon['NamedItemId']=='unique/254' or weapon['NamedItemId'] is None
        profile=json.loads(row['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==row['Owner']
        charges=json.loads(row['Charges']);assert len(charges)==1 and charges[0]['Count']==3 and charges[0]['Owner']==row['Owner']
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168
    files=[ROOT/'tools/record_assassin_weapon_contact.py',ROOT/'docs/assassin-weapon-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-run-1.tar.gz',STAGE/'failed-run-2.tar.gz',STAGE/'failed-run-3.tar.gz',STAGE/'failed-run-4.tar.gz',ROOT/'tools/reference/assassin-weapon-contact-source-review.json']
    result=dict(component='Six Assassin charge builders and charged common Attack NPC contacts',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=221,markers=dict(sorted(expected_markers().items())),connections=74,actualNewWireInputs=42,retainedWireInputs=30,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,builders=6,savedContexts=6,builderScenarios=36,commonFinisherScenarios=72,dualClawScenarios=3,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=2,integrationBuild=6,sourceSnapshot=6,nativeRun=5,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/assassin-weapon-contact/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Older complete do34/do35/common Attack and existing source contact/poison/cold/proc services reused. Six builders,ordinary accuracy,charge gain/consume-one,miss/block/immunity/cancel,underfunded fallback,initial poison,selected hands,actual inputs and original/profile/charge/wound saves qualified. Whole Hellslayer/Venom Grip and named Bartuc controls;raw claw carrier remains an explicit property-boundary fixture rather than named-item admission. Four failed attempts are frozen,including a real cold-writer replacement correction and fixture/diagnostic repairs. Exact modern callbacks,recipient guards,player recipients,other item/missile/incoming paths and separate Dragon Claw cold-length conflict remain open. No newer client read or deployment.')
    e.write('assassin-weapon-contact-native-evidence.json',result,files,check)
    print('Assassin contacts:221results/42new inputs/6builders/2reloads;remaining context and modern gaps explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
