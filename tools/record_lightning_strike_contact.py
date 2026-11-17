#!/usr/bin/env python3
"""Bind ordered Plague Javelin parent/moving-cloud delivery, original saves and learned input."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_charged_strike_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/lightning-strike-contact';BASE='3e49d1f9a736f06854f843ab113a4e26e22ef86b'
sys.path.insert(0,str(ROOT/'tests'))
from check_lightning_strike_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('lightning-strike-contact','lightning-strike','player-chain','mage-chain'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    r['chain-child-search-check']+=3;r['player-chain-reload-result']+=2
    for n in ('lightning-strike-right','lightning-strike-quick','lightning-strike-contact-right','lightning-strike-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/charged-strike-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.lightningstrike']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/ChainLightning.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','tests/Sanctuary.Integration/ChargedStrikeContactChecks.cs','tests/Sanctuary.Integration/LightningStrikeContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',1)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==483;log=e.cohort(STAGE,expected_markers(),483);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==151 and 'denied by connection filter' not in log and 'Lightning Strike chain unsupported context' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==2 and all('WardrobeSet' in w for w in warnings),warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for name in ('Content','Integration'):assert hashlib.sha256(a.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    s=e.load(STAGE/'lightning-strike-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['PrimaryLife']==60000*256 and 0<s['SecondaryLife']<60000*256 and s['Mana']==17 and s['Durability']>0
    f=s['Flight'];assert f['Owner']==s['Owner'] and f['Frame']==1 and f['Rank']==1 and f['Remaining']==1 and f['AimTarget']==s['Primary'] and f['StrikeContext']==dict(OpeningPrimary=0,Profile='sanctuary-lightning-strike-opening-v1') and len(f['Delays'])==1 and f['Delays'][0]['Target']==s['Secondary'] and len(f['PlayerPacket']['Steps']['Cells'])>0
    from datetime import datetime
    parse=lambda x:datetime.fromisoformat(x.replace('Z','+00:00'))
    assert (parse(f['Delays'][0]['UntilUtc'])-parse(f['NextUtc'])).total_seconds()==.08
    gear=json.loads(s['Gear']);assert len(gear)==3 and gear[0]['NamedItemId']is None and [g['NamedItemId']for g in gear[1:]]==['unique/232','unique/207'];profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_lightning_strike_contact import record as review
    review(True)
    files=[ROOT/'tools/record_lightning_strike_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/lightning-strike-contact-checkpoint.md',ROOT/'docs/amazon-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Lightning Strike source0 chain NPC contacts',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=483,markers=dict(sorted(expected_markers().items())),connections=151,newWireInputs=6,retainedWireInputs=138,boots=3,saves=3,reloads=2,chainScenarios=9,immediatePoisonClockBoundaryCases=1,savedOriginalContexts=1,continuedOriginalReturnSegments=2,knownWardrobeSaveWarnings=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description refreshed;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,failedAttempts=[],nonblockingQuestions=['Whether Lightning Strike chain should follow weapon-skill Magical Penetration exclusion. Current impact-time spell additions are preserved pending reply;question does not block base contact.'],nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Amazon source0 chain NPC impacts reuse elemental HP/absorb/credit/kill/native return with Successful1/GetHit4/HitClass64/HitFlags0,without weapon callbacks,proc allocation,Crushing Blow,drain,poison/cold status,slow or armor. The approved neighbor-only opening,per-child current stat/seed/speed capture,identity filter,source path,four-frame target leases and finite budget remain. Existing live potion additions are preserved;zero-duration immediate poison remains typed damage and does not replace an existing poison clock. Nine cases and one receiver boundary include solo,immune,resisted,lethal,provider removal,potion timing and finite return hop. An original partial returning segment with an80ms remaining lease,gear/proc/wound/resources persists and continues twice. Natural right/quick known-miss input shows one melee-attempt packet then two chain packets,without item casts. Retained original Lightning Strike,Sorceress/Mage chain,search and prior ranged/contact/motion checks pass. Current potion classification remains nonblocking;modern full event/stat/RNG/owner/NHD/world/exceptional/PvP and full named/class/client/asset acceptance remain open. No saved field/version,native rebuild,updated client read,installation,eligibility change or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/lightning-strike-contact')
    e.write('lightning-strike-contact-native-evidence.json',result,files,check);print('Lightning Strike:483results/6new inputs/9chain cases/2original returning segments/shared controllers/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
