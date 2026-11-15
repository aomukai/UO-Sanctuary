#!/usr/bin/env python3
"""Authenticate the complete,gated Mirrored action/world candidate."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/mirrored-world';PARENT='e8d6241ab110601fe3b9fcb5f12b4306f425954d'

def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-8.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-8.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    previous=pin(ROOT/'.local/class-skills/mirrored-packets/birth-inputs.json');parent=pin('tools/reference/mirrored-packets-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    added={'src/Sanctuary.Content/MirroredBladesAction.cs','src/Sanctuary.Content/MirroredBladesContacts.cs','tests/Sanctuary.Integration/MirroredWorldChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('ActionTiming','CharacterProfiles','ClassCombat','ClassDeath','MirroredBladesPackets','NamedCombat','PlayerWeaponContact','WeaponMissiles')}
    assert set(now)-set(old)==added and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']
    for kind,n in (('content',3),('integration',5)):
        p=STAGE/f'{kind}-build-{n}.log';pin(p)
        assert all(s in p.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    c3=pin(STAGE/'source-snapshot-6.json')
    assert {n:h for n,h in c3['inputs']['sourceInputs'].items()if n.startswith('src/')}=={n:h for n,h in now.items()if n.startswith('src/')}
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in ('cleave-action','magic-arrow-cast','mirrored-packets'):expected.update({family+'-check':1,family+'-reload-check':2})
    for family in ('mirrored-world','echoing-strike-cast'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    for mode in ('left','right','quick'):expected.update({'echoing-strike-cast-'+mode+'-check':3,'echoing-strike-cast-'+mode+'-result':3})
    text=e.cohort(STAGE,expected,48);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-6.log')
    assert text.count('Client: 127.0.0.1: Connected.')==15
    assert text.count('Mirrored world:all four original weapon kinds pass SQ25/one payment/independent primary-copy contacts and Crushing Blow')==1
    assert text.count('Mirrored world:actual expired/facet/offline/class-history/death controls retire released throws without replay')==1
    assert text.count('Mirrored world:original four-kind owners/weapons/boots/ammo/quantity/fractional resources and nine primary-copy missiles/metadata/seeds/clocks retain')==2
    assert text.count('Mirrored packets:192independent channel/count/critical controls')==1
    for mode in ('left','right','quick'):assert text.count('echoing-strike-cast-'+mode+': windup')==3
    saved=pin(STAGE/'mirrored-world-fixtures.json');assert saved['Generation']==2 and len(saved['Rows'])==4
    assert [len(r['Flights'])for r in saved['Rows']]==[0,3,3,3]
    for kind,row in enumerate(saved['Rows']):
        assert row['Life']%256 and row['Mana']%256 and row['Weapon']>0 and row['Boots']>0
        for ordinal,f in enumerate(row['Flights']):
            m=f['Mirrored'];assert m['Kind']==kind and m['Ordinal']==ordinal and m['Rank']==7 and not m['Basic']and m['Seed']>0
            assert f['Owner']==row['Owner']and f['Ability']==int(f['Ability'])and f['SkillRank']==7 and f['Frames']==50 and f['NextDelay']==0
            assert f['Damage']['SkillAttackPercent']==190 and f['Damage']['SourcePoisonFrames']
    assert pin(STAGE/'mirrored-packets-fixtures.json')['Generation']==pin(STAGE/'echoing-strike-cast-fixtures.json')['Generation']==2
    pin(STAGE/'cleave-action-fixtures.json');pin(STAGE/'magic-arrow-cast.json')
    history=[]
    for run in (1,2,3,4):
        a=STAGE/f'native-{run}';earlier=pin(a/'birth-inputs.json');pin(a/'birth-native-checks.log');pin(a/f'native-run-{run}.log')
        for n,h in earlier['sourceInputs'].items():assert e.sha(a/'source'/n)==h,n
        for kind in ('Content','Integration'):
            assert e.sha(a/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        log=(a/'birth-native-checks.log').read_text()
        if run==1:assert 'magic-arrow-cast-check: failed'in log and 'magic-arrow-action-source-probe.json'in log
        elif run==2:assert 'Fractional payment accounting'in log and 'mirrored-world-check-result: failed'in log
        elif run==3:assert 'FileNotFoundError'in(a/f'native-run-{run}.log').read_text()and ' ERR]'not in log
        else:
            e.cohort(a,expected,48)
            assert {n for n,h in earlier['sourceInputs'].items()if n.endswith('.cs')and h!=now[n]}=={
                'src/Sanctuary.Content/WeaponMissiles.cs','src/Sanctuary.Content/MirroredBladesAction.cs','tests/Sanctuary.Integration/MirroredWorldChecks.cs'}
        history.append(str(a.relative_to(ROOT)))
    invalid=STAGE/'native-5-invalid';bad=pin(invalid/'birth-inputs.json');reason=pin(invalid/'invalid.json')
    pin(invalid/'birth-native-checks.log');pin(invalid/'native-run-5.log')
    assert reason['accepted']is False and 'KeyboardInterrupt'in(invalid/'native-run-5.log').read_text()
    for n,h in bad['sourceInputs'].items():assert e.sha(invalid/'source'/n)==h,n
    assert e.sha(invalid/'integration/Sanctuary.Integration.dll')==e.sha(STAGE/'native-4/integration/Sanctuary.Integration.dll')==bad['artifacts']['Assemblies/Sanctuary.Integration.dll']
    assert e.sha(invalid/'content/Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll']==bad['artifacts']['Assemblies/Sanctuary.Content.dll']
    for n in range(1,9):pin(STAGE/f'source-snapshot-{n}.json')
    for kind,last in (('content',3),('integration',5)):
        for n in range(1,last+1):pin(STAGE/f'{kind}-build-{n}.log')
    for directory,n in (('compile-1','src/Sanctuary.Content/MirroredBladesContacts.cs'),('compile-4','tests/Sanctuary.Integration/MirroredWorldChecks.cs')):pin(STAGE/directory/'source'/n)
    assert 'error CS0136'in(STAGE/'integration-build-4.log').read_text()
    review=pin('tools/reference/mirrored-world-source-review.json')
    assert not review['approved']and not review['learnedEnabled']and review['newTimers']==review['newLiveReferenceReads']==0 and not review['newPersistenceProfiles']
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(added|changed),'tests/check_mirrored_world.py','tools/record_mirrored_world.py','docs/mirrored-world-rule-proposal.md','docs/mirrored-world-checkpoint.md',
        'docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','docs/class-skill-roadmap.md',
        'tools/reference/echoing-strike-world-rule-approval.json','tools/reference/eldritch-blast-cast-client-evidence.json'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.MirroredBlades'not in code and 'CombatAbility.EchoingStrike'in code
    result=dict(referenceVersion='3.3.93847',component='Complete proposed Mirrored Blades SQ25/melee/ranged world',handlers=229,warlockHandlers=20,completedClasses=[],approved=False,learnedEnabled=False,
        contentBuild=3,integrationBuild=5,sourceSnapshot=8,nativeRun=6,nativeMarkers=48,markers=dict(expected),boots=3,reloads=2,saves=3,authenticatedSessions=15,
        actualInputs=9,actualInputsAbility='EchoingStrike',actualMirroredInputs=0,packetControls=192,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],
        optionalExistingProfileFields=[dict(profile=147,version=0,field='Mirrored')],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,newerReferenceSubstitution=False,
        retainedNativeRuns=history,invalidInterruptedRun=str(invalid.relative_to(ROOT)),frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},
        verified=['All four original weapon kinds exercise SQ25/unpaid windup/one release debit,independent primary/copy ordinary hit and Crushing Blow,actual Ripsaw Open Wounds,one arrow/bolt/throw debit and basic fallback. Real skeleton blocks three independent copies;cancel/move/weapon change,no-ammo release,target death and empty melee controls pass.',
            'Expired/facet/offline/class-history/actual player death controls retire released throws without replay. New optional metadata rejects before mutating the existing store. C3also rechecks ownership between overdue steps and includes shape/unarmed admission guards.',
            'Four original owners/weapons/boots/ammo identities/throw quantities/durability/fractional resources and nine original ranged primary/copy packets/critical/ordinal/kind/seeds/clocks retain and resume actual hits across two reloads. Future dates isolate persistence only.',
            'Retained192packet controls,Cleave ordered melee/default allocation and Magic Arrow travelling damage/item effects/reload tests pass. Nine actual learned Echoing left-right-quick casts and automatic contacts retain;no Mirrored socket input is claimed.',
            'N1missing harness probe,N2payment assertion ignoring Ripsaw leech,N3wrong manifest copy name retained. C2/I3/N4passed48markers;C3adds lifecycle/restore/admission guards. I4duplicate local failed compilation;mistaken stale-I3N5was stopped withSIGINT130 and explicitly invalidated. Final C3/I5/S8/N6clean.',
            'No old supplier/compiler matrix,kernel/client rebuild,new timer/profile/version,installed deployment,protected-current retry or artwork. Legacy/shared client inputs,installed build/config/saves and four pending proposal/evidence pairs remain unchanged.'],
        limits=['Complete action/world rule is pending;learned binding/input remains gated,coverage229/240/Warlock20/30,no class certified. Prepared native controls and retained Echoing inputs do not establish actual Mirrored network/graphical input.',
            'Current start68/do166,emission/factory,global seed/event order,Slow Missiles/standard-arrow edges,unmapped/special/PvP/class-pet and globally unqualified item chance-to-cast contexts remain unverified or unsupported. Shared supported effects are retained;no new unavailable player proc is fabricated.',
            'Prepared saves use future inspection clocks,not extended gameplay lifetimes. Metadata-only atomic rejection does not claim every legacy malformed-save path is atomic. All possible nested callback/geometry/late-clock edges and full-class/property acceptance remain open.',
            'Four independent proposals remain pending. Final mirror/weapon/projectile/impact effects,audio and talent/hotkey icons remain separately needed;identified native/shared placeholders only.'],inputs=inputs)
    target=ROOT/'tools/reference/mirrored-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Mirrored world evidence stale'
    else:target.write_text(encoded)
    print('Mirrored world:C3/I5/S8/N6,48markers/15connections/2reloads,nine saved missiles;192packet controls;complete rule pending,229handlers.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
