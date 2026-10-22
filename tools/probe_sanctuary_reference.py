#!/usr/bin/env python3
"""Bounded immutable Sanctuary knockback-mode/path proof with explicit terrain/world stubs."""
import hashlib
import json
from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
COMMIT='0c0bbd1e6bef9181a3da1c046523af622a925416'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']==COMMIT and not tree['truncated']
blobs={r['path']:r['sha'] for r in tree['tree']}
paths={k:STAGE/'d2moo-pinned-source'/v for k,v in {
 'misc':'source/D2Common/src/Path/PathMisc.cpp','mode':'source/D2Game/src/MONSTER/MonsterMode.cpp',
 'unit':'source/D2Common/src/Units/Units.cpp','damage':'source/D2Game/src/UNIT/SUnitDmg.cpp','pal':'source/D2Game/src/SKILLS/SkillPal.cpp',
}.items()}
for path in paths.values():
 b=path.read_bytes();rel=path.relative_to(STAGE/'d2moo-pinned-source').as_posix();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==blobs[rel]
def routine(key,name):
 s=paths[key].read_text();at=s.index('__fastcall '+name+'(') if '__fastcall '+name+'(' in s else s.index(name+'(');a=s.rfind('\n',0,at)+1;b=s.index('{',at)+1;depth=1
 while depth:
  depth+=(s[b]=='{')-(s[b]=='}');b+=1
 return s[a:b]
