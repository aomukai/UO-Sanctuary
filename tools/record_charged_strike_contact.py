#!/usr/bin/env python3
"""Bind ordered Plague Javelin parent/moving-cloud delivery, original saves and learned input."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_plague_javelin_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/charged-strike-contact';BASE='4681c5a3a9df5541454914b3a1af35d3fed309cc'
sys.path.insert(0,str(ROOT/'tests'))
from check_charged_strike_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('charged-strike-contact','jab','power-strike','charged-strike','slow-missiles-component','charged-strike-velocity'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    r['weapon-sequence-rate-check']+=3
    for name in ('charged-strike-native-bolts','charged-strike-expiry-start','charged-strike-expiry','charged-strike-result','slow-missiles-component-result'):r[name]+=1
    r['charged-strike-reload-result']+=2
    for n in ('charged-strike-contact-right','charged-strike-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/plague-javelin-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.chargedstrike']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/ChargedStrikeBolts.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','tests/Sanctuary.Integration/ChargedStrikeContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',2),('Integration',3)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==442;log=e.cohort(STAGE,expected_markers(),442);assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==137 and 'denied by connection filter' not in log and 'Charged Strike bolt unsupported context' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for name in ('Content','Integration'):assert hashlib.sha256(a.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    failed=e.load(STAGE/'failed-native-1-native-inputs.json');assert failed['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll'] and failed['Assemblies/Sanctuary.Integration.dll']!=born['artifacts']['Assemblies/Sanctuary.Integration.dll']
    with tarfile.open(STAGE/'failed-native-1.tar.gz','r:gz')as a:
        text=a.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'Charged Strike missed parent/emission/resource split' in text
    s=e.load(STAGE/'charged-strike-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['TargetLife']==60000*256 and s['Mana']==17 and s['Durability']>0
    f=s['Flight'];assert f['Owner']==s['Owner'] and f['Frame']==1 and f['Rank']==1 and f['Min256']>0 and f['Max256']>=f['Min256'] and f['FactoryMotion']==dict(VelocityFp8=2304,SlowVelocityPercent=100)
    gear=json.loads(s['Gear']);assert len(gear)==3 and gear[0]['NamedItemId']is None and [g['NamedItemId']for g in gear[1:]]==['unique/232','unique/207'];profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_charged_strike_contact import record as review
    review(True)
    files=[ROOT/'tools/record_charged_strike_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/charged-strike-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'docs/amazon-coverage-matrix.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-native-1.tar.gz']
    result=dict(component='Charged Strike source0 elemental bolt contacts',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=442,markers=dict(sorted(expected_markers().items())),connections=137,newWireInputs=6,retainedWireInputs=126,boots=3,saves=3,reloads=2,boltScenarios=8,savedOriginalContexts=1,continuedOriginalBolts=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description refreshed;mechanical and completion data unchanged. A stale later Charged Strike roadmap override was removed before final builds.',contentBuild=2,integrationBuild=3,sourceSnapshot=4,nativeRun=2,failedAttempts=['failed-native-1:fixture expected no freed packet for a melee miss;each miss correctly frees one return packet. Only parent/input packet-count assertions changed;production unchanged.'],nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Source0 chargedstrikebolt reuses elemental HP/absorb/credit/kill/native return with Successful1/GetHit4/HitClass0/HitFlags0,without weapon callbacks,proc RNG,Crushing Blow,drain,poison,cold,slow or armor. Existing captured rank/endpoints/mastery,damage-roll service,initializer seed/curved path,first-contact removal,77frame life and factory-speed/legacy fallback remain. Eight bolt cases include immunity,resistance,lethal,provider removal,one-of-two collision,independent three-bolt contacts and legacy motion shape. A real successful parent primes the original proc stream;later isolated bolts preserve it. Natural right/quick known-miss input has one completed melee attempt packet,three bolt packets,one zero-attempt item observation and one mana payment. An original partial bolt and exact gear/proc/wound/mana/durability/quantity persist and continue twice. Retained melee,old bolt damage/expiry,72factory speed cases and mixed legacy/new saves pass alongside126retained wire inputs. Modern Slow Missiles damage capture/order,source Size/shotgun/activation/global-scheduler/grid/seed/owner/provider/exceptional/PvP and full named/class/client/asset acceptance remain open. No new saved field/version,native rebuild,updated client read,installation,eligibility change or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/charged-strike-contact')
    e.write('charged-strike-contact-native-evidence.json',result,files,check);print('Charged Strike:442results/6new inputs/8bolt cases/2original continuations/retained melee and motion/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
