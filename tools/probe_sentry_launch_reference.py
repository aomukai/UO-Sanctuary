#!/usr/bin/env python3
"""Execute complete do17 and Charged Bolt initializer with full distance setter.

Progressive record resolution, target retrieval, allocation and final path build
are explicit services. The record is non-progressive in the pinned sentry data.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
def extract(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;end=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 body=s[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body
cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
constexpr int UNITFLAG_SKSRVDOFUNC=64,PATHTYPE_CHARGEDBOLT=6;
struct D2DynamicPathStrc{static constexpr int MAXPATHLEN=78;uint8_t nDistMax=0,nDist=0;int targetX=0,type=0;};
struct D2SeedStrc{uint32_t low=0;};struct D2UnitStrc{int dwFlags=0,x=100,y=102,total=98,current=98;D2SeedStrc pSeed;D2DynamicPathStrc* pDynamicPath;};
struct D2GameStrc{};struct D2SkillsTxt{int dwCalc[1]={1};}row;
struct Tables{int nMissilesTxtRecordCount=1000;}table;auto* sgptDataTables=&table;
struct D2MissileStrc{int dwFlags=0,nX=0,nY=0,nMissile=0,nSkillLevel=0,nSkill=0,pInitArgs=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOwner=nullptr;void(*pInitFunc)(D2UnitStrc*,int)=nullptr;};
bool hasRow;int missileId,countValue,aimKind,allocationKind,calls,created,rangeValue;
struct Launch{int ordinal,x,y,rank;uint32_t seed;int total,distance;};std::vector<Launch> launches;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==306);return hasRow?&row:nullptr;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int id){assert(id==306);return missileId;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int rank){assert(formula==1&&id==306&&rank==3);return countValue;}
bool D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){int n=calls++;*x=126+n;*y=140-n;if(aimKind==1&&n%2)return false;if(aimKind==2)*x=0;if(aimKind==3)*y=0;return true;}
int MISSILE_GetTotalFrames(D2UnitStrc* p){return p->total;}
void MISSILE_SetTotalFrames(D2UnitStrc* p,int frames){p->total=frames;}
void MISSILE_SetCurrentFrame(D2UnitStrc* p,int frames){p->current=frames;}
void SEED_InitLowSeed(D2SeedStrc* s,int n){s->low=uint32_t(n);}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->targetX;}
void PATH_SetType(D2DynamicPathStrc* p,int type){p->type=type;}
void D2Common_10142(D2DynamicPathStrc* p,D2UnitStrc*,int){assert(p->type==PATHTYPE_CHARGEDBOLT);}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*);
'''
cpp+=extract('source/D2Common/src/Path/Path.cpp','PATH_SetNewDistance')+'\n'
cpp+=extract('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_MissileInit_ChargedBolt')+'\n'
cpp+=r'''
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){
 assert(p->dwFlags==0x21&&p->nSkill==306&&p->nSkillLevel==3&&p->nX==100&&p->nY==102&&p->nMissile==495&&p->pOwner);
 if(allocationKind&&p->pInitArgs%2)return nullptr;
 static D2UnitStrc missile;D2DynamicPathStrc path;path.targetX=p->nTargetX;missile={};missile.total=missile.current=rangeValue;missile.pDynamicPath=&path;p->pInitFunc(&missile,p->pInitArgs);created++;
 launches.push_back({p->pInitArgs,p->nTargetX,p->nTargetY,p->nSkillLevel,missile.pSeed.low,missile.total,path.nDist});return &missile;
}
'''
cpp+=extract('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_SrvDo017_ChargedBolt_BoltSentry')+'\n'
cpp+=r'''
int main(){printf("[");bool first=true;
 for(int present:{0,1})for(int mid:{-1,495,1000})for(int count:{0,1,5,11})for(int aim:{0,1,2,3})for(int alloc:{0,1})for(int range:{1,77,78,98,255,256}){
 hasRow=present;missileId=mid;countValue=count;aimKind=aim;allocationKind=alloc;rangeValue=range;calls=created=0;launches.clear();D2GameStrc game;D2UnitStrc unit;
 int result=SKILLS_SrvDo017_ChargedBolt_BoltSentry(&game,&unit,306,3);
 assert(result==(present&&mid>=0&&mid<1000));assert(calls==(result?count:0));
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",present,mid,count,aim,alloc,range,result,calls);first=false;
 bool comma=false;for(auto& x:launches){printf("%s[%d,%d,%d,%d,%u,%d,%d]",comma?",":"",x.ordinal,x.x,x.y,x.rank,x.seed,x.total,x.distance);comma=true;}
 printf("]]");
 }printf("]\n");}
'''
source=STAGE/'sentry-launch-source-probe.cpp';source.write_text(cpp);binary=source.with_suffix('')
subprocess.run(['g++','-std=c++17','-O2',str(source),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==1152
san=STAGE/'sentry-launch-source-probe-san';subprocess.run(['g++','-std=c++17','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(source),'-o',str(san)],check=True)
with (STAGE/'sentry-launch-sanitizer.log').open('wb') as log:sanitized=subprocess.check_output([str(san)],stderr=log)
assert json.loads(sanitized)==data and not (STAGE/'sentry-launch-sanitizer.log').read_bytes()
fixture=STAGE/'sentry-launch-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'sourceHashes':hashes,'routineHashes':routines,'verification':{'completeVolleyCases':len(data)},'fixture':{'path':str(fixture.relative_to(ROOT)),'sha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'established':['Each ordinal re-queries aim; failed/zero coordinates skip only that missile. Allocation failure does not stop later ordinals or change successful do17 return.','Charged-bolt initialization uses aimX+ordinal seed, clamps total/current frames at77 and calls the full distance setter after uint8 conversion. Source range98 gives77 total frames and77 path distance.'],'limits':['Progressive missile selection, skill formula, target lookup, allocation and actual path building are explicit checked services.','No modern executable timing/target, actual world missile or collision/damage integration is established; range256 is an explicit non-gameplay clamp control.'],'handlerEnabled':False}
path=ROOT/'tools/reference/sentry-launch-source-evidence.json'
if args.check:assert json.loads(path.read_text())==e
else:path.write_text(json.dumps(e,indent=2)+'\n')
print(len(data),'complete source do17/initializer/distance-setter cases; sanitizers clean')
