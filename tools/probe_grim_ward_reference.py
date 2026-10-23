#!/usr/bin/env python3
"""Compile unchanged Grim Ward lifecycle routines against declared services.

This is bounded legacy evidence, not a certificate for the modern executable.
In particular the periodic curse's missile/owner formula context is unresolved.
"""
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
    return blocks[name]


bar = source('source/D2Game/src/SKILLS/SkillBar.cpp')
miss = source('source/D2Game/src/MISSILES/MissMode.cpp')
common = source('source/D2Common/src/Units/Missile.cpp')
skills = source('source/D2Game/src/SKILLS/Skills.cpp')
creation = source('source/D2Game/src/MISSILES/Missiles.cpp')
nec = source('source/D2Game/src/SKILLS/SkillNec.cpp')
formula = source('source/D2Common/src/D2Skills.cpp')
cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
constexpr int UNIT_MONSTER=1,UNIT_MISSILE=3,STATE_CORPSE_NODRAW=1,STATE_CORPSE_NOSELECT=2;
constexpr int MONSTATS2FLAGINDEX_LARGE=0,MONSTATS2FLAGINDEX_SMALL=1;
constexpr unsigned gdwBitMasks[]={1,2};
struct D2ActiveRoomStrc{} room;
struct D2MissileDataStrc{int nCurrentFrame=0,nTotalFrames=1000;} data;
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,skill=150,rank=1,x=123,y=456;D2MissileDataStrc* pMissileData=&data;};
struct D2GameStrc{} game;
struct D2CoordStrc{int nX=0,nY=0;};
struct D2SkillsTxt{int wSrvMissileA=252,wSrvMissileB=249,wSrvMissileC=255,dwCalc[1]={0};} row;
struct D2MonStats2Txt{unsigned dwFlags=0;} modes;
struct D2MissilesTxt{int dwParam[2]={6,30},dwHitPar[1]={0},wHitSubMissile[1]={253};} missileRow;
struct Tables{int nMissilesTxtRecordCount=700;} tables;Tables* sgptDataTables=&tables;
struct D2MissileStrc{unsigned dwFlags=0;int nMissile=0,nSkill=0,nSkillLevel=0,nX=0,nY=0,nRange=0;D2UnitStrc* pOwner=nullptr;} created;
D2UnitStrc caster,corpse,body;D2UnitStrc* selected=&corpse;
bool validRow=true,eligible=true,validRoom=true,freeSpace=true,validModes=true,validMissile=true,validOwner=true,factory=true;
int creates=0,hidden=0,reserved=0,refreshed=0,dispatch=0,handled=0,calc=1000,formulaCaster=0,dispatchCaster=0,order=0;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return validRow?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected;}
bool D2COMMON_11018(D2UnitStrc*){return eligible;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*,int,int){return validRoom?&room:nullptr;}
bool COLLISION_GetFreeCoordinates(D2ActiveRoomStrc*,D2CoordStrc* p,int radius,unsigned mask,int mode){if(radius!=2||mask!=0x1000u||mode!=1)std::abort();p->nX++;return freeSpace;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return validModes?&modes:nullptr;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){created=*p;creates++;order=order*10+1;return factory?&body:nullptr;}
void STATES_ToggleState(D2UnitStrc*,int s,int){if(s==1)hidden++;else reserved++;order=order*10+(s==1?2:3);}
void UNITROOM_RefreshUnit(D2UnitStrc*){refreshed++;order=order*10+4;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return validMissile?&missileRow:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return validOwner?&caster:nullptr;}
int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int,int,int){formulaCaster=p==&caster?1:2;return calc;}
void sub_6FD14400(D2GameStrc*,D2UnitStrc* p,int fn,int,int){dispatch++;dispatchCaster=p==&body?1:2;if(fn!=missileRow.dwParam[1])std::abort();}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){handled++;return 17;}
'''
cpp = cpp.replace('#include <cstdio>', '#include <cstdio>\n#include <cstdlib>')
for text, name in [(common, 'MISSILE_GetRemainingFrames'), (common, 'MISSILE_GetCurrentFrame'),
                   (common, 'MISSILE_SetCurrentFrame'), (bar, 'SKILLS_SrvDo075_GrimWard'),
                   (miss, 'MISSMODE_SrvHit26_GrimWardStart'), (miss, 'MISSMODE_SrvDo14_GrimWard')]:
    cpp += routine(text, name) + '\n'
cpp += r'''void reset(){created={};creates=hidden=reserved=refreshed=dispatch=handled=formulaCaster=dispatchCaster=order=0;}
int main(){body.dwUnitType=UNIT_MISSILE;bool first=true;std::printf("{\"placementCases\":[");
for(int gates=0;gates<128;gates++)for(int size:{0,1,2,3})for(int succeeds:{0,1}){
 reset();validRow=gates&1;selected=gates&2?&corpse:nullptr;eligible=gates&4;validRoom=gates&8;freeSpace=gates&16;validModes=gates&32;row.wSrvMissileA=row.wSrvMissileB=row.wSrvMissileC=gates&64?252:-1;
 modes.dwFlags=size;factory=succeeds;corpse.dwUnitType=1;
 if(gates&64){row.wSrvMissileA=252;row.wSrvMissileB=249;row.wSrvMissileC=255;}
 int result=SKILLS_SrvDo075_GrimWard(&game,&caster,150,20);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",gates,size,succeeds,result,creates,hidden,reserved,refreshed,created.nMissile,created.pOwner==&caster,created.nX,created.nY,order);
}
validRow=eligible=validRoom=freeSpace=validModes=true;selected=&corpse;row.wSrvMissileA=252;row.wSrvMissileB=249;row.wSrvMissileC=255;
std::printf("],\"childCases\":[");first=true;
for(int gates=0;gates<32;gates++)for(int override:{-1,0,1,8,1234})for(int duration:{-1,0,4,5,1000}){
 reset();validMissile=gates&2;missileRow.wHitSubMissile[0]=gates&4?253:-1;validOwner=gates&8;validRow=gates&16;missileRow.dwHitPar[0]=override;calc=duration;body.rank=21;
 int result=MISSMODE_SrvHit26_GrimWardStart(&game,gates&1?&body:nullptr,nullptr);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",gates,override,duration,result,creates,created.dwFlags,created.nRange,created.nMissile,created.nSkillLevel,created.pOwner==&caster,formulaCaster,created.nX);
}
std::printf("],\"periodCases\":[");first=true;validRow=true;
for(int present:{0,1})for(int valid:{0,1})for(int period:{-1,0,1,6,10})for(int frames:{0,1,2,5,6,7,994,995,996,999,1000})for(int skill:{0,150})for(int rank:{0,1,21}){
 reset();validMissile=valid;missileRow.dwParam[0]=period;data.nCurrentFrame=data.nTotalFrames-frames;body.skill=skill;body.rank=rank;
 int result=MISSMODE_SrvDo14_GrimWard(&game,present?&body:nullptr);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",present,valid,period,frames,skill,rank,result,dispatch,handled,dispatchCaster);
}
std::printf("],\"timeline\":[");first=true;validMissile=true;body.skill=150;body.rank=20;missileRow.dwParam[0]=6;data.nTotalFrames=1000;data.nCurrentFrame=1000;
// The common handler is a declared service in this harness. Apply its reviewed
// decrement explicitly, using unchanged current-frame accessors, after do14.
while(MISSILE_GetCurrentFrame(&body)>0){reset();int elapsed=MISSILE_GetRemainingFrames(&body);MISSMODE_SrvDo14_GrimWard(&game,&body);if(dispatch){if(!first)std::printf(",");first=false;std::printf("%d",elapsed);}MISSILE_SetCurrentFrame(&body,MISSILE_GetCurrentFrame(&body)-1);}
std::printf("]}\n");}
'''
probe = STAGE / 'grim-ward-source-probe.cpp'
probe.write_text(cpp)
binary = STAGE / 'grim-ward-source-probe'
subprocess.run(['g++', '-std=c++20', '-O2', str(probe), '-o', str(binary)], check=True)
data = json.loads(subprocess.check_output([str(binary)]))
for gates, size, succeeds, result, count, hidden, reserved, refreshed, missile, owner, x, y, order in data['placementCases']:
    ok = gates == 127
    assert (result, count, hidden, reserved, refreshed) == (int(ok),) * 5
    if ok:
        assert (missile, owner, x, y, order) == (255 if size & 1 else 249 if size & 2 else 252, 1, 124, 456, 1234)
for gates, override, duration, result, count, flags, lifetime, missile, rank, owner, formula_caster, x in data['childCases']:
    ok = gates == 31
    assert result == 1 and count == int(ok)
    if ok:
        assert (flags, lifetime, missile, rank, owner, formula_caster, x) == (32769, override if override > 0 else max(duration, 5), 253, 21, 1, int(override <= 0), 123)
for present, valid, period, frames, skill, rank, result, dispatch, handled, caster in data['periodCases']:
    exists = present and valid
    pulse = frames % max(period, 1) == 0
    cast = exists and pulse and skill > 0 and rank > 0
    collision = exists and (not pulse or skill > 0 and rank > 0)
    assert (result, dispatch, handled, caster) == (17 if collision else 2, int(cast), int(collision), int(cast))
assert data['timeline'] == list(range(0, 1000, 6))

# Trace the unchanged surrounding routines, without pretending the declared
# formula/collision services above executed these entire engine subsystems.
for text, name in [(miss, 'MISSMODE_HandleMissileCollision'), (skills, 'sub_6FD14400'),
                   (nec, 'SKILLS_SrvDo030_Curse'), (nec, 'sub_6FD0B450'),
                   (skills, 'sub_6FD10360'), (skills, 'sub_6FD10EC0'),
                   (formula, 'SKILLS_EvaluateSkillFormula'), (formula, 'SKILLS_GetSpecialParamValue'),
                   (formula, 'SKILLS_GetHighestLevelSkillFromSkillId')]:
    routine(text, name)
assert 'SKILLS_SrvDo030_Curse' in blocks['sub_6FD14400']
assert 'gpSkillSrvDoFnTable_6FD40A20[nDoFunc](pGame, pUnit' in blocks['sub_6FD14400']
assert 'nFrame = MISSILE_GetCurrentFrame(pMissile) - 1' in blocks['MISSMODE_HandleMissileCollision']
assert 'MISSILE_SetTotalFrames(pMissile, nRange);' in creation and 'MISSILE_SetCurrentFrame(pMissile, nRange);' in creation
assert 'pSkillCalc.pUnit = pUnit;' in blocks['SKILLS_EvaluateSkillFormula']
assert 'SKILLS_GetHighestLevelSkillFromSkillId(pUnit, nSkillId)' in blocks['SKILLS_GetSpecialParamValue']

data_path = STAGE / 'grim-ward-source-probe.json'
data_path.write_text(json.dumps(data, indent=2) + '\n')
counts = {k: len(v) for k, v in data.items()}
skill_path = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
missile_path = ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json'
skill_row = next(v for v in json.loads(skill_path.read_text()).values() if v.get('skill') == 'Grim Ward')
missile_rows = {v['Missile']: v for v in json.loads(missile_path.read_text()).values() if v.get('Missile', '').startswith('grimward')}
assert (skill_row['srvstfunc'], skill_row['srvdofunc'], skill_row['calc1'], skill_row['mana']) == (33, 75, 1000, 4)
for size, frames in [('small', 5), ('medium', 8), ('large', 8)]:
    assert missile_rows['grimward' + size + 'start']['Range'] == frames
    assert (missile_rows['grimward' + size]['Param1'], missile_rows['grimward' + size]['Param2']) == (6, 30)
out = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'],
           status='Bounded lifecycle proof; periodic modern missile/owner curse context unresolved. Native handler pending.',
           sourceHashes=hashes,
           pinnedTables={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in (skill_path, missile_path)},
           probe=dict(caseCounts=counts, totalCases=sum(v for k, v in counts.items() if k != 'timeline'), pulseValues=len(data['timeline']),
                      compiledBlocks=['MISSILE_GetRemainingFrames', 'MISSILE_GetCurrentFrame', 'MISSILE_SetCurrentFrame', 'SKILLS_SrvDo075_GrimWard', 'MISSMODE_SrvHit26_GrimWardStart', 'MISSMODE_SrvDo14_GrimWard'],
                      blockHashes={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()},
                      dataPath=str(data_path.relative_to(ROOT))),
           established=[
               'Do75 checks skill, eligible corpse, room/free coordinates (radius2/mask0x1000), monster/modes and missile bounds. Large takes precedence over small. It requests a start missile then toggles nodraw/noselect and refreshes even if the declared factory returns null.',
               'Hit26 requires start/row/child/owner/skill; positive hit parameter overrides lifetime, otherwise owner-evaluated skill calc1 has minimum5. Child flags32769, original owner, rank and relocated coordinates are preserved. Current table calc1=1000.',
               'Do14 checks MISSILE_GetRemainingFrames before entering the collision handler. Despite its name this helper returns total minus current, hence elapsed frames. Every elapsed-frame multiple of max(Param1,1) dispatches Param2 using the body missile itself, then invokes collision. No period match bypasses skill validation. A matching period with nonpositive skill/rank returns2 without collision.',
               'Creation sets total and current frames to the same range. Common collision decreases current by1 after the do14 modulo test. A1000-frame body is therefore eligible at elapsed0,6,...,996:167 pulse values, including its first do call. Exact child insertion/event order remains separately unverified.',
               'The traced dispatch table routes30 to generic curse with pUnit unchanged. That routine evaluates range/duration/stats on pUnit and stores it as curse creator; the legacy formula service keeps that unit. A hard-skill lookup searches that unit skill list, with no owner lookup in the reviewed special-parameter/skill-list helpers.'
           ],
           unresolved=[
               'D2R3.3.93847 may resolve missile owner/hard Find Potion synergy differently from the supplemental older engine. Do not silently evaluate modern physical-resistance synergy against owner, or assume zero, before resolving this context.',
               'Generic curse starts special Terror AI without the explicit dynamic-target assignment used by the separate older scare helper. Establish modern ground-anchor/creator versus threat selection before reusing Howl origin behavior.',
               'Exact source free-coordinate/path/target-center and first scheduled child-frame order need a native terrain/clock adapter. Existing UO corpse access8tiles is provisional h2h input behavior.',
               'Modern DM curve remains the separate published-versus-decompiled disagreement. No shared runtime curve changed.',
               'Retained original corpse loot takes precedence over blindly hiding/deleting the native container; source nodraw and visible ward art need a documented presentation adapter.'
           ],
           limits=[
               'Declared services cover corpse eligibility, room/free-coordinate result, allocation, skill formula, ownership and collision. The probes do not execute a modern game, full missile engine, expression VM, special AI or damage consumer.',
               'Generic curse/collision/formula surrounding blocks are hash-traced only, not compiled by this probe. Shared start33 eligibility is already covered by Find Potion5004-case evidence.',
               'No native handler enabled, class completion claimed, build or save format changed. Three-size start/body/stop effects, affected-state cues and talent/hotkey icons remain separate future assets.'
           ])
(ROOT / 'tools/reference/grim-ward-engine-evidence.json').write_text(json.dumps(out, indent=2) + '\n')
print('Grim Ward bounded lifecycle cases:', out['probe']['totalCases'], 'pulse values:', len(data['timeline']))
