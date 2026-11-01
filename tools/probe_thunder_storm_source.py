#!/usr/bin/env python3
"""Execute complete older Thunder Storm start/do callback and shared wrapper."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/thunder-storm-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    paths=['source/D2Game/src/SKILLS/SkillSor.cpp','source/D2Game/src/SKILLS/Skills.cpp'];texts={};hashes={};blocks={}
    for rel in paths:
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);texts[rel]=raw.decode();hashes[rel]=sha(p)
    for rel,name in [(paths[1],'D2GAME_CreateMissile_6FD115E0'),(paths[0],'SKILLS_SrvSt13_ThunderStorm'),(paths[0],'SKILLS_SrvDo029_ThunderStorm')]:blocks[name]=function(texts[rel],name)
    cpp=r'''
#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int STAT_MODIFIERLIST_SKILL=1,STAT_MODIFIERLIST_LEVEL=2;
struct D2GameStrc{} game;struct D2ActiveRoomStrc{bool town=false;} ownerRoom,targetRoom;
struct D2DynamicPathStrc{int x=11,y=13;} path;
struct D2UnitStrc{int dwUnitId=34,x=34,y=46;D2DynamicPathStrc* pDynamicPath=&path;D2ActiveRoomStrc* room=&targetRoom;} owner,target,missile;
struct D2SkillStrc{int p1=77,p2=0;} skill;
struct D2SkillsTxt{int wSrvMissileA=166,nAuraState=57,dwAuraLenCalc=0,dwParam[8]={0,0,0,0,0,0,17,0};} row;
struct Tables{int nMissilesTxtRecordCount=1000,nStatesTxtRecordCount=200;} tables;auto* sgptDataTables=&tables;
struct D2CurseStrc{D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTarget=nullptr;int nSkill=0,nSkillLevel=0,nDuration=0,nStat=0,nStatValue=0,nState=0;};struct D2StatListStrc{} list;
struct D2MissileStrc{int nSkill=0,nMissile=0,dwFlags=0;D2UnitStrc* pOwner=nullptr;int nX=0,nY=0,nSkillLevel=0;};
bool hasRow=true,hasSkill=true,active=false,found=true,allocate=true,statList=true;
int curses=0,writes=0,searches=0,creates=0,hits=0,removes=0,events=0,seenRadius=0,seenFlags=0,seenLast=0;
D2MissileStrc captured;D2CurseStrc curse;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return hasSkill?&skill:nullptr;}
void SKILLS_SetParam1(D2SkillStrc* p,int n){p->p1=n;}void SKILLS_SetParam2(D2SkillStrc* p,int n){p->p2=n;}
int SKILLS_GetParam1(D2SkillStrc* p){return p->p1;}int SKILLS_GetParam2(D2SkillStrc* p){return p->p2;}
bool STATES_CheckState(D2UnitStrc*,int){return active;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int rank){return 3600+600*(rank-1);}
D2StatListStrc* sub_6FD10EC0(D2CurseStrc* p){curses++;curse=*p;return statList?&list:nullptr;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int,int,int){writes++;}
D2UnitStrc* sub_6FD107F0(D2GameStrc*,D2UnitStrc*,int,int,int radius,int flags,int last,int){searches++;seenRadius=radius;seenFlags=flags;seenLast=last;return found?&target:nullptr;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return p->room;}bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc* p){return p->town;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return nullptr;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->x;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->y;}
int UNITS_GetDistanceToCoordinates(D2UnitStrc*,int,int){return 0;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){creates++;captured=*p;return allocate?&missile:nullptr;}
void MISSMODE_SrvDmgHitHandler(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){hits++;}
void SUNIT_RemoveUnit(D2GameStrc*,D2UnitStrc*){removes++;}
void sub_6FCC6300(D2UnitStrc*,D2UnitStrc*,int,int,int,int,int){events++;}
'''
    cpp+='\n'.join(blocks.values())
    cpp+=r'''
int main(){printf("{\"start\":[");bool first=true;
for(int r:{0,1})for(int s:{0,1}){hasRow=r;hasSkill=s;skill={77,7};int ret=SKILLS_SrvSt13_ThunderStorm(&game,&owner,57,1);printf("%s[%d,%d,%d,%d,%d]",first?"":",",r,s,ret,skill.p1,skill.p2);first=false;}
printf("],\"do\":[");first=true;
for(int guard=0;guard<7;guard++)for(int state:{0,1})for(int reset:{0,1})for(int chosen:{0,1})for(int town:{0,1})for(int otherTown:{0,1})for(int make:{0,1})for(int listFound:{0,1})for(int unit:{0,1})for(int rank:{1,20,255}){
hasRow=guard!=1;hasSkill=guard!=6;row.wSrvMissileA=guard==2?-1:guard==3?1000:166;row.nAuraState=guard==4?-1:guard==5?200:57;
active=state;found=chosen;allocate=make;statList=listFound;ownerRoom.town=town;targetRoom.town=otherTown;owner.room=&ownerRoom;skill={77,reset};
curses=writes=searches=creates=hits=removes=events=seenRadius=seenFlags=seenLast=0;captured={};curse={};
int ret=SKILLS_SrvDo029_ThunderStorm(&game,unit?&owner:nullptr,57,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,state,reset,chosen,town,otherTown,make,listFound,unit,rank,ret,skill.p1,skill.p2,curses,writes,searches,creates,hits,removes,events,seenRadius,seenFlags,seenLast,captured.dwFlags,captured.pOwner==&owner,captured.nX,captured.nY,captured.nSkillLevel,curse.nDuration,curse.nSkillLevel);first=false;
}printf("]}\n");}
'''
    STAGE.mkdir(exist_ok=True);p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
    data=json.loads(subprocess.check_output([str(binary)]));assert len(data['start'])==4 and len(data['do'])==5376
    for row in data['start']:
        r,s,ret,p1,p2=row;assert (ret,p1,p2)==((1,-1,1) if r and s else (0,77,7))
    for r in data['do']:
        guard,state,reset,chosen,town,otherTown,make,listFound,unit,rank=r[:10];valid=guard==0;buff=valid and (not state or reset);attack=valid and not buff
        create=attack and chosen and unit and not town and not otherTown;hit=create and make
        assert r[10:20]==[int(valid),77 if not attack else 34 if chosen else -1,0 if valid else reset,int(buff),2 if buff and listFound else 0,int(attack),int(create),int(hit),int(hit),int(hit)]
        assert r[20:23]==([17,3,77] if attack else [0,0,0])
        assert r[23:28]==([1,1,34,46,rank] if create else [0,0,0,0,0])
        assert r[28:]==([3600+600*(rank-1),rank] if buff else [0,0])
    san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(san)],check=True)
    log=STAGE/'sanitizer.log'
    with log.open('wb') as out:assert json.loads(subprocess.check_output([str(san)],stderr=out))==data
    assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},startCases=4,periodicCases=5376,sanitizers='ASan/UBSan clean',
        established=['Complete start13 resets prior target=-1 and initialization flag=1; missing row/skill refuse without mutation.', 'Complete do29 separates buff/recast and periodic strike branches,retains supplied skill/rank in the buff,queries target with radius17/filter3/prior ID,checks caster/target town before fresh flags1 target-position missile creation,then hits/removes/calls its event only on successful creation. Target memory updates even if town/factory gates suppress damage; no target resets it.', 'Complete shared wrapper forwards retained skill/rank and target position on each strike. Fresh creation supports strike-time suppliers; this harness does not execute the full damage/stat factory or interpreter.'],
        declaredServices=['Current table duration formula3600+600*(rank-1); target/room/state/list/skill/table/factory/hit/event services are explicit. Null-owner helpers are safe service declarations,not whole game-engine guarantees.', 'Exact target selection/filter and periodic scheduling/damage/Slow Missiles/mana are outside these functions and require separate evidence. No modern executable trace is claimed.'],
        artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),p,binary,san,log,fixture)})
    out=ROOT/'tools/reference/thunder-storm-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Thunder Storm:4start/5376complete periodic cases and shared wrapper;ASan/UBSan clean;full damage/scheduler/current executable remain separate.')
if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--check',action='store_true');probe(ap.parse_args().check)
