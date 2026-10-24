#!/usr/bin/env python3
"""Complete Sentry target callbacks/choice with explicit native-adapter services."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/AI/AiUtil.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
s=raw.decode();routines={}
def extract(name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 body=s[m.start():i];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body
cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <climits>
#include <cstdlib>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,STATE_INVIS=1,UNITFLAG_CANBEATTACKED=256,COLLIDE_MISSILE_BARRIER=4,PATHTYPE_TOWARD=2;
struct D2GameStrc{};struct D2UnitStrc;struct D2ActiveRoomStrc{bool town=false;};
struct D2DynamicPathStrc{int type=15,x=1,y=2,points=0;D2UnitStrc* target=nullptr;};
struct D2UnitStrc{D2GameStrc* pGame=nullptr;int id=0,dwUnitType=1,x=100,y=100,size=2,dwClassId=0,dwFlags=256;bool dead=false,invis=false,eligible=true,barrier=false;uint64_t pSeed=0;D2ActiveRoomStrc room;D2DynamicPathStrc* pDynamicPath=nullptr;};
struct D2MonStatsTxt{int nThreat=10;};struct D2UnkAiCallbackArgStrc2{D2UnitStrc* pTarget=nullptr;int nDistance=INT_MAX;D2UnitStrc* pAlternativeTarget=nullptr;int nAlternativeDistance=INT_MAX;};
struct D2AiCallback7ArgStrc{D2UnitStrc* pTarget=nullptr;int nDistance=INT_MAX;D2UnitStrc* unk0x08=nullptr;};
D2UnitStrc units[4],remembered;D2MonStatsTxt records[4];bool hasRow[4],pathSuccess=false,useRemembered=false;
int CLIENTS_GetUnitX(D2UnitStrc* u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc* u){return u->y;}int UNITS_GetUnitSizeX(D2UnitStrc* u){return u->size;}
bool SUNIT_IsDead(D2UnitStrc* u){return u->dead;}D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* u){return &u->room;}bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc* r){return r->town;}
bool STATES_CheckState(D2UnitStrc* u,int){return u->invis;}bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int){return false;}uint64_t ITEMS_RollRandomNumber(uint64_t*){return 99;}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t){return t->eligible;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int id){return hasRow[id]?&records[id]:nullptr;}
bool UNITS_TestCollisionWithUnit(D2UnitStrc* t,D2UnitStrc*,int){return t->barrier;}
int PATH_GetType(D2DynamicPathStrc* p){return p->type;}D2UnitStrc* PATH_GetTargetUnit(D2DynamicPathStrc* p){return p->target;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->x;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->y;}
void PATH_SetTargetUnit(D2DynamicPathStrc* p,D2UnitStrc* t){p->target=t;}void PATH_SetType(D2DynamicPathStrc* p,int type){p->type=type;}
void D2COMMON_10170_PathSetTargetPos(D2DynamicPathStrc* p,int x,int y){p->x=x;p->y=y;}
void D2Common_10142(D2DynamicPathStrc* p,D2UnitStrc*,int){p->points=pathSuccess;}
int PATH_GetNumberOfPathPoints(D2DynamicPathStrc* p){return p->points;}
D2UnitStrc* sub_6FCF1E80(D2GameStrc*,D2UnitStrc*,void*,D2UnitStrc*(*)(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,void*),int);
int sub_6FCF2920(D2GameStrc*,D2UnitStrc*,int,int,D2UnitStrc** out,int* distance){if(!useRemembered)return 0;*out=&remembered;*distance=7;return 1;}
'''
for name in ['AIUTIL_GetDistanceToCoordinatesWithSize','AIUTIL_GetDistanceToCoordinates_FullUnitSize','sub_6FCF1A50','sub_6FCF1980','sub_6FCF1B30','sub_6FCF27B0']:cpp+=extract(name)+'\n'
cpp+=r'''
D2UnitStrc* sub_6FCF1E80(D2GameStrc* g,D2UnitStrc* u,void* args,D2UnitStrc*(*)(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,void*),int callback){assert(callback==6||callback==7);for(auto& t:units){if(callback==6)sub_6FCF1980(g,u,&t,args);else sub_6FCF1B30(g,u,&t,args);}return nullptr;}
'''
cpp+=extract('sub_6FCF2CC0')+'\n'
cpp+=r'''
int main(){printf("[");bool first=true;int positions[4][4]={{3,10,1,7},{14,16,4,8},{50,60,4,7},{26,30,20,45}};
for(int pos=0;pos<4;pos++)for(int threats=0;threats<4;threats++)for(int eligible=0;eligible<16;eligible++)for(int walls:{0,1,3,15})for(int path:{0,1})for(int cache:{0,1}){
 D2GameStrc g;D2DynamicPathStrc dynamic;D2UnitStrc source;source.pDynamicPath=&dynamic;useRemembered=cache;remembered.id=99;pathSuccess=path;
 for(int i=0;i<4;i++){units[i]={};units[i].id=i+1;units[i].dwClassId=i;units[i].size=(pos+i)%4;units[i].x=100+positions[pos][i];units[i].eligible=eligible&(1<<i);units[i].barrier=walls&(1<<i);
 hasRow[i]=threats!=2;records[i].nThreat=threats==0?10:threats==1?(i<2?10:i==2?0:1):0;if(threats==3&&i<2)units[i].dwUnitType=UNIT_PLAYER;}
 int distance=0,melee=0;D2UnitStrc* result=sub_6FCF2CC0(&g,&source,&distance,&melee);
 assert(dynamic.type==15&&dynamic.target==nullptr&&dynamic.x==1&&dynamic.y==2);
 printf("%s{\"input\":[%d,%d,%d,%d,%d,%d],\"candidates\":[",first?"":",",pos,threats,eligible,walls,path,cache);first=false;
 for(int i=0;i<4;i++){auto& t=units[i];printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",t.id,t.x,t.y,t.size,t.dwUnitType,hasRow[i],records[i].nThreat,t.eligible,t.barrier);}
 printf("],\"selected\":[%d,%d]}\n",result?result->id:0,distance);
}printf("]\n");}
'''
p=STAGE/'sentry-target-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++17','-O2',str(p),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==4096
san=STAGE/'sentry-target-source-probe-san';subprocess.run(['g++','-std=c++17','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(san)],check=True)
with (STAGE/'sentry-target-sanitizer.log').open('wb') as log:clean=json.loads(subprocess.check_output([str(san)],stderr=log))
assert clean==data and not (STAGE/'sentry-target-sanitizer.log').read_bytes()
fixture=STAGE/'sentry-target-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'sourceHashes':{rel:hashlib.sha256(raw).hexdigest()},'routineHashes':routines,'cases':len(data),'fixture':{'path':str(fixture.relative_to(ROOT)),'sha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'sanitizers':'ASan/UBSan clean','nativeEvidence':'tools/reference/sentry-target-native-evidence.json','established':['Complete full-size distance, eligibility, callback6/callback7, alternative chooser and main selector execute together. Primary vs low-threat pools, distance48 inclusion, strict smaller-distance/tie retention, barrier rejection, alternative distance5 guard, path success/failure and fallback distance20 guard remain distinct.','Explicit remembered-target service overrides scanning. The alternative path probe restores original path type/target/target-coordinate fields.'],'limits':['Room enumeration, hostile/allegiance test, source remembered-target resolver, path initializer, collision, invisible-player roll/melee and current executable equivalence are declared services; this is not a whole AI engine.','Eligibility matrix varies hostile service results with live/non-town/attackable/non-invisible targets; invisible branch and native player PvP are not certified.','Source callback7 dereferences monster-row metadata without a null guard. No-record controls have no primary and never enter that branch; native integration must flag unsupported missing-row fallback instead of silently inventing threat data.']}
p=ROOT/'tools/reference/sentry-target-source-evidence.json'
if args.check:assert json.loads(p.read_text())==e
else:p.write_text(json.dumps(e,indent=2)+'\n')
print('4096 complete source target-selection contexts; sanitizers clean; world service adapters remain explicit')
