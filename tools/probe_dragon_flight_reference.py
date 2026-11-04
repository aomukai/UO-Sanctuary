#!/usr/bin/env python3
"""Execute pinned Flight start, two-event sequence, coordinate and warp bodies.

The current executable supplies read-only sequence data. The compiled routines
are the explicitly older source; skill dispatch/payment and world services are
declared boundaries, not claims of modern executable equivalence.
"""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

from player_weapon_sequences import BASE, ROOT, read, sha
from review_weapon_event_order import function

STAGE = BASE / 'dragon-flight-source'


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    profiles, inputs, executable = read(21)
    assert len(profiles) == 14
    for p in profiles:
        assert (p['Points'], p['Frames'], p['RawAuxiliary']) == (23, 23, 256)
        assert p['Rows'] == [[0, 10 if i < 10 else 12, i if i < 10 else i-10,
                              0, int(i in (9, 14))] for i in range(23)]
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    bodies = {}
    for rel, names in {
        'source/D2Game/src/SKILLS/SkillSor.cpp': ['SKILLS_SrvSt12_Telekinesis_DragonFlight'],
        'source/D2Game/src/SKILLS/Skills.cpp': ['D2GAME_GetXAndYFromTargetUnit_6FD14020'],
        'source/D2Game/src/UNIT/SUnit.cpp': ['sub_6FCBDFE0', 'sub_6FCBCE70'],
        'source/D2Game/src/SKILLS/SkillAss.cpp': ['SKILLS_SrvDo052_DragonFlight'],
        'source/D2Common/src/DataTbls/SequenceTbls.cpp': ['DATATBLS_GetSequenceEvent'],
        'source/D2Common/src/Units/Units.cpp': ['UNITS_SetAnimationFrame', 'UNITS_GetFrameBonus'],
        'source/D2Game/src/PLAYER/PlrModes.cpp': ['sub_6FC80EE0'],
    }.items():
        source = BASE / 'd2moo-pinned-source' / rel
        raw = source.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest() == next(
            r['sha'] for r in tree['tree'] if r['path'] == rel)
        inputs[str(source.relative_to(ROOT))] = sha(source)
        for name in names:
            bodies[name] = function(raw.decode(), name)
    preflight = json.loads((ROOT / 'tools/reference/dragon-flight-preflight.json').read_text())
    assert preflight['source']['skill']['Param7'] == 38
    for r in preflight['legacyInspection']['routines']:
        assert hashlib.sha256(bodies[r['symbol']].encode()).hexdigest() == r['routineSha256']
    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <string>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_TYPES_COUNT=6,UNITFLAG_SKSRVDOFUNC=64,UNITFLAGEX_TELEPORTED=0x10000;
constexpr int STAT_PROGRESSIVE_TOHIT=333,DAMAGERESULTFLAG_SUCCESSFULHIT=1,COLLIDE_MASK_PLAYER_FLYING=666;
constexpr int NUM_WEAPON_CLASSES=14,NUMBER_OF_PLAYERCLASSES=7,WEAPONCLASS_HTH=0,PLRMODE_ATTACK1=7,PLRMODE_ATTACK2=8,PLRMODE_KICK=12,PLRMODE_SPECIAL3=15,PLRMODE_SPECIAL4=16;
constexpr int EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2,EVENTTYPE_REMOVESKILLCOOLDOWN=3;
struct D2ActiveRoomStrc{int level=1;bool town=false;} ownerRoom,targetRoom;
struct D2DynamicPathStrc{int x=71,y=81;} path;
struct D2ClientStrc{} client;
struct D2PlayerDataStrc{D2ClientStrc* pClient=&client;int unk0xA8[42]={},unk0xA4=0;unsigned unk0xA0=0;} player;
struct D2AnimSeqTxt{int wSequence,nMode,nFrame,nDir,nEvent;};struct Anim{int pFrameFlags[144]={};} anim;
struct D2UnitStrc{int dwUnitType=0,dwClassId=6,dwAnimMode=18,x=100,y=100;unsigned dwFlags=0,dwFlagEx=0;
 int dwSeqFrame=0,nSeqCurrentFramePrecise=0,dwSeqSpeed=256,dwSeqFrameCount=23*256,wAnimSpeed=256,dwFrameCountPrecise=0;
 D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=&anim;D2DynamicPathStrc* pDynamicPath=&path;D2ActiveRoomStrc* room=&ownerRoom;} owner,target;
