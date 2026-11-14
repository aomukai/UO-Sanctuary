#!/usr/bin/env python3
"""Authenticate native room/search -> source root -> actual timed NPC hits."""
import argparse,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-native-search'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ('content','integration'):
        log=STAGE/f'{kind}-build-1.log';pin(log)
        assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/goatman-native-combat-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'goatman-native-search-check':1,
        'goatman-native-search-reload-check':2,'goatman-native-search-check-result':1,'goatman-native-search-reload-check-result':2,
        'goatman-native-combat-check':1,'goatman-native-combat-reload-check':2,'goatman-native-combat-check-result':1,'goatman-native-combat-reload-check-result':2})
    e.cohort(STAGE,expected,21);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    log=(STAGE/'birth-native-checks.log').read_text();decisions=[int(n)for n in re.findall(r'Goatman native search:(\d+) actual room/search/scorer/root decisions select all5timed actions',log)]
    assert len(decisions)==3 and all(n>=5 for n in decisions)
    saved=pin(STAGE/'goatman-native-search-fixtures.json')
    assert saved['Generation']==2 and saved['Controller']['Mode']==1
    assert json.loads(saved['Birth'])['Snapshot']['Birth']['MaximumLife256']-saved['Life']==257
    assert pin(STAGE/'goatman-native-combat-fixtures.json')['Generation']==2
    for name in ('src/Sanctuary.Content/GoatmanNativeSearch.cs','src/Sanctuary.Content/GoatmanNativeDecision.cs','src/Sanctuary.Content/GoatmanNativeCombat.cs',
        'src/Sanctuary.Content/GenericPetTraversal.cs','src/Sanctuary.Content/GenericPetSearchWorld.cs','src/Sanctuary.Content/GenericPetSearchNested.cs',
        'tests/Sanctuary.Integration/GoatmanNativeSearchChecks.cs','tests/check_goatman_native_search.py','tools/record_goatman_native_search.py',
        'docs/goatman-native-search-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'data/Sanctuary/Balance/generic-pet-search-monsters.json','data/Sanctuary/Balance/generic-pet-search-monster2.json',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/generic-pet-traversal-source-evidence.json'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Goatman native rooms/identity/enemy search through source decisions and timed combat',
        handlers=221,warlockHandlers=12,completedClasses=[],learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,
        worldMappingApproval='Prepared adaptation;review with remaining native host before automatic admission.',
        nativeMarkers=21,markers=dict(expected),authenticatedSessions=6,boots=3,reloads=2,saves=3,sourceDecisionsPerBoot=decisions,
        contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,
        newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Actual native mobile membership in adjacent registered-size rooms traverses the pinned descriptor1 kind0 source algorithm.',
            'Real owner/birth/source positions,target metadata/allegiance/safety,size-shrunk native LOS feed source search/scorer/weighted root and all5actual timed actions.',
            'Adjacent room and stable serial tie selection;controlled ally,invulnerable,other-facet and historical corpse rejection;actual native wall LOS.',
            'Stale native owner combat references are cleared through source refresh and cannot be resurrected by an earlier wrapper snapshot.',
            'Original idle controller,birth,Frenzy,wounds and RNG survive2reloads;native timed-combat regression passes in the same cohort.'],
        limits=['Native room/list order,allegiance/LOS projection and untagged-area fallback are explicit proposed Sanctuary mappings,not verified D2 room/AI flags.',
            'Complete AI entry,doors,movement/follow/warp,birth path,mask64/scratch producers,event clock/suspension/reactivation and cap/casting remain unfinished.',
            'Proxy/monster-owner descriptors,corpse/buff skills,object/missile/tile and unsupported source branches refuse.',
            'No automatic/learned summon or complete class certification;final effects/audio/icons remain separate.'],inputs=inputs)
    p=ROOT/'tools/reference/goatman-native-search-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded,'Native search evidence stale'
    else:p.write_text(encoded)
    print('Goatman native search:21markers/6authenticatedsessions/2reloads;real room/search/root reaches all5timed actions;complete host remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
