#!/usr/bin/env python3
"""Bind source weapon-poison allocation on learned Druid form attacks."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/druid-weapon-poison';BASE='c6a8a558fdd229e69a88725d082aa3652b29d49b'
sys.path.insert(0,str(ROOT/'tests'))
from check_druid_weapon_poison import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    for n in c['extra_pre_save_commands']:r[n]+=3
    for n in ('druid-fury-stage-check','druid-fury-stage-result'):r[n]+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/basic-melee/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/DruidChargedAttacks.cs','src/Sanctuary.Content/DruidFireClaws.cs','src/Sanctuary.Content/DruidFury.cs','src/Sanctuary.Content/DruidHunger.cs','src/Sanctuary.Content/DruidRabies.cs','src/Sanctuary.Content/DruidWeaponContact.cs','tests/Sanctuary.Integration/DruidCapturedProcChecks.cs','tests/Sanctuary.Integration/DruidWeaponPoisonChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',1)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-1.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    log=e.cohort(STAGE,expected_markers(),479)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==191 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):
            assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'druid-weapon-poison-fixtures.json');assert len(saved)==8
    assert [(r['Ability'],r['Form']) for r in saved]==[(165,164),(166,162),(167,162),(167,164),(170,162),(170,164),(168,162),(171,162)]
    for row in saved:
        assert row['Generation']==2 and row['Life']>0 and row['TargetLife']>0
        weapon,gloves=json.loads(row['Gear']);assert weapon['NamedItemId']=='unique/170' and gloves['NamedItemId']=='unique/232'
        assert len(weapon['NamedProperties'])==8 and len(gloves['NamedProperties'])==7
        shape=json.loads(row['Shape']);assert shape['Owner']==row['Owner'] and shape['Form']['Ability']==row['Form']
        poison=json.loads(row['Poison']);assert poison['Owner']==row['Target'] and poison['PoisonSource']==row['Owner'] and poison['PoisonMs']==7000 and poison['PoisonClock']['Rate256']==326
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168
    files=[ROOT/'tools/record_druid_weapon_poison.py',ROOT/'docs/druid-weapon-poison-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/basic-melee-native-evidence.json',ROOT/'tools/reference/druid-setup-source-review.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Source weapon-poison allocation for all six learned Druid form weapon skills',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=479,markers=dict(sorted(expected_markers().items())),connections=191,actualWeaponPoisonWireInputs=24,retainedWireInputs=162,furyPaidSaveStages=3,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,skills=6,formSkillContexts=8,savedContexts=8,poisonScenarioCases=48,fullPoisonFurySeries=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/druid-weapon-poison/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='All six learned form weapon producers collect explicit source poison rate/length rather than infer them from aggregate totals. Existing source128,count/mastery/override,recipient mitigation and initial-rate/clock services are reused. Actual Blackbog/Venom Grip pools653/350/count2 produce653rate/175frames or326at50percent resistance. Gear/resistance changes distinguish windup capture from action-time reads;immunity,miss/block/cancel,full Fury repeated initial hits and exact saved clocks pass with 24representative poison inputs and retained roster/proc cohorts. One explicit source-packet fixture expectation now includes its legitimate initial64;aggregate-only compatibility remains unchanged. Separate Rabies forced skill-poison initial damage/event chronology,specific recipients,player-to-player,charged finishers,missile/incoming contexts,other item bodies and exact modern/RNG/stat-graph/graphical parity remain unfinished. One known WardrobeSet warning;no deployment or newer D2R read.')
    e.write('druid-weapon-poison-native-evidence.json',result,files,check)
    print('Druid weapon poison:479results/24actual inputs/8form contexts/48scenario cases/2reloads;separate skill-poison and other gaps stay explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