struct D2GameStrc{int dwGameFrame=100;} game;
struct D2SkillsTxt{int dwAuraRangeCalc=7,dwParam[8]={100,35},nSrcDam=128;} row;
struct D2DamageStrc{int wResultFlags=0,dwEnDmgPct=0;} packet;
struct D2LevelsTxt{int nTeleport=1;} level;
struct D2CoordStrc{int nX=0,nY=0;};struct D2DrlgCoordsStrc{int nTileXPos=20,nTileYPos=30;};struct D2SkillStrc{} skill;
bool hasRow=true,hasTarget=true,hostile=true,hasLevel=true,collision=false,roomFound=true,freeFound=true,pathGood=true,used=true;
int resultFlags=1,progressiveAr=85,ar=0,builders=0,allocs=0,charges=0,drains=0,src=0,warps=0,freeCalls=0,pathCalls=0,moves=0,refresh=0,sync=0,timers=0;
int rankNow=20,callbackResult=0;std::string order;
struct Event{int type,frame,event,param;};std::vector<Event> events;std::vector<int> phases,results;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){order+='R';return hasRow?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){order+='T';return hasTarget?&target:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 38;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int PATH_ComputeSquaredDistance(int x,int y,int a,int b){return (x-a)*(x-a)+(y-b)*(y-b);}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return hostile;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return p->room;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc* p){assert(p);return p->town;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->x;}
int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->y;}
int DUNGEON_GetLevelIdFromRoom(D2ActiveRoomStrc* p){return p->level;}
D2LevelsTxt* DATATBLS_GetLevelsTxtRecord(int id){assert(id==1);order+='L';return hasLevel?&level:nullptr;}
bool UNITS_TestCollisionByCoordinates(D2UnitStrc*,int,int,int mask){assert(mask==COLLIDE_MASK_PLAYER_FLYING);order+='C';return collision;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*,int,int){warps++;order+='r';return roomFound?&targetRoom:nullptr;}
int UNITS_GetUnitSizeX(D2UnitStrc*){return 2;}
D2ActiveRoomStrc* COLLISION_GetFreeCoordinates(D2ActiveRoomStrc* p,D2CoordStrc* c,int size,unsigned mask,int flags){assert(size==2&&mask==0x1C09&&flags==0);order+='f';freeCalls++;c->nX++;c->nY-=2;return freeFound?p:nullptr;}
bool D2Common_10229(D2DynamicPathStrc*,D2UnitStrc* p,D2ActiveRoomStrc* r,int x,int y){order+='p';pathCalls++;if(!pathGood)return false;moves++;p->x=x;p->y=y;p->room=r;return true;}
D2PlayerDataStrc* UNITS_GetPlayerData(D2UnitStrc*){return &player;}
void DUNGEON_GetRoomCoordinates(D2ActiveRoomStrc*,D2DrlgCoordsStrc*){}
void D2GAME_PACKETS_SendPacket0x07_6FC3D120(D2ClientStrc*,int,uint16_t,uint16_t){sync++;order+='s';}
void UNITROOM_RefreshUnit(D2UnitStrc*){refresh++;order+='u';}
void sub_6FCBB190(D2GameStrc*,D2UnitStrc*,int){order+='a';}
int GetTickCount(){return 1234;}
void sub_6FCBDF90(){}
void j_D2GAME_InitTimer_6FC351D0(D2GameStrc* g,D2UnitStrc*,int type,int frame,void(*)(),int,int){assert(type==3&&frame==g->dwGameFrame+50);timers++;order+='t';}
void sub_6FC7E310(D2GameStrc*,D2UnitStrc*,int,int){order+='m';}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){assert(stat==STAT_PROGRESSIVE_TOHIT);return progressiveAr;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int rank){return 60+25*(rank-1);}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int hit,int zero){assert(zero==0);ar=hit;order+='h';return resultFlags;}
void sub_6FCF7CE0(D2GameStrc*,D2DamageStrc*,D2UnitStrc*,D2UnitStrc*,int,int){builders++;order+='b';}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,unsigned char n){allocs++;packet=*p;src=n;order+='A';}
D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc*,D2UnitStrc*){return &packet;}
void sub_6FCF77E0(D2GameStrc*,D2UnitStrc*,D2DamageStrc* p){assert(p==&packet);charges++;order+='q';}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int zero){assert(zero==0);drains++;order+='d';}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){std::abort();}int ITEMS_GetWeaponClassId(D2UnitStrc*){std::abort();}bool UNITS_CanDualWield(D2UnitStrc*){std::abort();}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int event,int param){events.push_back({type,frame,event,param});}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return used?&skill:nullptr;}
int SKILLS_GetFlags(D2SkillStrc*){return 0;}void SKILLS_SetFlags(D2SkillStrc*,int){std::abort();}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){std::abort();}int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){std::abort();}
bool SUNIT_IsDead(D2UnitStrc*){return false;}
'''
    for name in ['SKILLS_SrvSt12_Telekinesis_DragonFlight', 'D2GAME_GetXAndYFromTargetUnit_6FD14020',
                 'sub_6FCBDFE0', 'SKILLS_SrvDo052_DragonFlight', 'DATATBLS_GetSequenceEvent',
                 'UNITS_GetFrameBonus', 'sub_6FCBCE70', 'UNITS_SetAnimationFrame']:
        cpp += '\n'+bodies[name]
    cpp += r'''
