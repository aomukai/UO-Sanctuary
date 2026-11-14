#!/usr/bin/env python3
"""Authenticate the executable native Goatman controller proposal."""
import argparse,json,re,struct
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-native-host'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    snaps=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in (1,2,3,4)]
    assert all(s['copiedData']==snaps[0]['copiedData']and s['projectInputs']==snaps[0]['projectInputs']for s in snaps)
    assert {n for n in snaps[0]['sourceInputs']if snaps[0]['sourceInputs'][n]!=snaps[1]['sourceInputs'][n]}=={
        'src/Sanctuary.Content/GoatmanNativeHostCandidate.cs','src/Sanctuary.Content/GoatmanNativeMotion.cs','src/Sanctuary.Content/DeathMarkWorld.cs'}
    assert all(snaps[1]['sourceInputs'][n]==snaps[2]['sourceInputs'][n]for n in snaps[1]['sourceInputs'])
    assert set(snaps[2]['sourceInputs'])-set(snaps[1]['sourceInputs'])=={'tests/Sanctuary.Integration/GoatmanNativeHostChecks.cs'}
    assert {n for n in snaps[2]['sourceInputs']if snaps[2]['sourceInputs'][n]!=snaps[3]['sourceInputs'][n]}=={'src/Sanctuary.Content/DeathMarkWorld.cs','src/Sanctuary.Content/GoatmanNativeSearch.cs','tests/Sanctuary.Integration/GoatmanNativeHostChecks.cs'}
    for kind,num in [('content',3),('integration',2)]:
        log=STAGE/f'{kind}-build-{num}.log';pin(log)
        assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/goatman-native-search-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'reviewed-run-1'/name)
    metadata=pin('tools/reference/generic-pet-secondary-data-evidence.json');raw=ROOT/'.local/class-skills/generic-pet-search-services/monstats.bin';pin(raw)
    assert e.sha(raw)==metadata['artifacts'][str(raw.relative_to(ROOT))]
    b=raw.read_bytes();assert struct.unpack_from('<I',b)[0]==751 and len(b)==4+751*508
    assert pin('data/Sanctuary/Balance/goatman-host-source.json')==[dict(ClassId=742,Flags241=b[4+742*508+241])]
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'goatman-native-host-check':1,
        'goatman-native-host-reload-check':2,'goatman-native-host-check-result':1,'goatman-native-host-reload-check-result':2,
        'goatman-native-search-check':1,'goatman-native-search-reload-check':2,'goatman-native-search-check-result':1,'goatman-native-search-reload-check-result':2})
    e.cohort(STAGE,expected,21);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    rows=[tuple(map(int,r))for r in re.findall(r'Goatman host candidate:(\d+) actual AI-entry/root decisions,(\d+) paths,(\d+) source moves,(\d+) source warps',
        (STAGE/'birth-native-checks.log').read_text())]
    assert len(rows)==3 and all(d>=2 and p>0 and s>0 and w>0 for d,p,s,w in rows)
    saved=pin(STAGE/'goatman-native-host-fixtures.json');assert saved['Generation']==2 and saved['Controller']['Mode']==1
    assert json.loads(saved['Birth'])['Snapshot']['Birth']['MaximumLife256']-saved['Life']==257
    assert saved['Host']['Clock']['Frame']>0 and pin(STAGE/'goatman-native-search-fixtures.json')['Generation']==2
    for name in ('content-build-1.log','GoatmanNativeHostCandidate.cs','GoatmanNativeMotion.cs','DeathMarkWorld.cs','SourceFrameTimeline.cs','reason.txt'):pin(STAGE/'diagnostics-build-1'/name)
    failed=(STAGE/'diagnostics-build-1/content-build-1.log').read_text();assert 'CS0411'in failed and 'CS1503'in failed
    for name in ('src/Sanctuary.Content/GoatmanNativeHostCandidate.cs','src/Sanctuary.Content/GoatmanNativeMotion.cs','src/Sanctuary.Content/DeathMarkWorld.cs',
        'src/Sanctuary.Content/SourceFrameTimeline.cs','src/Sanctuary.Content/GoatmanNativeSearch.cs','tests/Sanctuary.Integration/GoatmanNativeHostChecks.cs','tests/check_goatman_native_host.py','tools/record_goatman_native_host.py',
        'docs/goatman-native-host-proposal.md','docs/goatman-native-search-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/generic-pet-follow-executor-source-evidence.json'):pin(name)
    approval=pin('tools/reference/goatman-native-host-approval.json');assert approval['approved']and approval['candidateCommit']=='0ffb160ae41f08e6a38ca42a1e48f556f3cac874'
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Executable complete native Goatman AI/path/follow/warp/combat/clock controller candidate',
        approval='Approved explicit normal-Goatman native-host adaptation;production wiring remains',handlers=221,warlockHandlers=12,completedClasses=[],
        learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,nativeMarkers=21,markers=dict(expected),authenticatedSessions=6,boots=3,reloads=2,saves=3,
        activityPerBoot=[dict(decisions=d,paths=p,sourceMoves=s,warps=w)for d,p,s,w in rows],
        contentBuild=3,integrationBuild=2,sourceSnapshot=4,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,
        newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Real current AI entry/preprocess/default root runs actual native search/scorer,source paths/approach/follow/warp and timed ordinary NPC damage in one candidate loop.',
            'Distant enemy acquisition/approach/damage,empty-combat following,over-leash warp and publication of source flags128 on the original saved actor.',
            'Actual native unlocked door use,stun pause,native freeze cancellation/thaw rearm and existing Death Mark placement authority.',
            'Four-hour offline phase skip and real roster return/atomic source-room rebind preserve wounds,Frenzy deadlines and randomness;two original-body/clock reloads replay no offline combat or healing.',
            'Captured current primary byte241 supplies run hints;native room/search/all-five-action regressions pass in the same cohort.'],
        limits=['Fresh path masks/defaults,facet planning/native regions,source overlap64 mapping,near-owner search,UO doors,zero adapter bytes/scratch and native clock/lifecycle ordering are explicit approved Sanctuary adaptations.',
            'Normal original Goatman only;unqualified legacy/special/queued/saved-follow/uninterruptible-deferred and other-species contexts refuse.',
            'Clock snapshot is retained by disposable fixture;automatic timer,body persistence,cap/cast and learned input wiring remain uninstalled.',
            'No complete class certification or final effects/audio/icons;installed build/config/saves/native/client remain protected.'],inputs=inputs)
    p=ROOT/'tools/reference/goatman-native-host-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded,'Native host candidate evidence stale'
    else:p.write_text(encoded)
    print('Goatman host candidate:21markers/6sessions/2reloads;complete AI/path/follow/warp/combat/lifecycle loop;adaptation approved;production wiring remains.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
