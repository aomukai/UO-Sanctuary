#!/usr/bin/env python3
"""Bind four captured Barbarian contact/item/poison/control consumers."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/barbarian-contact';BASE='ab0e5d6f524356fc444db21fd5aa4e79423df1be'
sys.path.insert(0,str(ROOT/'tests'))
from check_barbarian_contact import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c.get('extra_checks',()):r[n]+=3
    for n in c.get('extra_pre_save_commands',()):r[n]+=3
    r['berserk-snapshot-result']+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/dragon-cold-duration/birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/BarbarianMelee.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','tests/Sanctuary.Integration/BarbarianAttackChecks.cs','tests/Sanctuary.Integration/BarbarianContactChecks.cs','tests/Sanctuary.Integration/BerserkChecks.cs','tests/Sanctuary.Integration/ConcentrateChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-4.json')['inputs']
    assert e.load(STAGE/'source-snapshot-3.json')['inputs']==final
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    log=e.cohort(STAGE,expected_markers(),174);assert 'pass across3boots/2reloads' in (STAGE/'native-run-4.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==62 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'barbarian-contact-fixtures.json');assert len(saved)==4
    for row in saved:
        assert row['Generation']==2 and row['Life']>0 and row['Mana']>=0
        weapon,gloves,helm=json.loads(row['Gear']);assert [x['NamedItemId'] for x in (weapon,gloves,helm)]==['unique/254','unique/232','unique/207']
        profile=json.loads(row['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==row['Owner']
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert len(e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json')['items'])==168
    files=[ROOT/'tools/record_barbarian_contact.py',ROOT/'docs/barbarian-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-run-1.tar.gz',STAGE/'failed-run-2.tar.gz',STAGE/'failed-run-3.tar.gz',ROOT/'tools/reference/barbarian-contact-source-review.json']
    result=dict(component='Four captured Barbarian melee contacts with complete item delivery',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=174,markers=dict(sorted(expected_markers().items())),connections=62,newWireInputs=24,retainedWireInputs=30,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,skills=4,contactScenarios=28,savedContexts=4,workerSnapshotChecks=3,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=2,sourceSnapshot=4,nativeRun=4,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Complete older start32/start39/do2 and existing ordered contact/poison/proc/control services reused. Captured shield/source fields,initial poison/clock,leech and complete Hellslayer/Venom Grip/Blackhorn originals qualify four skills,PMH-before-block,captured budgets/gear/resistance,miss/block/immunity/cancel,approved Basic fallback,control flags,actual inputs and saved profiles/originals/wounds. Optional transient control-metadata flag preserves Stun rules without changing defaults. Three attempts are frozen:worker result binding,legacy low-mana expectations,and root-disk exhaustion at final output after completed checks/saves. Production unchanged after Content1;Integration unchanged after build2. Exact modern stale-total/startup/RNG/stat/mode,recipient/player and remaining item/missile/incoming gaps remain. No deployment or updated client read.')
    e.write('barbarian-contact-native-evidence.json',result,files,check)
    print('Barbarian contacts:174results/24new inputs/28cases/4saved contexts/2reloads;modern and other gaps explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
