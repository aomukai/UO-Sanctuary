#!/usr/bin/env python3
"""Bind the NPC action candidate to owned data and focused native acceptance."""
import argparse,json,struct
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-npc-action'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text()) if p.suffix=='.json' else None
    origin=pin('tools/reference/d2r-local-animation-evidence.json')
    raw=STAGE.parent/'bone-barrier-retirement-source/AnimData.d2'
    assert e.sha(raw)==origin['sha256'];pin(raw)
    b=raw.read_bytes();cursor=0;records={};count=0
    for _ in range(256):
        n=struct.unpack_from('<I',b,cursor)[0];cursor+=4
        for _ in range(n):
            r=b[cursor:cursor+160];cursor+=160;count+=1
            code=r[:8].split(b'\0')[0].decode('ascii')
            if code in ('GMA12HS','GMA22HS'):
                frames,speed=struct.unpack_from('<II',r,8);events=[(i,v)for i,v in enumerate(r[16:])if v]
                assert len(events)==1 and events[0][1]==1
                records[code]=dict(code=code,frames=frames,speed=speed,actionFrame=events[0][0])
    assert cursor==len(b) and count==3661
    assert pin('data/Sanctuary/Balance/goatman-animation.json')==[records[n]for n in ('GMA12HS','GMA22HS')]
    monster=pin('tools/reference/d2r-named-equipment/json/monstats.json')['wargoatman']
    secondary=pin('tools/reference/d2r-named-equipment/json/monstats2.json')['wargoatman']
    assert monster['Code']=='GM' and monster['MonStatsEx']=='wargoatman' and secondary['BaseW']=='2hs'
    source=pin('tools/reference/d2r-named-equipment/json/skills.json')
    assert source['417']['aurastate']=='monfrenzy' and source['417']['Param1']==200 and source['417']['Param2']==25
    assert source['417']['aurastatcalc1']==source['417']['aurastatcalc2']==source['417']['aurastatcalc3']=='dm34'
    assert source['419']['calc5']==11 and source['419']['calc6']==30 and source['419']['calc1']=='ln12'
    pin('data/Sanctuary/Balance/warlock-npc-skills.json');pin('data/Sanctuary/Balance/generic-pet-states.json')
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,num in [('content',2),('integration',3)]:
        log=STAGE/f'{kind}-build-{num}.log';pin(log)
        assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    previous=pin('tools/reference/death-cast-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==previous['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'goatman-npc-action-check':3,'goatman-npc-melee-check':3,'frenzy-release-check':3})
    e.cohort(STAGE,expected,18);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert 'Goatman NPC candidate:510 own-row packets/1530 schedules/255 charge caps' in (STAGE/'birth-native-checks.log').read_text()
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','GoatmanNpcActionChecks.cs','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-1'/name)
    assert 'successful cached packet' in (STAGE/'diagnostics-1/birth-native-checks.log').read_text()
    for name in ('tools/reference/goatman-npc-melee-source-evidence.json','tools/reference/frenzy-release-source-evidence.json'):
        pin(name)
    for name in ('src/Sanctuary.Content/GoatmanNpcActionProposal.cs','src/Sanctuary.Content/GoatmanNpcMelee.cs',
                 'src/Sanctuary.Content/BarbarianFrenzyRelease.cs','tests/Sanctuary.Integration/GoatmanNpcActionChecks.cs',
                 'tests/Sanctuary.Integration/GoatmanNpcMeleeChecks.cs','tests/check_goatman_npc_action.py',
                 'tools/record_goatman_npc_action.py','docs/goatman-npc-action-proposal.md',
                 'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md'):
        pin(name)
    assert 'Approved=>false' in (ROOT/'src/Sanctuary.Content/GoatmanNpcActionProposal.cs').read_text()
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='prepared Goatman NPC Frenzy/Cleave actions',approved=False,
        handlers=221,warlockHandlers=12,completedClasses=[],learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,
        animation=dict(rawSha256=origin['sha256'],archivePath=origin['archivePath'],parsedRecords=count,selected=list(records.values())),
        nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=3,
        sourceSnapshot=4,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,
        newPersistenceIds=0,serializationVersionChanged=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        checks=['510 own-row Frenzy/Cleave packets,1530 owned animation schedules and255 shared-writer charge caps on each boot.',
                'Actual shared FP256 HP with explicit empty fixture event leaves;misses finish without successful HP commit.',
                'Fan membership/dedup/order,reentrant once-only release,late ticks,miss,expiry,movement,inactive host and nested cancellation/freeze.',
                'Retained24000 Stun/Berserk packet and15024 complete legacy Frenzy service comparisons on each boot.'],
        limits=['Prepared NPC action adaptation awaits approval;current startup67/68/latch/mode selection remain unverified.',
                'Fixture world/service leaves do not certify full production demon AI/path/clock/contact/event admission.',
                'Actor-owned Frenzy state is transient here;durable host state and movement/attack/other-animation consumers remain required.',
                'No learned summon route;no complete class certified;effects/audio and talent/hotkey icons remain separate.'],inputs=inputs)
    path=ROOT/'tools/reference/goatman-npc-action-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Goatman NPC action receipt stale'
    else:path.write_text(encoded)
    print('Goatman NPC candidate:18markers/3boots/2reloads;shared actions/stat writer verified,approval/native host pending;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