void sub_6FD13410(D2GameStrc* g,D2UnitStrc* p){phases.push_back(p->dwSeqFrame>>8);callbackResult=SKILLS_SrvDo052_DragonFlight(g,p,275,rankNow);results.push_back(callbackResult);}
'''
    cpp += bodies['sub_6FC80EE0']
    cpp += '\nD2AnimSeqTxt sequence[23] = {'+','.join(
        '{'+','.join(map(str, row))+'}' for row in profiles[0]['Rows'])+'};\n'
    cpp += r'''
void reset(){owner={};target={};target.dwUnitType=1;target.x=130;target.y=120;target.room=&targetRoom;ownerRoom={};targetRoom={};player={};row={};game={};level={};packet={};path={};
 hasRow=hasTarget=hostile=hasLevel=roomFound=freeFound=pathGood=used=true;collision=false;resultFlags=1;progressiveAr=85;rankNow=20;ar=builders=allocs=charges=drains=src=warps=freeCalls=pathCalls=moves=refresh=sync=timers=0;order.clear();events.clear();phases.clear();results.clear();}
bool comma=false;void list(const std::vector<int>& a){printf("[");for(size_t i=0;i<a.size();i++)printf("%s%d",i?",":"",a[i]);printf("]");}
void emit(const std::vector<int>& input,const std::vector<int>& output){if(comma)printf(",");comma=true;printf("[");list(input);printf(",");list(output);printf(",\"%s\"]",order.c_str());}
std::vector<int> out(int ret){return {ret,(int)owner.dwFlags,owner.x,owner.y,warps,freeCalls,pathCalls,moves,refresh,sync,timers,(int)player.unk0xA0,(int)owner.dwFlagEx,ar,builders,allocs,charges,drains,src,packet.wResultFlags,packet.dwEnDmgPct};}
int main(){printf("{\"start\":[");
for(int rowFound:{0,1})for(int selected:{0,1})for(int type:{0,1,2,3,4,5})for(int h:{0,1})for(int town:{0,1})for(int other:{0,1})for(int dx:{-39,-38,-30,0,30,38,39})for(int dy:{0,22,23,38}){
 reset();hasRow=rowFound;hasTarget=selected;target.dwUnitType=type;hostile=h;ownerRoom.town=town;targetRoom.town=other;target.x=owner.x+dx;target.y=owner.y+dy;
 int ret=SKILLS_SrvSt12_Telekinesis_DragonFlight(&game,&owner,275,20);emit({rowFound,selected,type,h,town,other,dx,dy},{ret});}
printf("],\"release\":[");comma=false;
for(int phase:{0,1,2,3})for(int guard:{0,1,2,3})for(int rank:{0,1,20,255})for(int flags:{0,1,9,16385})for(int source:{0,64,128}){
 reset();owner.dwSeqFrame=(phase<<8)|255;owner.dwFlags=72;hasRow=guard!=1;hasTarget=guard!=2;if(guard==3)owner.room=nullptr;resultFlags=flags;row.nSrcDam=source;
 int ret=SKILLS_SrvDo052_DragonFlight(&game,&owner,275,rank);emit({phase,guard,rank,flags,source},out(ret));}
printf("],\"landing\":[");comma=false;
for(int tele:{0,1,2,3})for(int found:{0,1})for(int collide:{0,1})for(int coordinate:{0,1,2})for(int room:{0,1})for(int free:{0,1})for(int pathOk:{0,1}){
 reset();owner.dwFlags=72;level.nTeleport=tele;hasLevel=found;collision=collide;if(coordinate==1)target.x=0;if(coordinate==2)target.y=0;roomFound=room;freeFound=free;pathGood=pathOk;
 int ret=SKILLS_SrvDo052_DragonFlight(&game,&owner,275,20);emit({tele,found,collide,coordinate,room,free,pathOk},out(ret));}
printf("],\"warp\":[");comma=false;
for(int type:{0,1})for(int supplied:{0,1})for(int room:{0,1})for(int free:{0,1})for(int pathOk:{0,1})for(int skip:{0,1})for(int flag:{0,1})for(int history:{0,19}){
 reset();owner.dwUnitType=type;roomFound=room;freeFound=free;pathGood=pathOk;player.unk0xA0=history;
 int ret=sub_6FCBDFE0(&game,&owner,supplied?&targetRoom:nullptr,130,120,skip,flag);
 auto r=out(ret);r.push_back(player.unk0xA8[40]);r.push_back(player.unk0xA8[41]);r.push_back(player.unk0xA4);emit({type,supplied,room,free,pathOk,skip,flag,history},r);}
printf("],\"coordinates\":[");comma=false;
for(int selected:{0,1})for(int x:{0,1,130})for(int y:{0,1,120}){reset();hasTarget=selected;target.x=path.x=x;target.y=path.y=y;int a=-1,b=-1;int ret=D2GAME_GetXAndYFromTargetUnit_6FD14020(&game,&owner,&a,&b);emit({selected,x,y},{ret,a,b});}
printf("],\"sequence\":[");comma=false;
for(int speed=0;speed<=768;speed++)for(int fail:{0,1,2,3,4}){
 reset();owner.pAnimSeq=sequence;owner.dwSeqSpeed=speed;owner.dwFlags=0;owner.dwSeqFrame=255;
 if(fail==1)level.nTeleport=0;if(fail==2)pathGood=false;if(fail==3)hasTarget=false;if(fail==4)used=false;
 sub_6FCBCE70(&game,&owner);auto clock=events;
 for(auto e:clock)if(e.type==EVENTTYPE_MODECHANGE){game.dwGameFrame=e.frame;sub_6FC80EE0(&game,&owner,e.event,e.param);}
 if(comma)printf(",");comma=true;printf("[[%d,%d],",speed,fail);list(out(0));printf(",[");
 for(size_t i=0;i<clock.size();i++){auto e=clock[i];if(i)printf(",");list({e.type,e.frame-100,e.event,e.param});}printf("],");list(phases);printf(",");list(results);printf(",%d]\n",owner.dwSeqFrame&255);
}printf("]}\n");}
'''
    source = STAGE / 'source.cpp'
    source.write_text(cpp)
    binary = STAGE / 'probe'
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                        '-fno-sanitize-recover=all', '-fno-omit-frame-pointer', '-fno-pie', '-no-pie',
                        str(source), '-o', str(binary)], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()
    data = json.loads(run.stdout)
    counts = {name: len(rows) for name, rows in data.items()}
    assert counts == dict(start=5376, release=768, landing=384, warp=256, coordinates=18, sequence=3845), counts
    for args, result, trace in data['start']:
        found, selected, kind, hostile, town, other, dx, dy = args
        assert result == [int(found and selected and dx*dx+dy*dy <= 38*38
                              and (kind not in (0, 1) or hostile and not town and not other))]
        assert trace == ('TR' if selected else 'T')
    for args, result, trace in data['release']:
        phase, guard, rank, flags, src = args
        valid = guard not in (1, 2)
        attack = valid and phase % 2 == 1
        landed = valid and phase % 2 == 0 and guard != 3
        assert result == [int(attack or landed), 8 if valid and phase % 2 == 0 and guard != 3 else 72,
                          131 if landed else 100, 118 if landed else 100,
                          *([int(landed)]*8), 65536 if landed else 0,
                          85+60+25*(rank-1) if attack else 0, int(attack and bool(flags & 1)),
                          *([int(attack)]*3), (src or 128) if attack else 0,
                          flags if attack else 0, 100+35*(rank-1) if attack and flags & 1 and rank > 0 else 0], (args, result)
        expected = 'R' if guard == 1 else 'RT' if guard == 2 or not attack and not landed else (
            'RTh'+('b' if flags & 1 else '')+'Aqd' if attack else 'RTTLrfpsuatm')
        assert trace == expected, (args, trace, expected)
    for args, result, trace in data['landing']:
        tele, found, collide, coordinate, room, free, path_ok = args
        admitted = coordinate == 0 and found and tele != 0 and not (tele == 2 and collide)
        moved = admitted and room and free and path_ok
        assert result == [int(admitted), 8, 131 if moved else 100, 118 if moved else 100,
                          int(admitted), int(admitted and room), int(admitted and room and free),
                          *([int(moved)]*5), 65536 if moved else 0, *([0]*8)], (args, result)
        expected = 'RTT'+('L' if coordinate == 0 else '')+('C' if coordinate == 0 and found and tele == 2 else '')
        if admitted:
            expected += 'r'+('f' if room else '')+('p' if room and free else '')+('suatm' if moved else '')
        assert trace == expected, (args, trace, expected)
    for args, result, trace in data['warp']:
        kind, supplied, room, free, path_ok, skip, flag, history = args
        entered = supplied or room
        moved = entered and (skip or free) and path_ok
        x, y = (130, 120) if skip else (131, 118)
        assert result == [int(moved), 0, x if moved else 100, y if moved else 100,
                          int(not supplied), int(entered and not skip), int(entered and (skip or free)),
                          int(moved), int(moved), int(moved and kind == 0), int(moved and kind == 0),
                          (history+1) % 20 if moved and kind == 0 else history,
                          (0x800 if flag else 0x10000) if moved else 0, *([0]*8),
                          x if moved and kind == 0 else 0, y if moved and kind == 0 else 0,
                          1234 if moved and kind == 0 else 0], (args, result)
        expected = ('' if supplied else 'r')+('f' if entered and not skip else '')+('p' if entered and (skip or free) else '')
        expected += ('suatm' if kind == 0 else 'ua') if moved else ''
        assert trace == expected, (args, trace, expected)
    for args, result, trace in data['coordinates']:
        selected, x, y = args
        assert result == [int(bool(x and y)), x, y] and trace == 'T'
    for args, result, events, phases, results, fraction in data['sequence']:
        speed, failure = args
        expected = [[1, max(1, (i*256+speed-1)//speed), 1, n] for n, i in enumerate((9, 14))] if speed else []
        expected.append([2, max(2, (23*256+speed-1)//speed) if speed else 1, 0, 0])
        assert events == expected, (args, events, expected)
        dispatched = speed != 0 and failure != 4
        assert phases == ([0, 1] if dispatched else [])
        assert results == ([0 if failure in (1, 3) else 1, 0 if failure == 3 else 1] if dispatched else [])
        assert result[7] == int(dispatched and failure == 0)  # actual warp commit
        assert result[15:18] == [int(dispatched and failure != 3)]*3  # odd phase still runs after failed warp
        assert fraction == 255
    fixture = STAGE / 'cases.json'
    fixture.write_text(json.dumps(data, separators=(',', ':'))+'\n')
    profiles_file = STAGE / 'current-sequence21.json'
    profiles_file.write_text(json.dumps(profiles, indent=2)+'\n')
    runtime = ROOT / 'data/Sanctuary/Balance/dragon-flight-sequence.json'
    encoded_profiles = json.dumps(profiles, indent=2)+'\n'
    if check:
        assert runtime.read_text() == encoded_profiles, 'Current Dragon Flight sequence data stale'
    else:
        runtime.write_text(encoded_profiles)
    evidence = dict(referenceVersion='D2R Reign of the Warlock 3.3.93847', talentId='assassin.dragonflight',
        legacyCommit=tree['sha'], currentExecutableParity=False, handlerEnabled=False, installed=False,
        executableSha256=executable, readOnlyFileInspection=True, processMemoryRead=False,
        classicDescriptorsMatched=322, currentSequence=21, currentWeaponProfiles=14,
        runtimePath=str(runtime.relative_to(ROOT)), runtimeSha256=sha(runtime),
        inputs=inputs, compiledUnchangedRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
        cases=counts, sanitizers='ASan+UBSan clean',
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in (source, binary, STAGE/'compiler.log', STAGE/'sanitizer.log', fixture, profiles_file, Path(__file__).resolve())},
        established=[
            'All14 current-file SQ21 profiles match the classic23point CAST0..9/KICK0..12 sequence, with melee markers at sequence indices9 and14. This is current immutable data, not current code execution.',
            'The complete older scheduler/player callback maps those visual markers to ordinal0 then1 while retaining fractional sequence bits. Supplied advance0..768 yields ordered ceil(2304/advance) teleport and ceil(3584/advance) kick events; zero advance schedules only end.',
            'Complete start12 uses inclusive squared38range with hostility and both town checks for living player/monster targets. Complete do52 does not repeat start-range or town validation.',
            'Even do52 clears the do flag after finding its room, reads current target coordinates and the origin level teleport policy. Policy2 tests flying collision; policy1/3 do not. A zero coordinate refuses before the level lookup.',
            'The complete warp helper searches free coordinates with player size and mask0x1C09, then commits a path move before refresh/sync/history/timer services. Player history wraps at20. Failed lookup/free-space/path movement produces no success notifications.',
            'Do52 ignores the warp helper return and returns1 once its own permission gates pass, even if no move occurred. The declared sequence dispatcher still reaches the odd kick after a refused/failed teleport; future native code must explicitly settle this context rather than infer success from do52 return.',
            'Odd do52 adds progressive AR to the rank AR supplier, builds body/boot damage only on hit, allocates even missed packets with srcdam128 when the row supplies0, then progressive dispatch precedes durability/contact. No ordinary weapon physical or Tail fire burst is substituted.'],
        services=[
            'Formula range38, rank AR60+25*(rank-1), hit flags and progressive AR are explicit suppliers. Complete source packet builders/progressive controllers already have separate receipts; here their calls/ordering are observed, not their internals or modern consume-one semantics.',
            'Hostility, room/level, flying collision, room lookup, free-coordinate search, path movement, packet/refresh/history clocks and callbacks are explicit world/engine services. The free-space service shifts coordinates(+1,-2) to prove the helper uses its returned position. No UO landing equivalence or current collision algorithm is claimed.',
            'Sequence speed is a supplied input; normal Assassin SQ mode conversion and used-skill flags0/living owner are supplied. The skill-dispatch seam calls unchanged do52 directly, not the complete generic dispatch/payment/posting chain. Alternate player callback paths abort.',
            'The existing current-file reader verifies all322 classic descriptors before reading SQ21. No process input, memory attachment, writable game file, installed build/save/config or client change occurs.'],
        remaining=[
            'Current attack-rate selection, mode entry/posting, failed-landing payment/cancellation, landing map rules, natural input/target leeway and save/reload behavior need composed native checks.',
            'Dragon Tail adaptation remains Tail-specific. Dragon Flight charged accuracy/block/dodge and charge/payment rules need identified source/current evidence or an explicit Sanctuary decision.',
            'No learned Flight handler, gameplay damage, public binding or class completion is certified. Final teleport/kick/charge effects/audio and separate tree/hotkey icons remain pending.'])
    path = ROOT / 'tools/reference/dragon-flight-source-evidence.json'
    encoded = json.dumps(evidence, indent=2)+'\n'
    if check:
        assert path.read_text() == encoded, 'Dragon Flight source evidence stale'
    else:
        path.write_text(encoded)
    print(f'Dragon Flight: {counts}; current SQ21/322 classic descriptors; nine complete older routines ASan+UBSan clean; learned route remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
