#!/usr/bin/env python3
"""Compile pinned Leap geometry/phase routines, keeping modern timing separate."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs = {v['path']: v['sha'] for v in tree['tree'] if v['type'] == 'blob'}
hashes, blocks = {}, {}


def source(path):
    raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == blobs[path], path
    hashes[path] = hashlib.sha256(raw).hexdigest()
    return raw.decode()


def routine(text, name):
    match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
    assert match, name
    end = text.index('{', match.start()) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    blocks[name] = text[match.start():end]
    return blocks[name] + '\n'


bar = source('source/D2Game/src/SKILLS/SkillBar.cpp')
units = source('source/D2Common/src/Units/Units.cpp')
seq = source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
skills = source('source/D2Game/src/SKILLS/Skills.cpp')
damage = source('source/D2Game/src/UNIT/SUnitDmg.cpp')
collision = source('source/D2Common/include/D2Collision.h')
assert 'COLLIDE_MASK_PLAYER_FLYING = COLLIDE_DOOR | COLLIDE_MISSILE_BARRIER' in collision
sequence = re.search(r'D2AnimSeqTxt gPlayerSequenceLeap\[15\] =\s*\{(.*?)\n\};', seq, re.S).group(0)
events = [i for i, line in enumerate(sequence.splitlines()[2:-1]) if 'ANIMSEQ_EVENT_MELEE_ATTACK' in line]
assert events == [5, 11, 14], events

cpp = r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <algorithm>
#include <cstdlib>
#define __stdcall
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,EVENTTYPE_ENDANIM=2,
 COLLIDE_NONE=0,COLLIDE_MASK_PLAYER_FLYING=0x804,PATHTYPE_LEAP=9;
template<class T>T D2Clamp(T v,T lo,T hi){return std::clamp(v,lo,hi);}
struct D2GameStrc{} game;
struct D2CoordStrc{int nX=0,nY=0;};
struct D2ActiveRoomStrc{} room;
struct Path{int move=7,foot=8,x=0,y=0,type=0,velocity=-1,updates=0;};
struct D2UnitStrc{int dwUnitType=0,dwClassId=0,x=100,y=100;Path* pDynamicPath=nullptr;} actor;
struct D2SkillStrc{int flags=0x80,x=101,y=103;} skill;
struct D2SkillsTxt{int dwAuraRangeCalc=0;int dwCalc[1]={1};} row;
struct D2DamageStrc{int wResultFlags=0;};
struct CharStats{int nRunSpeed=9;} chars;
struct D2MonStatsTxt{int nRun=9;} monster;
bool record=true,used=true,monsterRecord=true,arrived=false;
int range=8,pattern=0,attempt=-1,calls=0,deleted=0,leaps=0,areas=0,areaFlags=0;
int tx[3],ty[3];
constexpr int masks[8][3]={{15,15,15},{0,15,15},{0,0,15},{0,0,0},{14,15,15},{13,15,15},{11,15,15},{7,15,15}};
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
void UNITS_GetCoords(D2UnitStrc* p,D2CoordStrc* c){c->nX=p->x;c->nY=p->y;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return range;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc* p,int,int){return p;}
int UNITS_GetUnitSizeX(D2UnitStrc*){return 2;}int PATH_GetUnitCollisionPattern(D2UnitStrc*){return 2;}
D2ActiveRoomStrc* COLLISION_GetFreeCoordinates(D2ActiveRoomStrc* p,D2CoordStrc* c,int,int mask,int allow){
 if(mask!=0x1C09||allow!=1)std::abort();attempt=calls++;tx[attempt]=c->nX;ty[attempt]=c->nY;
 if(!(masks[pattern][attempt]&8)){c->nX+=40;c->nY+=40;}return masks[pattern][attempt]&1?p:nullptr;
}
bool COLLISION_CheckAnyCollisionWithPattern(D2ActiveRoomStrc*,int,int,int,int mask){if(mask!=0x1C09)std::abort();return !(masks[pattern][attempt]&2);}
bool D2Common_11025(int,int,int,int,D2ActiveRoomStrc*,int mask){if(mask!=COLLIDE_MASK_PLAYER_FLYING)std::abort();return masks[pattern][attempt]&4;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return used?&skill:nullptr;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){deleted++;}
int SKILLS_GetFlags(D2SkillStrc* p){return p->flags;}void SKILLS_SetFlags(D2SkillStrc* p,int flags){p->flags=flags;}
int SKILLS_GetParam1(D2SkillStrc* p){return p->x;}int SKILLS_GetParam2(D2SkillStrc* p){return p->y;}
int SKILLS_Leap(D2GameStrc*,D2UnitStrc*,D2SkillStrc*){leaps++;return arrived;}
int sub_6FD10200(D2GameStrc*,D2UnitStrc*,int,int,int,D2DamageStrc* d,int){areas++;areaFlags=d->wResultFlags;return 1;}
void PATH_SetMoveTestCollisionMask(Path* p,int v){p->move=v;}void PATH_SetFootprintCollisionMask(Path* p,int v){p->foot=v;}
void D2COMMON_10170_PathSetTargetPos(Path* p,int x,int y){p->x=x;p->y=y;}
void PATH_SetType(Path* p,int v){p->type=v;}
CharStats* PLRSAVE2_GetCharStatsTxtRecord(int){return &chars;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return monsterRecord?&monster:nullptr;}
void PATH_SetVelocity(Path* p,int velocity,const char*,int){p->velocity=velocity;}
void D2Common_10142(Path* p,D2UnitStrc*,int){p->updates++;}
'''
cpp += routine(units, 'UNITS_GetDistanceToCoordinates')
cpp += routine(bar, 'SKILLS_FindLeapTargetPosition')
cpp += routine(bar, 'SKILLS_SetVelocityForLeap')
cpp += routine(bar, 'SKILLS_SrvDo077_Leap')
cpp += r'''int main(){bool first=true;std::printf("{\"targetCases\":[");
for(int present:{0,1})for(int dx:{-50,-20,-5,-1,0,1,5,20,50})for(int dy:{-50,-20,-5,-1,0,1,5,20,50})
for(int radius:{0,1,8,12,30})for(int scenario=0;scenario<8;scenario++){
 record=present;range=radius;pattern=scenario;attempt=-1;calls=0;int x=actor.x+dx,y=actor.y+dy;
 int result=SKILLS_FindLeapTargetPosition(&actor,132,1,&x,&y);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d",present,dx,dy,radius,scenario,result,x,y,calls);
 for(int i=0;i<calls;i++)std::printf(",%d,%d",tx[i],ty[i]);std::printf("]");
}
std::printf("],\"phaseCases\":[");first=true;range=17;
for(int present:{0,1})for(int hasSkill:{0,1})for(int type:{UNIT_PLAYER,UNIT_MONSTER})
for(int flags:{0,1,0x80,0x100,0x180,0x200,0x1101})for(int done:{0,1})for(int pathPresent:{0,1}){
 Path path;actor.pDynamicPath=pathPresent?&path:nullptr;actor.dwUnitType=type;record=present;used=hasSkill;monsterRecord=true;
 skill.flags=flags;skill.x=101;skill.y=103;arrived=done;deleted=leaps=areas=areaFlags=0;
 int result=SKILLS_SrvDo077_Leap(&game,&actor,132,1);
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",present,hasSkill,type,flags,done,pathPresent,result,deleted,leaps,areas,areaFlags,skill.flags);
}
std::printf("],\"velocityCases\":[");first=true;
for(int type:{UNIT_PLAYER,UNIT_MONSTER})for(int pathPresent:{0,1})for(int x:{0,101})for(int y:{0,103})
for(int run:{0,1,9,15})for(int monPresent:{0,1}){
 Path path;actor.pDynamicPath=pathPresent?&path:nullptr;actor.dwUnitType=type;skill.x=x;skill.y=y;skill.flags=0x80;
 chars.nRunSpeed=monster.nRun=run;monsterRecord=monPresent;
 int result=SKILLS_SetVelocityForLeap(&game,&actor,&skill);
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",type,pathPresent,x,y,run,monPresent,result,skill.flags,path.move,path.foot,path.x,path.y,path.type,path.velocity,path.updates);
}std::printf("]}\n");}
'''
path = STAGE / 'leap-source-probe.cpp'
binary = STAGE / 'leap-source-probe'
path.write_text(cpp)
subprocess.run(['g++', '-std=c++20', '-O2', str(path), '-o', str(binary)], check=True)
data = json.loads(subprocess.check_output([str(binary)]))


def distance(x, y):
    return max(abs(x), abs(y)) + min(abs(x), abs(y)) // 2


def divide(x, y):
    return abs(x) // y * (-1 if x < 0 else 1)


masks = [(15, 15, 15), (0, 15, 15), (0, 0, 15), (0, 0, 0),
         (14, 15, 15), (13, 15, 15), (11, 15, 15), (7, 15, 15)]
for row in data['targetCases']:
    present, dx, dy, radius, scenario, result, x, y, calls, *trace = row
    px, py = 100 + dx, 100 + dy
    if not present:
        assert (result, x, y, calls, trace) == (0, px, py, 0, [])
        continue
    dist = max(1, distance(dx, dy))
    if dist > radius:
        px, py = 100 + divide(radius * dx, dist), 100 + divide(radius * dy, dist)
    side_x, side_y = max(-2, min(2, py - 100)), max(-2, min(2, 100 - px))
    candidates = [(px, py), (px + side_x, py + side_y), (px - side_x, py - side_y)]
    expected, wanted_trace = (0, px, py), []
    for (cx, cy), mask in zip(candidates, masks[scenario]):
        wanted_trace += [cx, cy]
        fx, fy = (cx, cy) if mask & 8 else (cx + 40, cy + 40)
        if mask & 7 == 7 and distance(fx - 100, fy - 100) <= radius:
            expected = (1, fx, fy)
            break
    assert (result, x, y, calls, trace) == (*expected, len(wanted_trace) // 2, wanted_trace), row
for present, has_skill, unit_type, flags, done, path_present, result, deleted, leaps, areas, area_flags, final in data['phaseCases']:
    valid = bool(present and has_skill)
    moving = bool(flags & 0x100)
    launch = not moving and bool(flags & 0x80)
    area = bool(valid and moving and done and unit_type != 1)
    expected_flags = flags
    if valid:
        if launch and path_present:
            expected_flags = 0x1101
        elif not moving and not launch and unit_type != 1:
            expected_flags = 0
    assert (result, deleted, leaps, areas, area_flags, final) == (
        int(valid and (not launch or path_present)), int(valid), int(valid and moving), int(area), 9 if area else 0, expected_flags)
for unit_type, path_present, x, y, run, mon_present, result, flags, move, foot, px, py, path_type, velocity, updates in data['velocityCases']:
    target = bool(path_present and x and y)
    moving = target and (unit_type != 1 or mon_present)
    assert (result, flags, move, foot, px, py, path_type, velocity, updates) == (
        int(moving), 0x1101 if path_present else 0x80, 0 if path_present else 7, 0 if path_present else 8,
        x if target else 0, y if target else 0, 9 if target else 0, run * 256 if moving else -1, int(moving))

data_path = STAGE / 'leap-source-probe.json'
data_path.write_text(json.dumps(data, indent=2) + '\n')
for text, name in ((bar, 'SKILLS_SrvSt40_Leap'), (bar, 'SKILLS_Leap'), (skills, 'sub_6FD12950'),
                   (skills, 'sub_6FD10140'), (damage, 'SUNITDMG_ApplyBlockOrDodge')):
    routine(text, name)
manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
modern_hashes = {}


def modern(name):
    rel = 'json/' + name + '.json'
    raw = (ROOT / 'tools/reference/d2r-named-equipment' / rel).read_bytes()
    actual = hashlib.sha256(raw).hexdigest()
    assert actual == manifest['files'][rel]['sha256']
    modern_hashes[rel] = actual
    return json.loads(raw)


modern_skills = modern('skills')
barbarian = next(row for row in modern('charstats').values() if row['class'] == 'Barbarian')
leap = next(row for row in modern_skills.values() if row['skill'] == 'Leap')
leap_attack = next(row for row in modern_skills.values() if row['skill'] == 'Leap Attack')
missiles = modern('missiles')
missile = next(row for row in missiles.values() if row['Missile'] == 'leapknockback')
out = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes,
           modernTableHashes=modern_hashes,
           probe=dict(caseCounts={k: len(v) for k, v in data.items()}, totalCases=sum(map(len, data.values())),
                      dataPath=str(data_path.relative_to(ROOT)),
                      compiledRoutines=['UNITS_GetDistanceToCoordinates', 'SKILLS_FindLeapTargetPosition', 'SKILLS_SetVelocityForLeap', 'SKILLS_SrvDo077_Leap'],
                      blockHashes={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()},
                      sequenceEvents=events, sequenceHash=hashlib.sha256(sequence.encode()).hexdigest()),
           established=[
               'Destination range uses max(abs(dx),abs(dy))+min(abs(dx),abs(dy))/2 with integer truncation. Clamp requested point to aura range, then try original and two perpendicular offsets capped2 per axis. Free-position mutation is checked again for clearance, flying path and range.',
               'do77 prioritizes moving0x100 over launch0x80. Launch configures path and flags; only successful player landing emits zero-damage result9 knockback area. Later nonmonster phase clears flags. Missing record/skill guards do not delete animation events.',
               'Legacy launch disables movement/footprint collision, sets PATHTYPE_LEAP and uses base character RunSpeed<<8. Zero coordinate fails after flags/masks are changed; it is not a successful native start contract.',
               'Reviewed surrounding start dispatcher pays after successful non-usemanaondo start. Reviewed landing restores normal masks/path, clears reserved destination/uninterruptible/skill_move and schedules end animation+1; airborne continuation rewinds sequence10.',
               'Reviewed result9 area helper uses default aura filter0x8583 and dodge with shield block disabled. Do not reuse War Cry physical block behavior for a zero-damage landing.'
           ],
           limits=[
               'Four unchanged legacy routines compiled with declared collision/room/formula/skill/arrival services. Source start/landing/payment/block routines are reviewed and hashed, not compiled by this probe. No full engine or modern executable certificate.',
               'Pinned current calc2=175 and Param5=8 minimum airborne frames are not consumed by the compiled older launch. Released2.4 notes establish75% speed increase and minimum height/duration; exact modern phase scheduling, server/client arc and hit/death-delay behavior remain acceptance work.',
               'Native UO walking/LOS, free-cell search and destination reservation are not equivalent to source flying-door/missile-barrier masks by assumption. An airborne controller needs safe saved/offline/load lifecycle across unwalkable ground.',
               'This source proof alone does not certify a handler. Native Leap lifecycle/terrain/payment/knockback integration is recorded separately in leap-native-evidence.json; Leap Attack remains pending.'
           ])
(ROOT / 'tools/reference/leap-engine-evidence.json').write_text(json.dumps(out, indent=2) + '\n')
preflight = dict(referenceVersion='3.3.93847', status='Leap native controller implemented with bounded source/differential/lifecycle checks; modern timing/mask/client parity remains open. Leap Attack pending.',
                 skill={k: v for k, v in leap.items() if v not in ('', None)},
                 relatedSkill={k: v for k, v in leap_attack.items() if v not in ('', None)},
                 knockbackMissile={k: v for k, v in missile.items() if v not in ('', None)},
                 evidence='tools/reference/leap-engine-evidence.json',
                 nativeGeometryEvidence='tools/reference/leap-geometry-native-evidence.json',
                 nativeEvidence='tools/reference/leap-native-evidence.json',
                 collisionEvidence='tools/reference/leap-collision-engine-evidence.json',
                 characterMotion={k: barbarian[k] for k in ('WalkVelocity', 'RunVelocity', 'RunDrain')},
                 releasedPrimary=dict(url='https://news.blizzard.com/en-gb/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',
                                      scope='Released2.4 confirms75% faster Leap/Leap Attack, minimum jump height/duration, and distinct Leap Attack area damage/mana10/adjacent-leap changes. Historical corroboration, not current executable capture.'),
                 reuse=['Share weighted source distance already used by SourceTerror through SkillGeometry, retaining independent target-size adjustment.',
                        'Existing SourceKnockbacks handles mapped landing displacement/recovery. Leap has zero weapon damage and no shield-block check.',
                        'Use existing fractional mana, binding/input lock, health/recovery and arrival-field/network consumers; preserve one start payment and separate jump phases.',
                        'Leap has no melee weapon requirement or AttackNoMana flag. Do not inherit Charge weapon eligibility or an ordinary-attack fallback; Leap Attack has separate melee requirements.',
                        'ChargeMotion is a walking adapter and cannot supply flying collision. Share verified coordinate/network pieces only; do not reuse its obstacle/contact stop behavior.'],
                 next=['Retain differential native destination geometry against compiled cases while integrating the terrain/flying adapter.',
                       'Saved airborne/reservation, cancellation/offline/load/death handling is implemented separately. Continue full terrain and owner/client acceptance, without treating the native LOS adapter as source mask identity.',
                       'Integrate source phase, current175-percent speed/minimum8 airborne frames and landing knockback with explicit modern timing/mask acceptance limits.',
                       'Keep Leap Attack weapon strike and physical area damage separate for its later handler.'],
                 assets=dict(effect='Existing skill-effects entry retains leapknockback; final jump/landing effect and arc presentation still needed. Use identified placeholders without delaying mechanics.',
                             icon='Existing separate skill-icons entry needs final Leap talent-tree and hotkey icon. Leap Attack has its own entries.'))
(ROOT / 'tools/reference/barbarian-leap-preflight.json').write_text(json.dumps(preflight, indent=2) + '\n')
print('Leap source cases:', out['probe']['totalCases'])
