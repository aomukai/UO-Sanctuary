#!/usr/bin/env python3
"""Authenticate learned Goatman casting,automatic saved AI and real input."""
import argparse,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-cast'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-8.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    snaps=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in (1,2,3,4,5,6,7,8)]
    assert all(s['projectInputs']==snaps[0]['projectInputs']for s in snaps)
    assert all(s['copiedData']==snaps[0]['copiedData']for s in snaps[:4])
    assert {n for n in snaps[3]['copiedData']if snaps[3]['copiedData'][n]!=snaps[4]['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert {n for n in snaps[0]['sourceInputs']if snaps[0]['sourceInputs'][n]!=snaps[1]['sourceInputs'][n]}=={'src/Sanctuary.Content/WarlockGoatmanCasting.cs'}
    assert {n for n in snaps[1]['sourceInputs']if snaps[1]['sourceInputs'][n]!=snaps[2]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/GoatmanCastChecks.cs'}
    assert {n for n in snaps[2]['sourceInputs']if snaps[2]['sourceInputs'][n]!=snaps[3]['sourceInputs'][n]}=={'src/Sanctuary.Content/WarlockGoatmanWorld.cs'}
    assert snaps[3]['sourceInputs']==snaps[4]['sourceInputs']
    assert {n for n in snaps[4]['sourceInputs']if snaps[4]['sourceInputs'][n]!=snaps[5]['sourceInputs'][n]}=={'src/Sanctuary.Content/WarlockGoatmanBody.cs'}
    assert {n for n in snaps[5]['sourceInputs']if snaps[5]['sourceInputs'][n]!=snaps[6]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/GoatmanCastChecks.cs'}
    assert {n for n in snaps[6]['sourceInputs']if snaps[6]['sourceInputs'][n]!=snaps[7]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/GoatmanBodyChecks.cs'}
    assert all(s['copiedData']==snaps[4]['copiedData']for s in snaps[4:])
    for kind,num in [('content',5),('integration',5)]:
        log=STAGE/f'{kind}-build-{num}.log';pin(log)
        assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/goatman-native-host-native-evidence.json');approval=pin('tools/reference/goatman-native-host-approval.json');assert approval['approved']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'goatman-cast-check':1,'goatman-cast-reload-check':2,
        'goatman-cast-check-result':1,'goatman-cast-reload-check-result':2,'goatman-native-host-check':1,'goatman-native-host-reload-check':2,
        'goatman-native-host-check-result':1,'goatman-native-host-reload-check-result':2,'goatman-body-check':1,'goatman-body-reload-check':2})
    for kind in ('right','quick'):
        expected[f'goatman-cast-{kind}-check']=3;expected[f'goatman-cast-{kind}-result']=3
    e.cohort(STAGE,expected,36);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-5.log')
    text=(STAGE/'birth-native-checks.log').read_text()
    assert len(re.findall(r'goatman-cast-(?:right|quick): automatic original demon attacked through saved source host',text))==6
    saved=pin(STAGE/'goatman-cast-fixtures.json');assert saved['Generation']==2 and saved['Maximum']-saved['Life']==257
    state=json.loads(saved['Text']);assert state['LearnedCast']and state['Controller']['Profile']=='native-goatman-host-v1'and state['Controller'].get('Fault')is None
    assert pin(STAGE/'goatman-native-host-fixtures.json')['Generation']==2
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-4.log','goatman-cast-fixtures.json','goatman-native-host-fixtures.json','reason.txt'):pin(STAGE/'reviewed-run-4'/name)
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','GoatmanCastChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-1'/name)
    assert 'Learned talent/mouse/quick bindings unavailable'in (STAGE/'diagnostics-1/birth-native-checks.log').read_text()
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','WarlockGoatmanWorld.cs','WarlockGoatmanCasting.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-2'/name)
    assert 'Missing or changed explicit NPC attack RNG context'in (STAGE/'diagnostics-2/birth-native-checks.log').read_text()
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','WarlockGoatmanBody.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-3'/name)
    assert 'CheckCombatTime'in (STAGE/'diagnostics-3/native-run-3.log').read_text()
    for name in ('content-build-1.log','WarlockGoatmanCasting.cs','reason.txt'):pin(STAGE/'diagnostics-build-1'/name)
    assert 'CS1503'in (STAGE/'diagnostics-build-1/content-build-1.log').read_text()
    for name in ('src/Sanctuary.Content/WarlockGoatmanWorld.cs','src/Sanctuary.Content/WarlockGoatmanCasting.cs','src/Sanctuary.Content/WarlockGoatmanBody.cs',
        'src/Sanctuary.Content/WarlockCombat.cs','src/Sanctuary.Content/GoatmanNativeHostCandidate.cs','tests/Sanctuary.Integration/GoatmanCastChecks.cs',
        'tests/Sanctuary.Integration/GoatmanBodyChecks.cs','tests/check_goatman_cast.py','tools/record_goatman_cast.py','tools/build_skill_roadmap.py','docs/goatman-cast-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/class-skill-status.json',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json'):pin(name)
    status=pin('data/Sanctuary/Balance/class-skill-status.json');assert any(v.get('talentId')=='warlock.summongoatman'and v.get('implementation')=='handler-present'for v in status['skills'])
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Learned Summon Goatman,automatic saved source host and shared demon pool',
        handlers=222,warlockHandlers=13,completedClasses=[],handlerEnabled=True,automaticGoatmanAiEnabled=True,
        nativeMarkers=36,markers=dict(expected),authenticatedSessions=12,actualInputs=6,boots=3,reloads=2,saves=3,
        contentBuild=5,integrationBuild=5,sourceSnapshot=8,nativeRun=5,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,
        newTimers=1,newPersistenceIds=0,serializationVersionChanged=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Learned right-mouse/quick ordinary Warlock SC and source-forbidden left binding;actual input emits original automatic source-controlled demon and real enemy HP/notifications.',
            'Once-only fractional payment after successful body/host creation;unpaid windup cancellation,underfunded/late-mana/late-terrain failure preserve roster.',
            'Hard Demonic Mastery0/5/10 shared cap1/2/3 and oldest saved-order replacement across actual Goatman/Tainted bodies;birth snapshots remain frozen.',
            'Optional omitted host/learned JSON leaves legacy prepared bodies unadopted;new saved original controller/birth/Frenzy/wounds/RNG/bindings survive2actualreloads and real login/reactivation without offline replay/healing.',
            'Native Fists/CreatureSwing requests are delay-only and change neither HP nor actor RNG;source host alone owns contacts.',
            'Production40ms runtime admits only saved hosts,delivers active actors in clock/serial order,and explicitly parks/cancels/faults unsupported contexts;approved complete-native-host and legacy prepared-body/difficulty/appearance regressions pass.'],
        limits=['Complete Warlock/class certification remains outstanding;remaining normal/special recipient/context gaps stay explicit and can fault the host.',
            'Ground placement reuses the native bounded adapter;exact D2 room/random-search order remains unverified.',
            'Tainted/Defiler learned hosts and Bind Demon remain separate;their footprints/flying/pools are not silently generalized.',
            'Final effects/audio/talent/hotkey icons remain separate identified placeholders;installed build/saves untouched.'],inputs=inputs)
    p=ROOT/'tools/reference/goatman-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded,'Learned Goatman evidence stale'
    else:p.write_text(encoded)
    print('Learned Goatman:36markers/6actualright-quickinputs/2reloads;automatic original source AI,sharedcap/payment/save;222/240handlers,Warlock13/30.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
