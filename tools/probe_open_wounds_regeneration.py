#!/usr/bin/env python3
"""Reuse complete monster regeneration for poison+wounds/replenish/credit."""
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'open-wounds-regen-source'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def build():
    prior_path = ROOT / 'tools/reference/poison-state-engine-evidence.json'
    prior = json.loads(prior_path.read_text())
    seed = BASE / 'poison-state-source-probe.cpp'
    prefix = seed.read_text().split('int main(){')[0]
    source = BASE / 'd2moo-pinned-source/source/D2Game/src/MONSTER/MonsterMode.cpp'
    name = 'D2GAME_MONSTER_ApplyStatRegen_6FC63440'
    assert sha(source) == prior['sourceHashes']['source/D2Game/src/MONSTER/MonsterMode.cpp']
    assert function(prefix, name) == function(source.read_text(), name)
    assert hashlib.sha256(function(prefix, name).encode()).hexdigest() == prior['blockHashes'][name]
    old = 'D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int state){return hasList&&state==STATE_POISON?&list:nullptr;}'
    assert prefix.count(old) == 1
    prefix = prefix.replace(old, 'D2StatListStrc woundList;bool hasWounds=false;\nD2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int state){return hasList&&state==STATE_POISON?&list:hasWounds&&state==STATE_OPENWOUNDS?&woundList:nullptr;}')
    cpp = prefix + r'''
int main(){bool first=true;printf("[");
for(int life:{1,128,256,257,1024})for(int basis:{0,64})for(int item:{-8,0,100})for(int poison:{0,16,40})for(int wound:{0,20,200})
for(int blocked:{0,1})for(int inTown:{0,1})for(int delayed:{0,1}){
 defender={};defender.dwUnitType=UNIT_MONSTER;defender.stats[STAT_HPREGEN]=basis+item-poison-wound;defender.stats[STAT_HITPOINTS]=life;
 list={};list.ownerId=77;woundList={};woundList.ownerId=88;hasList=poison>0;hasWounds=wound>0;baseRegen=basis;lifeMask=hasList||hasWounds;town=inTown;prevent=blocked;uninterrupt=delayed;
 killed=deferred=writes=scheduled=removed=0;killOwner=-1;D2GAME_MONSTER_ApplyStatRegen_6FC63440(&game,&defender,0,0);
 printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d],%d,%d,%d,%d]",first?"":",",life,basis,item,poison,wound,blocked,inTown,delayed,defender.stats[STAT_HITPOINTS],killed,killOwner,deferred);first=false;
}printf("]\n");}
'''
    STAGE.mkdir(parents=True, exist_ok=True)
    path = STAGE / 'probe.cpp'
    path.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], check=True, stdout=log, stderr=log)
    with (STAGE / 'sanitizer.log').open('w') as log:
        data = json.loads(subprocess.check_output([str(STAGE / 'probe')], stderr=log))
    assert not (STAGE / 'sanitizer.log').read_text() and len(data) == 2160
    for values, actual, killed, owner, delayed in data:
        hp, basis, item, poison, wound, blocked, town, uninterruptible = values
        delta = (0 if poison or wound else basis) + item - poison - wound
        if blocked and delta >= 0:
            delta = 0
        expected = hp if delta < 0 and hp < 256 and town else min(1024, max(0, hp + delta))
        lethal = expected == 0
        credit = 77 if poison else 88 if wound else -1
        assert (actual, killed, owner, delayed) == (expected, int(lethal and not uninterruptible), credit if lethal and not uninterruptible else -1, int(lethal and uninterruptible))
    cases = STAGE / 'cases.json'
    cases.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=prior['legacyCommit'], contexts=2160,
        compiledUnchangedRoutine={name: prior['blockHashes'][name]},
        inputs={str(p.relative_to(ROOT)): sha(p) for p in (Path(__file__), prior_path, source, seed)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in (path, cases, STAGE / 'compiler.log', STAGE / 'sanitizer.log')},
        established=['One signed frame removes natural regeneration under either life-mask,then sums item replenishment minus poison minus wounds. Prevent-heal suppresses nonnegative recovery,not negative rates. Town fractional-life guard and maxlife1024match the retained services.',
            'Complete routine chooses poison state owner before Open Wounds owner at zero HP,including when the wound contribution is larger. Uninterruptible zero life defers death instead of immediately killing.'],
        remaining=['Retained unit/stat/event/room/lookup/death services are explicit;maxlife1024and supplied flags do not certify full modern state lists/geometry/event/world behavior.',
            'Runtime keeps prior poison receiver/death-delay/PvP limits. Native combined HP,clock,source ownership and persistence need independent verification;source timing/whole engine equivalence is not inferred.'])
    target = ROOT / 'tools/reference/open-wounds-regeneration-source-evidence.json'
    target.write_text(json.dumps(result, indent=2) + '\n')
    print('Open Wounds regeneration:2160complete signed-rate/poison-priority/town/prevent-heal/death-delay contexts pass ASan/UBSan.')


if __name__ == '__main__':
    build()
