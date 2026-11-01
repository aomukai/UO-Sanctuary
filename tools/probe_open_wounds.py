#!/usr/bin/env python3
"""Execute complete older Open Wounds callback/math/shared state factory."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/open-wounds-source'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def rate(level):
    n = max(level, 1) - 1
    result = 40
    for count, step in ((14, 9), (15, 18), (15, 27), (15, 36), (max(0, n), 45)):
        use = min(n, count)
        result += use * step
        n -= use
    return result


def build(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs, bodies = {}, {}

    def source(relative):
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return raw.decode()

    def routine(text, name):
        body = function(text, name)
        bodies[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    scaffold = ROOT / 'tools/probe_battlecry_reference.py'
    prefix = next(n.value for n in ast.walk(ast.parse(scaffold.read_text()))
                  if isinstance(n, ast.Constant) and isinstance(n.value, str) and n.value.startswith('#include <algorithm>'))
    prefix = prefix.replace('struct D2UnitStrc{', 'struct D2UnitStrc{int woundFlags=0;')
    old_percentage = 'int MONSTERUNIQUE_CalculatePercentage(int v,int p,int d){return int(int64_t(v)*p/d);}'
    assert prefix.count(old_percentage) == 1
    prefix = prefix.replace(old_percentage, routine(source('source/D2Game/src/MONSTER/MonsterUnique.cpp'), 'MONSTERUNIQUE_CalculatePercentage'))
    items = source('source/D2Game/src/SKILLS/SkillItem.cpp')
    skills = source('source/D2Game/src/SKILLS/Skills.cpp')
    cpp = prefix + r'''
constexpr int STAT_LEVEL=12,STAT_HPREGEN=74,STATE_OPENWOUNDS=62;
struct D2DamageStrc{uint32_t dwHitFlags=0;};
int draws=0;
uint32_t ITEMS_RollRandomNumber(int*){draws++;return static_cast<uint32_t>(roll);}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* p,unsigned flags){return (p->woundFlags&flags)!=0;}
'''
    for text, name in ((skills, 'sub_6FD10EC0'), (items, 'SKILLITEM_CalculateOpenWoundsHpRegen'), (items, 'SKILLITEM_EventFunc15_OpenWounds')):
        cpp += routine(text, name)
    cpp += r'''
int main(){bool first=true;std::printf("[");
for(int chance:{0,1,5,100,125})for(int raw:{0,4,5,49,99,100,-1})for(int level:{0,1,15,16,30,31,45,46,60,61,99,127})
for(int type:{UNIT_PLAYER,UNIT_MONSTER,UNIT_OBJECT})for(int event:{5,6})for(int elite:{0,2,4,8,12})for(int oldKind:{0,1,2,3}){
 D2UnitStrc attacker,target;attacker.dwUnitId=7;attacker.stats[STAT_LEVEL]=level;attacker.stats[135]=chance;target.dwUnitType=type;target.woundFlags=elite;
 D2StatListStrc old;old.state=STATE_OPENWOUNDS;old.skill=oldKind==3?82:0;old.rank=oldKind==2?2:1;old.creator=999;old.expire=777;old.flags=10;old.stats[STAT_HPREGEN]=-13;
 target.list=oldKind?&old:nullptr;curseMask=false;validState=validMon=validModes=true;mon.dwMonStatsFlags=0;modes.dwFlags=1;state.nStateFlags[0]=3;draws=removals=events=0;roll=raw;
 int result=SKILLITEM_EventFunc15_OpenWounds(&game,event,&attacker,&target,nullptr,135<<16,0);auto* p=target.list;
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u]",chance,static_cast<uint32_t>(raw),level,type,event,elite,oldKind,result,draws,p?p->creator:0,p?p->expire:0,p?p->stats[STAT_HPREGEN]:0,removals,p?p->flags:0);
}
std::printf("],\"gates\":[");first=true;
for(int guard=0;guard<10;guard++)for(int oldKind:{0,1}){
 D2UnitStrc attacker,target;attacker.dwUnitId=7;attacker.stats[STAT_LEVEL]=99;attacker.stats[135]=100;target.dwUnitType=UNIT_MONSTER;
 D2StatListStrc old;old.state=STATE_OPENWOUNDS;old.skill=0;old.rank=1;old.creator=999;old.expire=777;old.stats[STAT_HPREGEN]=-13;target.list=oldKind?&old:nullptr;
 curseMask=false;validState=guard!=3;validMon=guard!=4;validModes=guard!=6;mon.dwMonStatsFlags=guard==5?4:0;modes.dwFlags=guard==7?0:1;state.nStateFlags[0]=3;target.attract=guard==8;target.stats[STAT_CURSE_RESISTANCE]=guard==9?100:0;draws=removals=events=0;roll=0;
 int result=SKILLITEM_EventFunc15_OpenWounds(&game,5,guard==1?nullptr:&attacker,guard==2?nullptr:&target,nullptr,135<<16,0);auto* p=target.list;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",guard,oldKind,result,draws,p?p->creator:0,p?p->expire:0,p?p->stats[STAT_HPREGEN]:0);
}std::printf("]}\n");}
'''
    # Separate arithmetic/callback rows from the target-factory gates.
    cpp = cpp.replace('std::printf("[");', r'std::printf("{\"cases\":[");', 1)
    STAGE.mkdir(parents=True, exist_ok=True)
    path = STAGE / 'probe.cpp'
    path.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], check=True, stdout=log, stderr=log)
    with (STAGE / 'sanitizer.log').open('w') as log:
        data = json.loads(subprocess.check_output([str(STAGE / 'probe')], stderr=log))
    assert not (STAGE / 'sanitizer.log').read_text()
    refreshes = replacements = 0
    for chance, raw, level, typ, event, elite, old, result, draws, creator, expiry, regen, removed, flags in data['cases']:
        success = chance > 0 and raw % 100 < chance
        value = rate(level)
        if typ == 0:
            value //= 4
            if event == 6:
                value //= 2
        elif typ == 1 and elite & 12:
            value //= 2
        same = old in (1, 2)
        expected_creator = 999 if old and (not success or same) else 7 if success else 0
        expected_expiry = 777 if old and (not success or old == 2) else 300 if success else 0
        expected_regen = -13 if old and (not success or same) else -value if success else 0
        assert (result, draws, creator, expiry, regen, removed, flags) == (
            int(success), int(chance > 0), expected_creator, expected_expiry, expected_regen, int(success and old == 3), 10 if old or success else 0)
        refreshes += success and old == 1
        replacements += success and old == 3
    for guard, old, result, draws, creator, expiry, regen in data['gates']:
        callback = guard not in (1, 2)
        factory = callback and guard not in (3, 4, 5, 6, 7)
        assert (result, draws, creator, expiry, regen) == (
            int(callback), int(callback), 999 if old else 7 if factory else 0,
            300 if factory else 777 if old else 0, -13 if old else -rate(99) if factory else 0)
    assert len(data['cases']) == 50400 and len(data['gates']) == 20
    table = ROOT / 'tools/reference/d2r-named-equipment/json/states.json'
    state_row = json.loads(table.read_text())['openwounds']
    assert state_row['*ID'] == 62 and state_row['active'] == state_row['life'] == 1 and not state_row.get('curse')
    cases = STAGE / 'cases.json'
    cases.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    for p in (Path(__file__), scaffold, table, ROOT / 'tools/reference/blood-pet-event-reuse-review.json'):
        inputs[str(p.relative_to(ROOT))] = sha(p)
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], callbackContexts=len(data['cases']), factoryGateContexts=len(data['gates']),
        equalRankRefreshContexts=refreshes, differentSkillReplacementContexts=replacements, functions=bodies, inputs=inputs,
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in (path, cases, STAGE / 'compiler.log', STAGE / 'sanitizer.log')},
        scope='Complete unchanged older callback/math/factory under explicit retained unit/stat/row/RNG/event/storage services; not the full game or modern executable.',
        established=[
            'Chance<=0draws0;positive chance draws once before state eligibility,including100percent. The callback ignores damage/skill-level inputs and reports chance success even when its state factory rejects.',
            'Level clamps only to1,not99;ordinary rate uses9/18/27/36/45tiers plus40. Player quarter/ranged half and monster flag0xC half reductions use integer ordering. Payload is200frames negative HP regeneration/state62/skill0/rank1.',
            'Same state/skill/rank refreshes only expiry,event retention and original creator/rate;stronger old rank rejects;different skill replaces. Missing row,NPC,nonattackable receiver gates occur after RNG. Attract and curse resistance do not suppress the noncurse OpenWounds state.'],
        remaining=[
            'The prior host property reset creator/rate/fraction and capped level99. Native host comparisons must establish corrected shared state/arithmetic separately from this older source proof;modern differences remain explicit.',
            'Negative-regeneration versus existing whole-HP bleed scheduling,overdue settlement during pre-health events,expiry/cure/reload/source-credit and lethal cases require host composition. Callback/factory proof is not end-to-end damage/event/regen proof.',
            'Modern callback equivalence,real subscriber order/global seeds/PvP/client and Blood hard20LifeTap target-cast integration remain separate. This standalone probe enables no callback;runtime Blood hard10acceptance is recorded separately.'])
    output = ROOT / 'tools/reference/open-wounds-source-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Open Wounds:{len(data["cases"])}complete callback/factory contexts,{len(data["gates"])}gates;ASan/UBSan passed. Runtime acceptance is recorded separately.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
