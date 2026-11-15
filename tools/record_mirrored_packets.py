#!/usr/bin/env python3
"""Authenticate prepared Mirrored packets without admitting its missing action."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/mirrored-packets';PARENT='5b76c86a9333b72953fd334c564d0d2b60e20493'

def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-2.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    previous=pin(ROOT/'.local/class-skills/echoing-strike-cast/birth-inputs.json')
    parent=pin('tools/reference/echoing-strike-cast-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    added={'src/Sanctuary.Content/MirroredBladesPackets.cs','tests/Sanctuary.Integration/MirroredPacketChecks.cs'}
    assert set(now)-set(old)==added and not set(old)-set(now)and all(now[n]==h for n,h in old.items())
    assert born['copiedData']==previous['copiedData']
    for kind,n in (('content',1),('integration',2)):
        p=STAGE/f'{kind}-build-{n}.log';pin(p)
        assert all(s in p.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    c1=pin(STAGE/'source-snapshot-1.json');pin(STAGE/'integration-build-1.log')
    assert {n:h for n,h in c1['inputs']['sourceInputs'].items()if n.startswith('src/')}=={n:h for n,h in now.items()if n.startswith('src/')}
    bad='tests/Sanctuary.Integration/MirroredPacketChecks.cs'
    pin(STAGE/'compile-1/source'/bad)
    assert e.sha(STAGE/'compile-1/source'/bad)==c1['inputs']['sourceInputs'][bad]
    assert 'error CS1513'in(STAGE/'integration-build-1.log').read_text()
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'mirrored-packets-check':1,'mirrored-packets-reload-check':2,
        'echoing-strike-cast-check':1,'echoing-strike-cast-check-result':1,'echoing-strike-cast-reload-check':2,'echoing-strike-cast-reload-check-result':2})
    for mode in ('left','right','quick'):expected.update({'echoing-strike-cast-'+mode+'-check':3,'echoing-strike-cast-'+mode+'-result':3})
    text=e.cohort(STAGE,expected,36);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('Client: 127.0.0.1: Connected.')==12
    assert text.count('Mirrored packets:192independent channel/count/critical controls;actual Stone Crusher/bow/crossbow/Gimmershred')==1
    assert text.count('Mirrored packets:four original weapon kinds/items/ammo/quantity/FP256resources/current stats and earlier immutable primary/duplicate packets retain')==2
    for mode in ('left','right','quick'):assert text.count('echoing-strike-cast-'+mode+': windup')==3
    saved=pin(STAGE/'mirrored-packets-fixtures.json');assert saved['Generation']==2 and saved['Controls']==192 and len(saved['Rows'])==4
    assert [r['Captured']['Kind']for r in saved['Rows']]==[0,1,2,3]
    for row in saved['Rows']:
        c=row['Captured'];current=row['Current'];e1=c['Damage']['Elements']
        assert c['Owner']==row['Owner']and c['Weapon']==row['Weapon']and c['Skill']['Rank']==8 and c['Skill']['TotalAttacks']==3
        assert current['Skill']['Rank']==20 and current['Skill']['TotalAttacks']==5 and current['Damage']['Elements']['MagicMin']<e1['MagicMin']
        assert row['Life']%256 and row['Mana']%256 and len(row['PoisonItems'])==2
        assert [e1[n]for n in ('PoisonMin','PoisonMax','PoisonFrames','PoisonCount','LifeDrain')]==[193,193,175,2,5]
        assert [p['Attack']['Ordinal']for p in row['Packets']]==[0,1,2]and all(p['Input']==c for p in row['Packets'])
        assert [p['Damage']['Critical']for p in row['Packets']]==[False,True,True]
    assert pin(STAGE/'echoing-strike-cast-fixtures.json')['Generation']==2
    review=pin('tools/reference/mirrored-packets-source-review.json')
    assert not review['approved']and not review['learnedEnabled']and review['newLiveReferenceReads']==review['newTimers']==0 and not review['newPersistenceProfiles']
    for n in ('tools/reference/mirrored-blades-source-evidence.json','tools/reference/mirrored-blades-native-evidence.json','data/Sanctuary/Balance/mirrored-blades-sequence.json',
        'tools/reference/echoing-strike-world-rule-approval.json','tools/reference/eldritch-blast-cast-client-evidence.json'):pin(n)
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(added),'tests/check_mirrored_packets.py','tools/record_mirrored_packets.py','docs/mirrored-packets-checkpoint.md',
        'docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md',
        'docs/class-skill-roadmap.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.MirroredBlades'not in code and 'CombatAbility.EchoingStrike'in code
    result=dict(referenceVersion='3.3.93847',component='Prepared four-kind Mirrored weapon inputs and damage packets',handlers=229,warlockHandlers=20,completedClasses=[],
        approved=False,learnedEnabled=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,nativeMarkers=36,markers=dict(expected),
        boots=3,reloads=2,saves=3,authenticatedSessions=12,actualInputs=9,actualInputsAbility='EchoingStrike',actualMirroredInputs=0,
        packetControls=192,weaponKinds=['melee','bow','crossbow','throw'],nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],
        existingSerializationVersionsChanged=False,newLiveReferenceReads=0,newerReferenceSubstitution=False,frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},
        verified=['New packet component reuses original selected weapon/owner/species/element/poison/supplement collectors,WeaponMissileDamage and unsigned critical. Every preexisting C#source and runtime data file is unchanged.',
            '192independent primary/duplicate/count-threshold/critical/channel integer controls over four actual weapon kinds pass. Stone Crusher retains18Damage+;source128hasnoEchoing ledger/source116 rule. Poison193/175/count2/impact87and drain5 remain intact.',
            'Actual Warlock/Witch Stone Crusher,bow,crossbow,Gimmershred/Tarnhelm/Bane/Snakecord/Venom Grip captures are immutable across live gear/training/Hex changes. Prepared work spends no life/mana/ammo/throw quantity/durability and applies no hit or item event.',
            'Original equipped weapons/poison gear/backpack helm/arrows/bolts/throw quantity/durability/fractional resources,current captures and earlier primary/duplicate packets retain through two reloads without action replay.',
            'Existing learned Echoing SC/lifecycle/left-right-quick/automatic hits retain:nine actual casts across three boots. I1test with-initializer syntax error is archived;I2fixes it,production C1unchanged. No old176alias/compiler matrix,kernel/client build,timer/profile/version/install/protected-game read/artwork.'],
        limits=['Mirrored action remains unavailable. The final-channel transform/capture/critical policy is an explicitly unapproved component candidate;one complete action/world proposal is still required,not a partial policy decision.',
            'Current start68/do166,emission,target and independent hit/block execution,melee-versus-ranged source timing,ordinary item/Hex/proc order,duplicate Crushing Blow recipient mapping,ammunition/durability,persistence/lifecycle and learned input remain unfinished.',
            'No actual Mirrored contact,damage/proc event,projectile,world actor or graphical acceptance is claimed. Existing Echoing socket evidence does not establish Mirrored input or approve its rules. No class certified.',
            'Public corroboration still declares3.3.93847;older potion/count conflicts remain explicit and missing historical ephemeral copies are disclosed. Four other proposals pending;final mirror/weapon/projectile effects,audio and talent/hotkey icons remain needed.'],inputs=inputs)
    target=ROOT/'tools/reference/mirrored-packets-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Mirrored packet evidence stale'
    else:target.write_text(encoded)
    print('Mirrored packets:C1/I2/S2/N1,192packet controls/36native markers/2reloads;9retained Echoing casts;229handlers,Mirrored action gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
