#!/usr/bin/env python3
"""Bind ordinary eight-class NPC melee without proc equipment and saved state."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/basic-melee';BASE='5a452c716eb0027d3615abdcffbd1cfb84dc3174'
sys.path.insert(0,str(ROOT/'tests'))
from check_basic_melee import configuration

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
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/druid-setup-items/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/PlayerBasicContact.cs','tests/Sanctuary.Integration/BasicMeleeChecks.cs','tests/Sanctuary.Integration/DruidSetupItemChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-2.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    log=e.cohort(STAGE,expected_markers(),428)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==167 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):
            assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'basic-melee-fixtures.json');assert len(saved)==8
    assert len({r['Owner'] for r in saved})==len({r['Item'] for r in saved})==len({r['Target'] for r in saved})==8
    for row in saved:
        assert row['Generation']==2 and row['Loss']>0 and row['Life']>0 and row['Mana']==17 and row['TargetLife']>0
        weapon,helm=json.loads(row['Gear']);assert weapon['NamedItemId']=='unique/52' and helm['NamedItemId']=='unique/207'
        assert len(weapon['NamedProperties'])==8 and len(helm['NamedProperties'])==6
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168
    files=[ROOT/'tools/record_basic_melee.py',ROOT/'docs/basic-melee-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/druid-setup-items-native-evidence.json',ROOT/'tools/reference/druid-setup-source-review.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-run-1.tar.gz']
    result=dict(component='Eight-class ordinary NPC melee independent of proc equipment',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=428,markers=dict(sorted(expected_markers().items())),connections=167,actualOrdinaryWireInputs=48,retainedWireInputs=114,furyPaidSaveStages=3,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,classContexts=8,savedContexts=8,formWeaponContexts=8,lowManaFormFallbacks=8,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/basic-melee/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Fresh actors without proc equipment now use the shared ordered NPC melee consumer:unarmed/plain/named contexts across eight classes,one flat miss return,shield blocking,native damage,setup,wounds and exact absent proc streams pass. Eight transformed weapon contexts retain melee/no-ammunition timing;eight low-mana form skills reach free ordinary Attack with item setup. Human projectile and charged Assassin paths retain their separate producers. Forty-eight actual left/right ordinary inputs span all eight classes. Specific recipient guards,player-to-player,charged finishers,initial learned-form poison allocation,missile/incoming procs,other bodies and exact modern/stat-graph/graphical parity remain unfinished. A fixture-only pre-target shield-choice ordering repair is frozen;one known WardrobeSet warning;no deployment or newer D2R read.')
    e.write('basic-melee-native-evidence.json',result,files,check)
    print('Ordinary melee:428results/48eight-class inputs/8form weapons/8low-mana fallbacks/2reloads;remaining contexts stay explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
