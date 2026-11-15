#!/usr/bin/env python3
"""Authenticate the prepared Eldritch world rule without new reference reads."""
import argparse
from collections import Counter
import hashlib
import json
import subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/eldritch-blast-world'
PARENT='00dceaac34b17c196792542fc1fee1028ff37786'

def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json')
    snapshot(STAGE/'source-snapshot-6.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-6.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    previous=pin(ROOT/'.local/class-skills/bind-demon-learned/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')}
    now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    added={'src/Sanctuary.Content/EldritchBlastWorld.cs','src/Sanctuary.Content/WeaponHexRefresh.cs','tests/Sanctuary.Integration/EldritchBlastWorldChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('HexBane','HexSiphon','HexPurge')}
    assert set(now)-set(old)==added and not set(old)-set(now)
    assert {n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']
    compiled=pin(STAGE/'source-snapshot-4.json')['inputs']['sourceInputs']
    assert {n:h for n,h in compiled.items()if not n.startswith('tests/')}=={n:h for n,h in now.items()if not n.startswith('tests/')}
    for kind,number in (('content',3),('integration',5)):
        path=STAGE/f'{kind}-build-{number}.log';pin(path)
        assert all(s in path.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/bind-demon-learned-native-evidence.json')
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'eldritch-blast-world-check':1,'eldritch-blast-world-check-result':1,
        'eldritch-blast-world-reload-check':2,'eldritch-blast-world-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    for phrase in ('Eldritch world:real +skills capture/fractional payment,three existing Hex types,shared magic roll,actual FP8 recovery',
        'Eldritch world:explicit native encounter magic immunity retains Hex maintenance with zero magic recovery;unmapped Hell Lich source still refuses;'):
        assert text.count(phrase)==1
    assert text.count('Eldritch world:original capture/absolute clocks/seed/fractional wounds and mana/original helm/three Hex attachments persist')==2
    fixture=pin(STAGE/'eldritch-blast-world-fixtures.json');assert fixture['Generation']==2 and len(fixture['Targets'])==5
    assert fixture['State']['Numbers']['Rank']==21 and fixture['State']['Numbers']['DurationFrames']==1250
    # Keep failed tested sources and binaries. N2/N3 differ only in the fixture;
    # C3 stays byte-identical through the final accepted native run.
    history=[]
    for number in (1,2,3):
        archive=STAGE/f'native-{number}';earlier=pin(archive/'birth-inputs.json')
        pin(archive/'birth-native-checks.log');pin(archive/f'native-run-{number}.log')
        assert ': failed' in (archive/'birth-native-checks.log').read_text()
        for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
        for kind in ('Content','Integration'):
            assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        if number>=2:assert earlier['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        history.append(str(archive.relative_to(ROOT)))
    for name in ('content-build-1.log','content-build-2.log','integration-build-1.log','integration-build-2.log',
        'integration-build-3.log','integration-build-4.log','source-snapshot-1.json','source-snapshot-2.json','source-snapshot-3.json','source-snapshot-5.json'):
        pin(STAGE/name)
    failed=pin(STAGE/'source-snapshot-1.json')
    for n,h in failed['inputs']['sourceInputs'].items():assert e.sha(STAGE/'compile-1/source'/n)==h,n
    assert 'error CS0121' in (STAGE/'content-build-1.log').read_text()
    review=pin('tools/reference/eldritch-blast-world-source-review.json')
    manifest=pin('tools/reference/d2r-named-equipment/sources.json')
    for name,key in (('skills','skill'),('missiles','missile')):
        path=f'tools/reference/d2r-named-equipment/json/{name}.json';table=pin(path)
        assert e.sha(ROOT/path)==manifest['files'][f'json/{name}.json']['sha256']
        assert review[key]==table['384'if name=='skills'else'eldritchblastnova']
    assert review['referenceVersion']=='3.3.93847'and not review['approved']and review['newProcessReads']==0
    capture=pin(ROOT/review['currentCapture']['path'])
    for n,h in capture['artifacts'].items():assert e.sha(ROOT/n)==h,n
    assert not capture['records'][0]['allUnwindFragmentsCaptured']
    client=pin('tools/reference/bind-demon-client-evidence.json');cm=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']and len(cm['patchHashes'])==44
    for kind in ('patchHashes','sharedInputs'):
        for n,h in cm[kind].items():assert e.sha(ROOT/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(added|changed),'tests/check_eldritch_blast_world.py','tools/record_eldritch_blast_world.py',
        'docs/eldritch-blast-world-rule-proposal.md','docs/eldritch-blast-world-checkpoint.md','docs/class-skills-resume-checkpoint.md',
        'docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','docs/class-skill-roadmap.md'):
        pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.EldritchBlast'not in code and 'CombatAbility.BindDemon'in code
    result=dict(referenceVersion='3.3.93847',component='Prepared Eldritch Blast pulse/Hex refresh/recovery world rule',handlers=227,warlockHandlers=18,completedClasses=[],
        learnedEnabled=False,proposalApproved=False,automaticTickEnabled=False,actualInputs=0,authenticatedSessions=3,
        contentBuild=3,integrationBuild=5,sourceSnapshot=6,nativeRun=4,nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[252],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        retainedFailedRuns=history,frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43),'isolatedClientPatches':44,'installed':False},
        established=['Prepared paid fractional release/captured actual +skills/hard synergies survives later gear/training changes;first/exact/repeated/late/offline/expiry pulse grid and saved seed use existing services.',
            'Three actual existing Hex attachments retain ownership/numeric state/Purge reaction and charges while extending captured durations. Legacy unknown duration and expired/removed attachment refuse;malformed clock/numbers reject atomically.',
            'Native FP256 shared magic roll,post-loss recovery/caps and unhexed/outside filtering pass. Existing periodic event boundary prevents direct-hit reactions. Explicit encounter magic immunity refreshes Hex without magic damage/recovery;captured potion additions damage independently without magic leech.',
            'Actual Hell Lich Lord exposes existing unmapped source-identity/aura eligibility boundary despite its numeric magic resistance;no native mapping changes. Internal/dead owners stop. Original saved capture/gear/resources/three Hex states survive two real reloads without replay under a documented future inspection clock.',
            'C1compile ambiguity and three failed fixture runs retained. Stronger-buff recast/source identity/future attachment validity/zero-body corpse setup corrected in tests;C3production byte-identical since N2. Existing item/NPC resource save controls retain;no kernel/client rebuild/newtimer/install/imagegen.'],
        limits=['Whole pulse/recovery/event/world proposal remains pending. No learned SC/left/right/quick/automatic tick integration or actual socket/graphical cast is claimed;Bind remains approved/playable.',
            'Current do165 remains partial92of1198bytes. Snapshot/order/mastery/leech/native geometry/recipient/event/nova-role choices are explicit adaptations,not recovered D2R parity. Nonzero mastery was supplied in a prepared modifier control,not measured from native magic-mastery gear.',
            'Saved fixture dates are explicitly retained inspection dates,not proof of natural one-hour gameplay duration or automated offline aging. Legacy old Hex duration is not reconstructed. Full safe-area/class-change/respec/PvP/special recipients/native mappings and class acceptance need later qualification.',
            'Four earlier proposals remain frozen/pending. Handler227/Warlock18/no class certified. Installed newer D2R never replaces3.3.93847;separate final effects/audio/icons remain missing.'],inputs=inputs)
    out=ROOT/'tools/reference/eldritch-blast-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Eldritch Blast world evidence stale'
    else:out.write_text(encoded)
    print('Prepared Eldritch world:C3/I5/S6/N4,15markers/3connections/2reloads;227handlers;complete rule pending.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