cpp=r'''#include <cstdint>
#include <cstdio>
#include <algorithm>
#include <cstdlib>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_MONSTER=1,PATHTYPE_KNOCKBACK_SERVER=8,MONSTER_SANDLEAPER1=99;
int gdwBitMasks[32];struct D2GameStrc{} game;
struct D2UnitStrc;
struct D2PathPointStrc{int X=0,Y=0;bool operator==(const D2PathPointStrc&)const=default;};
struct D2DynamicPathStrc{int nDist=5,dwPathPoints=0,type=0;D2UnitStrc* pTargetUnit=nullptr;D2PathPointStrc PathPoints[1],final;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,dwAnimMode=1,x=0,y=0;D2DynamicPathStrc* pDynamicPath=nullptr;};
struct D2PathInfoStrc{D2DynamicPathStrc* pDynamicPath;D2PathPointStrc tStartCoord,tTargetCoord;};
struct D2ModeChangeStrc{D2UnitStrc* pUnit;};
struct D2MonStats2Txt{int dwModeFlags=0;} modes;
struct D2MonStatsTxt{int nBaseId=1;} mon;
bool hasModes=true,hasMon=true,clip=false;int combatMode=-1;
int UNITS_GetXPosition(D2UnitStrc* p){return p->x;}int UNITS_GetYPosition(D2UnitStrc* p){return p->y;}
void PATH_RayTrace(D2DynamicPathStrc*,D2PathPointStrc* end,D2PathPointStrc start){if(clip)*end=start;}
void D2COMMON_10170_PathSetTargetPos(D2DynamicPathStrc* p,int x,int y){p->final={x,y};}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return hasModes?&modes:nullptr;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return hasMon?&mon:nullptr;}
void PATH_SetType(D2DynamicPathStrc* p,int type){p->type=type;}
void D2COMMON_10190_PATH_SetDistance(D2DynamicPathStrc* p,int distance){p->nDist=distance;}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc*,int mode){combatMode=mode;}
int D2Common_10142(D2DynamicPathStrc*,D2UnitStrc*,int);
'''
cpp+=routine('misc','PATH_Knockback_Server')+'\n'
cpp+=r'''int D2Common_10142(D2DynamicPathStrc* path,D2UnitStrc* unit,int){D2PathInfoStrc info{path,{unit->x,unit->y},{unit->x,unit->y}};return PATH_Knockback_Server(&info);}
'''
cpp+=routine('mode','sub_6FC64B60')+'\n'
cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1<<i;bool first=true;printf("{\"path\":[");
for(int dx=-4;dx<=4;dx++)for(int dy=-4;dy<=4;dy++)for(int distance: {5,10})for(int blocked: {0,1}){
 D2UnitStrc source;D2DynamicPathStrc path;path.pTargetUnit=&source;path.nDist=distance;D2PathInfoStrc info{&path,{dx,dy},{0,0}};clip=blocked;
 int result=PATH_Knockback_Server(&info);printf("%s[%d,%d,%d,%d,%d,%d,%d]",first?"":",",dx,dy,distance,blocked,result,path.final.X,path.final.Y);first=false;
}printf("],\"mode\":[");first=true;
for(int available: {0,1})for(int flags: {0,8,8192,8200})for(int alive: {0,1})for(int leaper: {0,1}){
 D2UnitStrc source,target;D2DynamicPathStrc path;target.pDynamicPath=&path;target.x=3;target.dwAnimMode=alive;path.pTargetUnit=&source;
 D2ModeChangeStrc change{&target};hasModes=available;modes.dwModeFlags=flags;mon.nBaseId=leaper?99:1;combatMode=-1;clip=false;
 int result=sub_6FC64B60(&game,&change);printf("%s[%d,%d,%d,%d,%d,%d,%d]",first?"":",",available,flags,alive,leaper,result,path.nDist,combatMode);first=false;
}printf("]}\n");}
'''
a=STAGE/'sanctuary-source-probe.cpp';binary=STAGE/'sanctuary-source-probe';a.write_text(cpp);subprocess.run(['c++','-std=c++20','-O2',str(a),'-o',str(binary)],check=True)
r=json.loads(subprocess.check_output([str(binary)],text=True))
for dx,dy,distance,blocked,result,x,y in r['path']:
 maximum=max(abs(dx),abs(dy));half=(distance>>1)+1
 xx=dx+int(dx*half/maximum) if maximum else dx;yy=dy+int(dy*half/maximum) if maximum else dy
 if blocked:xx,yy=dx,dy
 assert (x,y)==(xx,yy) and result==int((x,y)!=(dx,dy))
for available,flags,alive,leaper,result,distance,mode in r['mode']:
 valid=available and flags==8200
 assert result==int(valid) and mode==(13 if valid and alive else -1)
 assert distance==(10 if valid and alive and leaper else 5)
pal=paths['pal'].read_text();damage=paths['damage'].read_text();unit=paths['unit'].read_text()
assert 'PATH_SetVelocity(pUnit->pDynamicPath, 4096' in unit
assert damage.index('nAttackRate += nUndeadToHit;')<damage.index('int32_t nToHit = nAttackRate + MONSTERUNIQUE_CalculatePercentage(nAttackRate, nToHitPercent, 100)')
assert 'pDamage->dwEnDmgPct += nUndeadDamagePct;' in damage
assert 'nResValue = std::min(nResValue, 0);' in damage
out=dict(referenceVersion='3.3.93847',sourceCommit=COMMIT,gitBlobVerification=True,
 sourceHashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths.values()},
 pathCases=len(r['path']),modeCases=len(r['mode']),
 findings=['Unchanged PATH_Knockback_Server: half=(nDist>>1)+1, signed integer Chebyshev-normalized endpoint, ray-trace clipping and no-path when unchanged.','Unchanged monster mode requires both GH/KB modes, distance5 normally/distance10 sand leapers and live-mode transition13.','Inspected Units knockback velocity4096/base1024 yields65536 FP16 subtiles per frame before direction scaling; animation/velocity bypasses ordinary chill.','Inspected undead flat AR is added before percentage bonuses; undead damage adds to physical enhanced-damage percentage.','Sanctuary state separately clamps positive physical resistance tozero. Source BYPASSUNDEAD flag affects all damage-table entries; modern broader flag delivery remains unsupported, not silently certified.'],
 limits=['Ray tracing is an explicit all-clear/fully-blocked stub; no D2 terrain/body/path initializer or modern executable certificate.','World/mode/stat tables and acquisition are explicit stubs. The callback data/modern user observations are separate evidence.','Native controller uses an explicitly provisional two-subtile/tile endpoint projection, accepted UO steps and existing mapped GH/FHR clock.','Sand-leaper post-knockback AI15/45frames is not implemented; its native controller branch is explicitly gated.','Modern supplier-stat thresholds, broader bypass flag scope/delivery, complete native mode/recovery/owner/gear/client behavior remain open.'])
(ROOT/'tools/reference/sanctuary-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n');print('Sanctuary source:',len(r['path']),'path cases;',len(r['mode']),'mode cases; passed.')
