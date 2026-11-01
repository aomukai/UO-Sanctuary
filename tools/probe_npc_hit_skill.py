#!/usr/bin/env python3
"""Execute complete older item-proc/target/item-effect routing for Life Tap."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/npc-hit-skill-source'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/SKILLS/SkillItem.cpp'
    source = ROOT / '.local/class-skills/d2moo-pinned-source' / relative
    raw = source.read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
    names = ('SKILLITEM_HandleItemEffectSkill', 'SKILLITEM_CastSkillOnTarget',
             'SKILLITEM_CastSkillOnPosition', 'SKILLITEM_EventFunc20_SkillOnAttackHitKill')
    bodies = {name: function(raw.decode(), name) for name in names}
    cpp = r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int STATE_UNINTERRUPTABLE=98,UNITFLAG_SKSRVDOFUNC=8,SKILLSFLAGINDEX_ITEMTGTDO=0,SKILLSFLAGINDEX2_ITEMCHECKSTART=0;
constexpr int SKILLSITEMTARGET_CASTER=1,SKILLSITEMTARGET_RANDOM=2,SKILLSITEMTARGET_RANDOM_CORPSE=3,SKILLSITEMTARGET_LAST_ATTACKER=4;
constexpr uint32_t gdwBitMasks[]={1,2,4,8};
struct D2GameStrc{}game;struct Path{int x=17,y=19;};
struct D2UnitStrc{D2GameStrc*pGame=&game;Path path;Path*pDynamicPath=&path;D2UnitStrc*target=nullptr;uint32_t dwFlags=0;int dwUnitType=1,dwUnitId=7,pSeed=0;bool locked=false;};
struct D2DamageStrc{uint32_t dwHitFlags=0;};
struct D2SkillsTxt{uint32_t dwFlags[2]={};int wItemEffect=1,nItemTarget=0;}row;
struct Tables{int nStuff=6,nShiftedStuff=63;}tables;auto*sgptDataTables=&tables;
int chance=5,draws=0,handlers=0,starts=0,notifications=0,guard=0,handlerCaster=0,handlerTarget=0,handlerSkill=0,handlerRank=0,handlerFlag=0,notifyTarget=0;uint32_t rawRoll=0;
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){return guard==5||id!=82?nullptr:&row;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,uint32_t,int){return chance;}
uint32_t ITEMS_RollRandomNumber(int*){draws++;return rawRoll;}
bool STATES_CheckState(D2UnitStrc*u,int){return u->locked;}
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*u){return u->target;}
int D2COMMON_10175_PathGetFirstPointX(Path*p){return p->x;}int D2COMMON_10176_PathGetFirstPointY(Path*p){return p->y;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*u,D2UnitStrc*t){u->target=t;}
void D2COMMON_10170_PathSetTargetPos(Path*p,int x,int y){p->x=x;p->y=y;}
bool SKILLITEM_FindTargetPosition(D2GameStrc*,D2UnitStrc*u,int,int){u->path.x=31;u->path.y=37;return guard!=10;}
D2UnitStrc*sub_6FD15210(D2UnitStrc*,D2UnitStrc*t,int,int){return guard==11?nullptr:t;}
D2UnitStrc*SUNIT_GetLastAttacker(D2GameStrc*,D2UnitStrc*){return nullptr;}
int sub_6FD12950(D2GameStrc*,D2UnitStrc*,int,int,int){starts++;return guard!=8;}
int D2GAME_SKILLS_Handler_6FD12BA0(D2GameStrc*,D2UnitStrc*u,int skill,int rank,int a,int b,int flag){
 handlers++;handlerCaster=u->dwUnitId;handlerTarget=u->target?u->target->dwUnitId:0;handlerSkill=skill;handlerRank=rank;handlerFlag=flag;
 u->dwFlags|=UNITFLAG_SKSRVDOFUNC;return guard!=9;}
void sub_6FCC60D0(D2UnitStrc*,int,int,int,int target,int){notifications++;notifyTarget=target;}
void sub_6FCC6150(D2UnitStrc*,int,int,int,int,int){notifications++;}
'''
    cpp += '\n'.join(bodies.values()) + r'''
int main(){printf("[");bool first=true;
for(int c:{0,1,5,100,125})for(uint32_t r:{0u,4u,5u,99u,100u,0xffffffffu})for(int flags:{0,32,64})for(int damage:{0,1})for(int g=0;g<13;g++)for(int old:{0,1})for(int srv:{0,1}){
 chance=c;rawRoll=r;guard=g;draws=handlers=starts=notifications=handlerCaster=handlerTarget=handlerSkill=handlerRank=handlerFlag=notifyTarget=0;
 row={};row.wItemEffect=g==6?0:1;row.nItemTarget=g==10?2:g==11?3:g==12?1:0;row.dwFlags[1]=g==8?1:0;
 D2UnitStrc actor,victim,previous;victim.dwUnitId=29;previous.dwUnitId=99;actor.target=old?&previous:nullptr;actor.dwFlags=64|(srv?8:0);actor.locked=g==4;actor.pGame=g==3?nullptr:&game;
 D2DamageStrc hit{uint32_t(flags)};int packed=(198<<16)|(82<<6)|5;
 int result=SKILLITEM_EventFunc20_SkillOnAttackHitKill(&game,5,g==1?nullptr:&actor,g==2?nullptr:&victim,damage?&hit:nullptr,packed,0);
 bool restored=actor.target==(old?&previous:nullptr)&&actor.path.x==17&&actor.path.y==19&&actor.dwFlags==uint32_t(64|(srv?8:0));
 // In the no-original-unit-target route the source restores only path coordinates.
 // Its temporary target identity remains; preserve that distinction in outputs.
 bool pathRestored=actor.path.x==17&&actor.path.y==19&&actor.dwFlags==uint32_t(64|(srv?8:0));
 printf("%s[[%d,%u,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",c,r,flags,damage,g,old,srv,result,draws,handlers,starts,notifications,handlerCaster,handlerTarget,handlerSkill,handlerRank,handlerFlag,int(restored),int(pathRestored));first=false;
}printf("]\n");}
'''
    STAGE.mkdir(parents=True, exist_ok=True)
    generated = STAGE / 'probe.cpp'
    generated.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(STAGE / 'probe')], check=True, stdout=log, stderr=log)
    with (STAGE / 'sanitizer.log').open('w') as log:
        rows = json.loads(subprocess.check_output([str(STAGE / 'probe')], stderr=log))
    assert not (STAGE / 'sanitizer.log').read_text()
    accepted = 0
    for (chance, raw_roll, flags, damage, guard, old, srv), out in rows:
        eligible = guard != 1 and chance > 0 and (not damage or flags & 32)
        success = bool(eligible and raw_roll % 100 < chance and guard != 5)
        handler = success and guard not in (3, 4, 6, 8, 10, 11)
        notified = handler and guard != 9
        target = 7 if guard == 12 else (99 if old else 0) if guard == 2 else 29
        # Casting returns to the old target if present; otherwise it restores
        # only position/flag, not the temporary target pointer (older engine).
        helper = success and guard not in (3, 4, 6)
        changed_target = helper and not old and guard != 2
        path_restored = not (helper and guard == 10 and old)
        expected = [int(success), int(bool(eligible)), int(handler), int(success and guard == 8), int(notified),
                    7 if handler else 0, target if handler else 0, 82 if handler else 0, 5 if handler else 0,
                    1 if handler else 0, int(path_restored and not changed_target), int(path_restored)]
        assert out == expected, ((chance, raw_roll, flags, damage, guard, old, srv), out, expected)
        accepted += notified
    assert len(rows) == 9360
    cases = STAGE / 'cases.json'
    cases.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    skills = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
    row = {k.lower(): v for k, v in json.loads(skills.read_text())['82'].items()}
    assert row['itemeffect'] == 1 and not any(row.get(k) for k in ('itemtgtdo', 'itemcheckstart', 'itemtarget'))
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], contexts=len(rows), notifiedContexts=accepted,
        functions={n: hashlib.sha256(b.encode()).hexdigest() for n, b in bodies.items()},
        inputs={str(p.relative_to(ROOT)): sha(p) for p in (Path(__file__), source, skills)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in (generated, cases, STAGE / 'compiler.log', STAGE / 'sanitizer.log')},
        scope='Complete unchanged proc20, target/position wrappers and item-effect router; handler/start/target/path/stat/RNG/notification services declared. Does not execute do30, curse factory or healing inside this probe.',
        established=[
            'Missing source,nonpositive chance or a present packet without hitflag0x20reject before RNG. Eligible packets draw once even at100percent. Skill/rank decode uses the retained six-bit rank split;Life Tap targets the real attacker with skill82/rank5/flag1.',
            'Uninterruptible/missing-game/item-effect/start/handler failures happen after chance RNG. Proc return1does not prove an effect or notification. Current Life Tap uses default target routing without a start check or player casting/payment wrapper.',
            'Item helper restores the server-do flag and old unit target when present. With no original unit target it restores coordinates only,leaving the temporary target identity;native integration must keep this distinction explicit.'],
        remaining=[
            'Do30 radial traversal/receiver and defender healing are covered by separate existing evidence;their actual new NPC composition,subscriber ordering and first-swing healing require native acceptance.',
            'Configured table split6and explicit world/path/start/handler services are bounded older-engine evidence,not modern executable equivalence. Item property writer11and unsupported item proc layers are separate work.'])
    output = ROOT / 'tools/reference/npc-hit-skill-source-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'NPC hit skill:{len(rows)}complete proc/target/item-effect contexts,{accepted}notifications;ASan/UBSan passed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
