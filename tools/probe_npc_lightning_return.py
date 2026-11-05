#!/usr/bin/env python3
"""Execute the unchanged lightning-return callback and complete damage/return bodies."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

from probe_current_item_affixes import function
from probe_npc_damage_fill import enum

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'npc-lightning-return-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    parent = BASE / 'fire-wall-return-source/reused-telekinesis.cpp'
    receipt = ROOT / 'tools/reference/fire-wall-return-source-evidence.json'
    assert sha(parent) == json.loads(receipt.read_text())['artifacts'][str(parent.relative_to(ROOT))]
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs, bodies, values = {}, {}, {}

    def read(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            row['sha'] for row in tree['tree'] if row['path'] == relative)
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return raw.decode()

    source = read('source/D2Game/src/UNIT/SUnitDmg.cpp')
    item = read('source/D2Game/src/SKILLS/SkillItem.cpp')
    names = ['SUNITDMG_ExecuteEvents', 'SUNITDMG_CalculateTotalDamage',
             'SUNITDMG_ApplyResistancesAndAbsorb', 'SUNITDMG_ExecuteMissileDamage',
             'sub_6FCC1870', 'SUNITDMG_GetHitClass']
    for name in names:
        bodies[name] = function(source, name)
    callback = 'SKILLITEM_EventFunc10_AttackerTakesLightDamage'
    bodies[callback] = function(item, callback)
    specs = [
        ('source/D2Common/include/D2StatList.h', 'D2C_ItemStats'),
        ('source/D2Common/include/D2States.h', 'D2C_States'),
        ('source/D2Common/include/Units/Units.h', 'D2C_UnitTypes'),
        ('source/D2Common/include/Units/Units.h', 'D2C_UnitFlags'),
        ('source/D2Common/include/Units/Units.h', 'D2C_UnitFlagsEx'),
        ('source/D2Common/include/Units/Player.h', 'D2C_PlayerModes'),
        ('source/D2Common/include/D2Monsters.h', 'D2C_MonModes'),
        ('source/D2Common/include/DataTbls/MonsterIds.h', 'D2C_MonsterIds'),
        ('source/D2Common/include/DataTbls/MonsterTbls.h', 'D2C_MonStatsTxtFlagIndices'),
        ('source/D2Common/include/DataTbls/MissilesTbls.h', 'D2C_MissilesTxtFlagIndices'),
        ('source/D2Game/include/GAME/Event.h', 'D2C_EventTypes : uint8_t'),
        ('source/D2Game/include/UNIT/SUnitEvent.h', 'D2C_UnitEventTypes: uint8_t'),
        ('source/D2Game/include/UNIT/SUnitDmg.h', 'D2DamageResultFlags'),
        ('source/D2Game/include/UNIT/SUnitDmg.h', 'D2DamageHitFlags'),
        ('source/D2Game/include/UNIT/SUnitDmg.h', 'D2DamageReductionType : uint32_t'),
        ('source/D2Common/include/D2DataTbls.h', 'D2C_HitClassIds'),
    ]
    for path, name in specs:
        text = read(path)
        # This declaration alone has an end-of-line comment before its brace.
        text = re.sub(r'//[^\n]*|^\s*#.*$', '', text, flags=re.M)
        if name == 'D2C_MonsterIds':
            # This header contains simple aliases to earlier monster IDs.
            raw = re.search(r'enum ' + name + r'\s*\{(.*?)\};', text, re.S)[1]
            local, value = {}, -1
            for part in raw.split(','):
                if not part.strip():
                    continue
                key, sep, operand = part.strip().partition('=')
                value = (local[operand.strip()] if operand.strip() in local else int(operand.strip(), 0)) if sep else value + 1
                local[key.strip()] = value
            values.update(local)
        else:
            values.update(enum(text, name))
    # Difficulty ordinals have their own header; bind them independently too.
    values.update(enum(read('source/D2CommonDefinitions/include/D2Constants.h'), 'D2C_Difficulties'))
    cpp = parent.read_text().split('\nvoid __fastcall MISSMODE_SetDamageFlags', 1)[0]
    assert cpp.endswith('tkAfterReturn=*p;}\n')
    assert cpp.count('MONSTER_S=27,') == 1 and len(re.findall(r'\bMONSTER_S\b', cpp)) == 1
    cpp = cpp.replace('MONSTER_S=27,', '')  # Unused synthetic parent declaration.
    bound = {}

    def bind(match):
        name = match[1]
        if name in ('TRUE', 'FALSE'):
            assert int(match[2], 0) == int(name == 'TRUE')
            return match[0]
        assert name in values, name
        bound[name] = values[name]
        return name + '=' + str(values[name])

    # Only integer declarations are rewritten, never the complete bodies.
    cpp = re.sub(r'\b([A-Z][A-Z0-9_]+)=(-?(?:0x[\da-fA-F]+|\d+))\b', bind, cpp)
    inherited_hit_classes = enum(cpp, 'D2C_HitClassIds')
    assert all(values[name] == value for name, value in inherited_hit_classes.items())
    bound.update(inherited_hit_classes)
    for name in ('UNITFLAG_CANBEATTACKED', 'STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE'):
        bound[name] = values[name]
        cpp += '\nconstexpr uint32_t ' + name + '=' + str(values[name]) + 'u;\n'
    cpp += '\nuint32_t ITEMS_RollRandomNumber(D2SeedStrc*p){p->lSeed=0x6AC690C5ull*p->nLowSeed+p->nHighSeed;return p->nLowSeed;}\n'
    cpp += '\n' + bodies[callback] + '\n'
    # Observe wrappers without replacing either complete routine.
    cpp = cpp.replace('D2DamageStrc tkRaw{},tkAfterEvents{};',
                      'int nestedEvents=0,nestedReturns=0;D2DamageStrc tkRaw{},tkAfterEvents{};')
    cpp = cpp.replace('{tkRaw=*p;TkEvents::', '{nestedEvents++;tkRaw=*p;TkEvents::')
    cpp = cpp.replace('{TkReturn::SUNITDMG_ExecuteMissileDamage',
                      '{nestedReturns++;TkReturn::SUNITDMG_ExecuteMissileDamage')
    cpp += r'''
void reset(){actor={};target={};game={};row={};mon2={};difficulty={};actor.pGame=target.pGame=&game;actor.dwUnitType=target.dwUnitType=UNIT_MONSTER;actor.dwUnitId=77;target.dwUnitId=99;actor.group=1;target.group=2;target.dwFlags=UNITFLAG_CANBEATTACKED;target.dwAnimMode=1;
actor.pSeed.lSeed=0x29A00000025ull;target.pSeed.lSeed=0x29A00000037ull;actor.stats[STAT_HITPOINTS]=25600;target.stats[STAT_HITPOINTS]=128000;
maxLife=128000;hasRoom=hasRow=hasModes=true;inTown=uninterruptible=frozen=stunned=delay=false;baseId=0;blocked=0;row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];
trace.clear();tkEvents.clear();requested=-1;kills=modes=recoveryCalls=callbackCount=nestedEvents=nestedReturns=0;lastAttacker=nullptr;tkRaw={};tkAfterEvents={};tkAfterReturn={};}
void result(bool&first,int result){int notices=0,refreshes=0,ai=0;for(auto&e:trace){notices+=e[0]==14;refreshes+=e[0]==12;ai+=e[0]==13;}
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%llu,%llu,[",first?"":",",result,nestedEvents,nestedReturns,tkRaw.wResultFlags,tkRaw.dwLtngDamage,tkRaw.dwHitClass,tkRaw.dwHitFlags,tkAfterEvents.dwDmgTotal,tkAfterEvents.dwAbsLife,target.stats[STAT_HITPOINTS],tkAfterReturn.wResultFlags,target.dwLastHitClass,kills,notices,refreshes,ai,modes,recoveryCalls,int(lastAttacker==&actor),(unsigned long long)actor.pSeed.lSeed,(unsigned long long)target.pSeed.lSeed);
for(size_t n=0;n<tkEvents.size();n++)printf("%s%d",n?",":"",tkEvents[n]);printf("],%d,%u]",delay,target.dwFlags);first=false;}
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;printf("{\"damage\":[");bool first=true;
for(int amount:{1,5,99,5000})for(int res:{-150,-100,0,50,100,150})for(int mdr:{0,1,10})for(int pierce:{0,25,200})for(int hp:{1,256,257,128000}){
reset();actor.stats[STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE]=amount;actor.stats[STAT_PASSIVE_LTNG_PIERCE]=pierce;target.stats[STAT_LIGHTRESIST]=res;target.stats[STAT_MAGIC_DAMAGE_REDUCTION]=mdr;target.stats[STAT_HITPOINTS]=hp;
printf("%s{\"input\":[%d,%d,%d,%d,%d],\"output\":",first?"":",",amount,res,mdr,pierce,hp);bool one=true;result(one,SKILLITEM_EventFunc10_AttackerTakesLightDamage(&game,UNITEVENT_DAMAGEDINMELEE,&actor,&target,nullptr,STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE<<16,0));printf("}");first=false;
}printf("],\"absorb\":[");first=true;
for(int amount:{1,5,99})for(int pct:{0,25,40,75})for(int flat:{0,1,5,100})for(int hp:{256,257,128000}){
reset();actor.stats[STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE]=amount;target.stats[STAT_ITEM_ABSORBLIGHT_PERCENT]=pct;target.stats[STAT_ITEM_ABSORBLIGHT]=flat;target.stats[STAT_HITPOINTS]=hp;
printf("%s{\"input\":[%d,%d,%d,%d],\"output\":",first?"":",",amount,pct,flat,hp);bool one=true;result(one,SKILLITEM_EventFunc10_AttackerTakesLightDamage(&game,1,&actor,&target,nullptr,STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE<<16,0));printf("}");first=false;
}printf("],\"guards\":[");first=true;
for(int gate=0;gate<13;gate++)for(int event:{-1,1,2,3,5,6,99})for(int outerFlags:{0,1,4,32,65535}){
reset();actor.stats[STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE]=5;target.stats[STAT_HITPOINTS]=128000;D2DamageStrc outer{};outer.wResultFlags=outerFlags;outer.dwLtngDamage=123;outer.dwPhysDamage=456;
if(gate==3)target.dwFlags=0;if(gate==4)actor.stats[STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE]=0;if(gate==5)actor.stats[STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE]=-1;
if(gate==6)hasRoom=false;if(gate==7)inTown=true;if(gate==8)target.group=actor.group;if(gate==9)row.dwMonStatsFlags=0;if(gate==10)target.dwAnimMode=MONMODE_DEAD;if(gate==11)uninterruptible=true;if(gate==12){uninterruptible=true;target.stats[STAT_HITPOINTS]=256;}
printf("%s{\"input\":[%d,%d,%d],\"output\":",first?"":",",gate,event,outerFlags);bool one=true;result(one,SKILLITEM_EventFunc10_AttackerTakesLightDamage(&game,event,gate==1?nullptr:&actor,gate==2?nullptr:&target,&outer,STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE<<16,0));printf(",\"outer\":[%d,%d,%d]}",outer.wResultFlags,outer.dwLtngDamage,outer.dwPhysDamage);first=false;
}printf("]}\n");}
'''
    for name, body in bodies.items():
        assert function(cpp, name) == body, name
    path = STAGE / 'source.cpp'
    path.write_text(cpp)
    binary = STAGE / 'probe'
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g1', '-fsanitize=address,undefined',
                        '-fno-sanitize-recover=all', '-fno-pie', '-no-pie', str(path), '-o', str(binary)],
                       stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(binary)], capture_output=True, timeout=60)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
    cases = json.loads(run.stdout)
    output = STAGE / 'cases.json'
    output.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    validate(cases)
    for p in (parent, receipt, Path(__file__).resolve()):
        inputs[str(p.relative_to(ROOT))] = sha(p)
    table = ROOT / 'tools/reference/d2r-named-equipment/json/itemstatcost.json'
    manifest = table.parent.parent / 'sources.json'
    assert sha(table) == json.loads(manifest.read_text())['files']['json/itemstatcost.json']['sha256']
    row = json.loads(table.read_text())['item_attackertakeslightdamage']
    assert [row['*ID'], row['itemevent1'], row['itemeventfunc1']] == [128, 'damagedinmelee', 10]
    assert not row.get('itemevent2')
    runtime = ROOT / 'data/Sanctuary/Balance/npc-item-combat-events.json'
    native = next(r for r in json.loads(runtime.read_text()) if r['Stat'] == 128)
    assert native == dict(Stat=128, Name='item_attackertakeslightdamage',
                          Events=[dict(Event='damagedinmelee', Function=10)])
    for p in (table, manifest, runtime):
        inputs[str(p.relative_to(ROOT))] = sha(p)
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], currentExecutableParity=False,
                    contexts={k: len(v) for k, v in cases.items()}, headerBindings=bound, inputs=inputs,
                    completeRoutineHashes={k: hashlib.sha256(v.encode()).hexdigest() for k, v in bodies.items()},
                    artifacts={str(p.relative_to(ROOT)): sha(p) for p in
                               (path, binary, output, STAGE / 'compiler.log', STAGE / 'sanitizer.log')},
                    scope='Seven unchanged older routines: callback10, total/resistance, events, missile return, hit-class and recovery selection. All inherited uppercase integer declarations independently rebound to pinned headers. Ordinary NPC targets; explicit empty subscribers, town/room/allegiance/killable/stat/identity services. Kill, native notification and regeneration services remain declared; no live native or modern executable parity claim.')
    target = ROOT / 'tools/reference/npc-lightning-return-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded, 'NPC lightning-return source proof stale'
    else:
        target.write_text(encoded)
    print('NPC lightning return: complete callback/damage/return composition; independently bound headers; ASan/UBSan pass.')


def validate(cases):
    assert {k: len(v) for k, v in cases.items()} == dict(damage=864, absorb=144, guards=455)
    seeds = [0x29A00000025, 0x29A00000037]

    def ordinary(out, amount, total, absorbed, hp):
        after = min(128000, hp + absorbed) - total if total > 0 else min(128000, hp + absorbed)
        if total > 0 and after < 256:
            after = 0
        killed = int(after == 0)
        assert out == [1, 1, 1, 0x4021, amount * 256, 0x4D, 0, total, absorbed, after,
                       0x4021 | (2 if killed else 0), 0x4D, killed, 1-killed, 1-killed,
                       1-killed, 0, 0, 1, *seeds, [11], 0, 4 | (0 if killed else 32768)], (amount, total, absorbed, hp, out)

    for case in cases['damage']:
        amount, resist, mdr, pierce, hp = case['input']
        effective = max(-100, resist - pierce if resist < 100 else resist)
        total = (amount - mdr) * 256
        if total > 0:
            total = total * (100 - min(100, effective)) // 100
        ordinary(case['output'], amount, total, 0, hp)
    for case in cases['absorb']:
        amount, pct, flat, hp = case['input']
        absorbed = amount * 256 * min(pct, 40) // 100
        absorbed += min(flat * 256, amount * 256 - absorbed)
        ordinary(case['output'], amount, amount * 256 - absorbed, absorbed, hp)
    baseline = next(c['output'] for c in cases['guards'] if c['input'] == [0, 1, 0])
    ordinary(baseline, 5, 1280, 0, 128000)
    for case in cases['guards']:
        gate, event, outer = case['input']
        assert case['outer'] == [outer, 123, 456]
        out = case['output']
        if gate == 0:
            assert out == baseline
        elif gate in (1, 2, 3, 4, 5):
            assert out == [0, 0, 0, 0, 0, 0, 0, 0, 0, 128000, 0, 0, 0, 0, 0, 0, 0, 0, 0, *seeds, [], 0, 0 if gate == 3 else 4]
        else:
            expected = baseline.copy()
            if gate in (6, 7, 8, 9, 10):
                expected[7:11] = [0, 0, 128000, 0x4020 if gate in (6, 8) else 0x4021]
                expected[18] = 0
                expected[21] = []
            if gate in (6, 7, 8):
                expected[11] = 0
            if gate in (7, 10, 11, 12):
                expected[13:16] = [0, 0, 0]
                expected[23] = 4
            if gate == 12:
                expected[9] = 0
                expected[10] = 0x4023
                expected[22] = 1
            assert out == expected, (case['input'], out, expected)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
