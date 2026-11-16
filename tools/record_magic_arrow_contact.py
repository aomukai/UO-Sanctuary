#!/usr/bin/env python3
"""Bind Magic Arrow ordered impact and captured armor packet/reload checks."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import current
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/magic-arrow-contact';BASE='fbf2bdb1764a54b42e262c2e5105d424edf5542a'
sys.path.insert(0,str(ROOT/'tests'))
from check_magic_arrow_contact import configuration

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
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/thrown-proc-flight/birth-inputs.json');compiled=e.load(STAGE/'source-snapshot-3.json')['inputs'];now=current();metadata='Data/Sanctuary/Balance/class-skill-status.json';assert compiled['sourceInputs']==now['sourceInputs'] and compiled['projectInputs']==now['projectInputs'];assert {p:d for p,d in compiled['copiedData'].items()if p!=metadata}=={p:d for p,d in now['copiedData'].items()if p!=metadata};assert born['copiedData']==compiled['copiedData'];assert {p:d for p,d in born['sourceInputs'].items()if p.endswith('.cs')}==compiled['sourceInputs'];before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240;assert sum(a!=b for a,b in zip(before['skills'],after['skills']))==8;assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():
        if p!=metadata:assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AmazonBow.cs','src/Sanctuary.Content/BowLaunch.cs','src/Sanctuary.Content/DoubleThrowMissiles.cs','src/Sanctuary.Content/MagicArrowContact.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/MagicArrowChecks.cs','tests/Sanctuary.Integration/MagicArrowContactChecks.cs'],changed
    path='tests/Sanctuary.Integration/MagicArrowChecks.cs';old=subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT).decode();new=(ROOT/path).read_text()
    assert new==old.replace('TimedEffects.CanRegenerateHits(target),"Outer attacker event suppression lost"','!TimedEffects.CanRegenerateHits(target),"Outer hit-event suppression or separate accuracy-roll PMH lost"'),'Retained Magic Arrow fixture changed beyond source-backed PMH correction'
    first=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-3.json')['inputs'];assert first['copiedData']==final['copiedData'] and first['projectInputs']==final['projectInputs']
    assert all(final['sourceInputs'].get(p)==d for p,d in first['sourceInputs'].items()if p.startswith(('src/','shared/')))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',3)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==164
    log=e.cohort(STAGE,expected_markers(),164);assert 'pass across3boots/2reloads' in (STAGE/'native-run-3.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==53 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'magic-arrow-contact-fixtures.json');assert saved['Generation']==2 and saved['Life']>0 and saved['TargetLife']>0 and saved['Mana']==17 and saved['Flight']['Frame']>0
    assert saved['Flight']['Missile']=='magicarrow' and saved['Flight']['Damage']['ArmorDelta']==-1 and saved['Flight']['Damage']['Elements']['PoisonMin']==0
    bow,gloves,helm=json.loads(saved['Gear']);assert bow['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207'
    profile=json.loads(saved['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');observed=e.load(STAGE/'hellslayer-readiness-fixtures.json');expected=json.loads(json.dumps(observed));docpath='data/'+metadata.removeprefix('Data/');expected['InputHashes'][docpath]=e.sha(ROOT/docpath);expected['PostVerificationDocumentation']=dict(Path=docpath,ObservedSha256=observed['InputHashes'][docpath],CurrentSha256=e.sha(ROOT/docpath),Changes='Eight remaining-description strings only;all mechanical and completion fields unchanged. Original native audit remains in magic-arrow-contact/hellslayer-readiness-fixtures.json.');assert audit==expected
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_magic_arrow_contact import record as review
    review(True)
    files=[ROOT/'tools/record_magic_arrow_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'data/Sanctuary/Balance/class-skill-status.json',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/magic-arrow-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'unavailable-armor-supplier-run-1.tar.gz',STAGE/'armor-cap-run-2.tar.gz']
    result=dict(component='Learned Magic Arrow ordered NPC missile impact,item hit procs and captured armor packet continuation',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=164,markers=dict(sorted(expected_markers().items())),connections=53,newWireInputs=6,retainedWireInputs=42,boots=3,saves=3,reloads=2,contactScenarios=13,savedOriginalContexts=1,continuedArmorImpacts=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=['data/Sanctuary/Balance/class-skill-status.json'],mechanicalDataChanged=False,postVerificationMetadata=dict(path='data/Sanctuary/Balance/class-skill-status.json',beforeSha256=born['copiedData'][metadata],afterSha256=e.sha(ROOT/'data/Sanctuary/Balance/class-skill-status.json'),scope='Only eight remaining-description strings refreshed after verification;all mechanical data and class completion fields unchanged.'),contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeRebuilt=False,persistenceVersionChanged=False,optionalSavedFields=['WeaponMissileDamage.ArmorDelta;default0,omitted when0'],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Actual learned named magicarrow NPC impacts share the throwing packet allocator and ordered damage/credit/status/kill/return bridge. Successful unblocked impacts issue domissiledamage before HP with the approved actor/world child stream. Accuracy PMH precedes block and ignores attacker-event suppression;the retained fixture expectation is corrected against the complete bound source routine. Captured negative armor applies after the handler including block/suppressed events. Twelve provider/armor cases and a Crossbow case retain captured conversion/poison,live provider removal,pierce,once mana/no ammo and exact RNG. Armor cases inject a negative1 packet value with live zero-delta gear;this certifies packet consumption/persistence,not whole Soul Drainer admission or an original negative-armor bow loadout. That item stays gated. A partly travelled original preserves complete flight/gear/proc/wound/mana state through two reloads,before test-only recipient overrides are restored;two continuations apply armor once without new payment. Source poison and all throwing/melee/item regressions pass. Earlier unavailable-supplier and armor-cap fixture attempts are frozen. Old version0 packets default optional armor to0;anonymous compatibility flights retain their old consumer. Modern attack timing,hand/provider/owner/stat/world,other missiles,exceptional/PvP/full named/class/client/asset certification remain open. No deployment or updated game reference read.')
    e.write('magic-arrow-contact-native-evidence.json',result,files,check)
    print('Magic Arrow:164results/6new inputs/13cases/2reloads;readiness and installed state preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
