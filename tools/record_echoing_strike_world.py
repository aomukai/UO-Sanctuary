#!/usr/bin/env python3
"""Authenticate the complete, still-gated Echoing Strike world candidate."""
import argparse
import hashlib
import json
import subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/echoing-strike-world'
PARENT='1ed238ca6c8dd90257d35da8856638d2a552388b'

def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return e.load(p) if p.suffix=='.json' else None
    born=pin(STAGE/'birth-inputs.json')
    snapshot(STAGE/'source-snapshot-8.json',STAGE/'birth-inputs.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    previous=pin(ROOT/'.local/class-skills/echoing-strike-inputs/birth-inputs.json')
    parent=pin('tools/reference/echoing-strike-inputs-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items() if n.endswith('.cs')}
    now={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    added={'src/Sanctuary.Content/EchoingStrikePackets.cs','src/Sanctuary.Content/EchoingStrikeWorld.cs',
           'src/Sanctuary.Content/EchoingStrikeCasting.cs','tests/Sanctuary.Integration/EchoingStrikeWorldChecks.cs'}
    assert set(now)-set(old)==added and not set(old)-set(now)
    assert {n for n,h in old.items() if now[n]!=h}=={'src/Sanctuary.Content/ClassCombat.cs'}
    assert born['copiedData']==previous['copiedData']
    for kind,number in (('content',3),('integration',7)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log)
        assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'echoing-strike-inputs-check':1,'echoing-strike-inputs-reload-check':2,
        'echoing-strike-world-check':1,'echoing-strike-world-check-result':1,
        'echoing-strike-world-reload-check':2,'echoing-strike-world-reload-check-result':2})
    text=e.cohort(STAGE,expected,18)
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-5.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    for phrase,count in (('Echoing world:10independent packet count/critical controls',1),
        ('Echoing world:source fan/full20update outbound',1),('Echoing world:prepared SC cancellation/late mana/movement/once-only release',1),
        ('Echoing world:original saved flight IDs/packets/critical/RNG/source stats/clock/return budget/weapon/fractional wounds+mana retain',2)):
        assert text.count(phrase)==count,phrase
    saved=pin(STAGE/'echoing-strike-world-fixtures.json')
    state=saved['State'];flights=state['Flights'];leases=state['Leases']
    assert saved['Generation']==2 and len(flights)==len(leases)==2 and saved['TargetLife']>0
    assert saved['Life']%256 and saved['Mana']%256
    assert {f['Owner'] for f in flights}=={saved['Owner']}
    assert {f['Packet']['Input']['Weapon'] for f in flights}=={saved['Weapon']}
    assert {l['Cast'] for l in leases}=={f['Id'] for f in flights}
    assert {l['Target'] for l in leases}=={saved['Target']}
    assert all(l['UntilUtc']>flights[0]['NextUtc'] for l in leases)
    assert len({f['Cast'] for f in flights})==1 and [f['Ordinal'] for f in flights]==[0,1]
    history=[]
    for run in (1,2,3,4):
        archive=STAGE/f'native-{run}';earlier=pin(archive/'birth-inputs.json')
        pin(archive/'birth-native-checks.log');pin(archive/f'native-run-{run}.log')
        for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
        for kind in ('Content','Integration'):
            assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        if run<4:assert ': failed' in (archive/'birth-native-checks.log').read_text()
        else:
            e.cohort(archive,expected,18)
            assert {n for n,h in earlier['sourceInputs'].items() if h!=born['sourceInputs'][n]}=={'tests/Sanctuary.Integration/EchoingStrikeWorldChecks.cs'}
            assert earlier['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        history.append(str(archive.relative_to(ROOT)))
    # Content C3 was frozen before integration I3 and is unchanged in final S8.
    c3=pin(STAGE/'source-snapshot-4.json')
    assert {n:h for n,h in c3['inputs']['sourceInputs'].items() if n.startswith('src/')}=={n:h for n,h in now.items() if n.startswith('src/')}
    for n in range(1,9):pin(STAGE/f'source-snapshot-{n}.json')
    for kind,last in (('content',3),('integration',7)):
        for n in range(1,last+1):pin(STAGE/f'{kind}-build-{n}.log')
    review=pin('tools/reference/echoing-strike-world-source-review.json')
    assert review['approved'] is False and review['learnedEnabled'] is False
    assert review['newPersistenceProfile']==253 and review['newPersistenceVersion']==review['newTimers']==review['newLiveReferenceReads']==0
    pin('data/Sanctuary/Balance/echoing-strike-profile.json')
    pin('tools/reference/echoing-strike-inputs-source-review.json')
    client=pin('tools/reference/eldritch-blast-cast-client-evidence.json')
    cm=pin(ROOT/client['inputManifest']['path'])
    assert inputs[client['inputManifest']['path']]==client['inputManifest']['sha256']
    for kind in ('patchHashes','sharedInputs'):
        for n,h in cm[kind].items():assert e.sha(ROOT/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),
                          ('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n)
            assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest()
            frozen.append(n)
    for n in (*sorted(added),'src/Sanctuary.Content/ClassCombat.cs','tests/check_echoing_strike_world.py',
        'tools/record_echoing_strike_world.py','docs/echoing-strike-world-checkpoint.md','docs/echoing-strike-world-rule-proposal.md',
        'docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md',
        'docs/skill-effects.md','docs/skill-icons.md','docs/class-skill-roadmap.md'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.EchoingStrike' not in code
    result=dict(referenceVersion='3.3.93847',component='Complete proposed Echoing Strike packet/SC/native flight',
        handlers=228,warlockHandlers=19,completedClasses=[],approved=False,learnedEnabled=False,
        contentBuild=3,integrationBuild=7,sourceSnapshot=8,nativeRun=5,nativeMarkers=18,markers=dict(expected),
        boots=3,reloads=2,saves=3,authenticatedSessions=3,actualInputs=0,nativeRebuilt=False,clientRebuilt=False,
        newTimers=0,newPersistenceProfiles=[dict(id=253,version=0)],existingSerializationVersionsChanged=False,
        newLiveReferenceReads=0,newerReferenceSubstitution=False,retainedNativeRuns=history,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),
                      'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},
        verified=[
            'Ten independent packet controls cover five hard-point echo counts and both critical outcomes,Damage+exclusion,source116,physical-only ED and rate-only poison scaling with unchanged length/count/drain.',
            'Connected prepared SC early/cancel/late-mana/movement/once-only payment,actual two-echo damage/live Hexes,immutable release values,no durability wear and independent twenty-frame leases pass.',
            'Shared fan/full twenty-update outward turn,bounded live-owner return,early/offline/absolute-expiry handling,actual skeleton shield block and actual missile barrier return pass.',
            'Original IDs/packets/critical/seeds/source stats/motion/return budgets/weapon/fractional resources and two positive recipient leases retain across two reloads. Advancing into the saved target footprint does not replay damage before lease expiry. Malformed restore rejects atomically.',
            'N1fixture corridor,N2older missile-name lookup,N3FP256 assertion-unit mistakes retained with exact sources/DLLs/logs. C3/I5/N4passed;I7adds positive saved-lease continuation. Final C3/I7/S8/N5clean;no production change since C3.',
            'No old alias/compiler matrix,kernel/client rebuild,automatic timer,installed deployment,protected-current retry or artwork. Installed build/config/saves,accepted client/shared sources and four pending proposal/evidence pairs unchanged.'],
        limits=[
            'Complete proposal remains pending;no learned binding or automatic ticking. Connected prepared controls are not actual socket casts or graphical acceptance;handler coverage stays228/240,Warlock19/30,no class certified.',
            'Current do166 remains0/757readable bytes;do44/hit34/damage16,capture/order/RNG/InitSteps/return/hit-delay/event policies are explicit adaptations,not recovered D2R callback equivalence.',
            'Saved fixtures use a future inspection clock solely for persistence;not one-hour gameplay lifetime. PvP/class pets/unmapped source contexts remain unsupported. Special targets,global seed interleaving and broader class tests remain open.',
            'Final moving/return/contact/cast effects,audio and talent/hotkey icons remain separately missing;identified shared placeholders only.'],inputs=inputs)
    target=ROOT/'tools/reference/echoing-strike-world-native-evidence.json'
    encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Echoing world evidence stale'
    else:target.write_text(encoded)
    print('Echoing world:C3/I7/S8/N5,18markers/3authenticated boots+saves/2reloads;complete rule pending,228handlers.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
