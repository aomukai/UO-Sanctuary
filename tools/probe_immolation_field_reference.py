#!/usr/bin/env python3
"""Execute complete stationary field collision, point-unit lookup and do5 callback."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_immolation_routing_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
mode='source/D2Game/src/MISSILES/MissMode.cpp';units='source/D2Common/src/Units/Units.cpp';pathrel='source/D2Common/src/Path/Path.cpp'
base=(STAGE/'immolation-routing-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('struct D2ActiveRoomStrc{};','struct D2UnitStrc;struct D2ActiveRoomStrc{D2UnitStrc* pUnitFirst=nullptr;};\nstruct D2PathPointStrc{uint16_t X=40,Y=70;};\nstruct D2DynamicPathStrc{int dwVelocity=0,dwFlags=1,nCollidedWithMask=0,dwUnitSize=1,nSavedStepsCount=1;D2ActiveRoomStrc* pRoom=nullptr;struct{int wPosX=40,wPosY=70;}tGameCoords;D2PathPointStrc SavedSteps[1];};')
base=base.replace('int dwUnitType=3,','int dwAnimMode=0,dwFlags=3,size=2,remaining=100,activation=100,nSeqCurrentFramePrecise=0;D2UnitStrc* pRoomNext=nullptr;int dwUnitType=3,').replace('void* pDynamicPath=nullptr;','D2DynamicPathStrc* pDynamicPath=nullptr;')
base=base.replace('nCollideFriend=0;','nCollideFriend=0,nSubStart=12,nSubStop=36;')
base=base.replace('int UNITS_GetUnitSizeX(D2UnitStrc*){return 1;}','int UNITS_GetUnitSizeX(D2UnitStrc* p){return p->size;}')
base+=r'''
#define D2_ASSERT assert
constexpr int PATH_SAVE_STEPS_MASK=1,MISSMODE_BARRIERCOLLIDE=6,UNITFLAG_ISVALIDTARGET=1,UNITFLAG_CANBEATTACKED=2;
constexpr int PLRMODE_DEATH=12,PLRMODE_DEAD=13,MONMODE_DEATH=12,MONMODE_DEAD=13;
constexpr int COLLIDE_CORPSE=0x8000;
struct D2DrlgCoordsStrc{int nSubtileX=0,nSubtileY=0,nSubtileWidth=200,nSubtileHeight=200;};
struct D2MissileUnitFindArgStrc{D2GameStrc* pGame;D2UnitStrc* pMissile;D2UnitStrc* pOwner;D2MissilesTxt* pMissilesTxtRecord;};
struct CollisionRow{int(*pfUnitFindCallback)(D2UnitStrc*,void*)=nullptr;unsigned nCollisionMask=0x184;};
D2ActiveRoomStrc* adjacent[1]={&room};int worldMask=0x140,masks=0;D2DynamicPathStrc path;D2UnitStrc second;
void DUNGEON_GetAdjacentRoomsListFromRoom(D2ActiveRoomStrc*,D2ActiveRoomStrc*** out,int* count){*out=adjacent;*count=1;}
void DUNGEON_GetRoomCoordinates(D2ActiveRoomStrc*,D2DrlgCoordsStrc* out){*out={};}
int UNITS_GetXPosition(D2UnitStrc* p){return p->x;}int UNITS_GetYPosition(D2UnitStrc* p){return p->y;}
unsigned COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,unsigned mask){return worldMask&mask;}
void COLLISION_SetMaskWithSize(D2ActiveRoomStrc*,int,int,int,unsigned mask){assert(mask==0x40);masks++;}
int PATH_GetVelocity(D2DynamicPathStrc* p){return p->dwVelocity;}int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){return 1;}
int MISSILE_GetCurrentFrame(D2UnitStrc* p){return p->remaining;}void MISSILE_SetCurrentFrame(D2UnitStrc* p,int f){p->remaining=f;}
int MISSILE_GetActivateFrame(D2UnitStrc* p){return p->activation;}
int ITEMS_RollLimitedRandomNumber(int*,int limit){return limit>0?0:0;}
'''
for rel,name in [(units,'D2Common_10407_Impl'),(units,'D2Common_10407'),(pathrel,'D2Common_10201'),(pathrel,'D2COMMON_10198_PathGetSaveStep'),(mode,'MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster')]:base+=routine(rel,name)+'\n'
base+='CollisionRow stru_6FD2E5F8[9];\n'+routine(mode,'MISSMODE_HandleMissileCollision')+'\n'+routine(mode,'MISSMODE_SrvDo05_FireWall_ImmolationFire_MeteorFire_MoltenBoulderFirePath')
base+=r'''
void reset(){parent={};owner={};target={};second={};path={};parent.dwClassId=230;parent.size=1;parent.pGame=&game;parent.pMissileData=&parent.md;parent.pDynamicPath=&path;path.pRoom=&room;target.dwUnitType=1;target.dwUnitId=10;second=target;second.dwUnitId=20;room.pUnitFirst=&target;row={};row.dwMissileFlags=0;row.nCollideKill=row.nToHit=row.nAlwaysExplode=row.wSrvHitFunc=0;row.wSrvDmgFunc=3;hasRecord=hasSkill=hasOwner=true;terrain=0;arCalls=fillCalls=flagCalls=damageCalls=areaCalls=clears=masks=0;randomValue=randomCalls=0;worldMask=0x140;stru_6FD2E5F8[3].pfUnitFindCallback=MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster;}
int main(){bool first=true;std::printf("{\"footprint\":[");
for(int size:{1,2,3,4})for(int dx=-3;dx<=3;dx++)for(int dy=-3;dy<=3;dy++)for(int kind:{0,1,3})for(int dead:{0,1}){
 reset();target.size=size;target.x=40+dx;target.y=70+dy;target.dwUnitType=kind;target.dwAnimMode=dead?12:0;int result=MISSMODE_SrvDo05_FireWall_ImmolationFire_MeteorFire_MoltenBoulderFirePath(&game,&parent);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",size,dx,dy,kind,dead,result,parent.remaining,fillCalls,flagCalls,arCalls,masks);
}
std::printf("],\"life\":[");first=true;
for(int remaining:{1,2,12,99,100})for(int mask:{0,0x40,0x140,4})for(int repeat:{0,1}){
 reset();parent.remaining=remaining;worldMask=mask;if(repeat){parent.md.nOwnerType=1;parent.md.dwOwnerGUID=10;}
 int result=MISSMODE_SrvDo05_FireWall_ImmolationFire_MeteorFire_MoltenBoulderFirePath(&game,&parent);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",remaining,mask,repeat,result,parent.remaining,fillCalls,randomCalls,masks);
}
std::printf("],\"order\":[");first=true;
for(int reversed:{0,1}){reset();room.pUnitFirst=reversed?&second:&target;room.pUnitFirst->pRoomNext=reversed?&target:&second;D2MissileUnitFindArgStrc args{&game,&parent,&owner,&row};auto* found=D2Common_10407(&room,40,70,MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster,&args,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d]",reversed,found?found->dwUnitId:-1);}std::printf("]}\n");}
'''
cpp=STAGE/'immolation-field-source-probe.cpp';cpp.write_text(base);binary=STAGE/'immolation-field-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(cpp),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data['footprint'])==1176 and len(data['life'])==40 and len(data['order'])==2
for size,dx,dy,kind,dead,result,remaining,fill,flags,ar,masks in data['footprint']:
 footprint=(dx==dy==0) if size==1 else abs(dx)+abs(dy)<=1 if size==2 else abs(dx)<=1 and abs(dy)<=1;eligible=footprint and kind!=3 and not dead
 assert(result,remaining,fill,flags,ar,masks)==(1,99,int(eligible),int(eligible),0,1)
for remaining,mask,repeat,result,out,fill,draws,masks in data['life']:
 eligible=remaining>1 and mask==0x140;assert(out,fill,draws,masks)==(remaining-1,int(eligible),int(eligible),1)
 assert result==(2 if remaining<=1 or mask&5 else 1)
assert data['order']==[[0,10],[1,20]]
fixture=STAGE/'immolation-field-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.immolationarrow',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,sharedRouter='tools/reference/immolation-routing-engine-evidence.json',probe=dict(footprintCases=1176,lifetimeCases=40,roomOrderCases=2,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete do5/collision caller decrements lifetime before unit collision. A100-frame stationary field can damage on99 following updates; the final frame removes it without damage. Missing target collision masks do not damage; a hard wall removes the field. No LastCollide/ToHit/CollideKill permits repeated same-victim field contacts.','Complete D2Common_10407 lookup gives a point missile one cell against size1, a5-cell Manhattan cross against size2, and a3x3 square against size3 (larger clamps to3). Missile/nonliving targets are rejected by the complete current recipient callback.','Point lookup returns the first eligible unit in source room-list order; it does not hit all overlapping recipients or choose lowest ID. Native room/sector order remains a separate projection, but each field cell must select at most one recipient per update.','Reviewed source dynamic-path initialization saves one starting point; complete D2Common_10201 queries stationary collision masks. Complete do5 sets the0x40 field footprint and delegates collision/lifetime to the shared caller.'],limits=['Room membership/coordinates, collision mask world grid, path velocity/movement, formula/stat/final damage/flags services remain declared adapters. These execute full lookup/callback/caller/do5 bodies, not a complete modern terrain/path/damage event engine.','Do5 animation draws are declared; exact seed correlation is not established. Native half-tile terrain/Z/room ordering, owner/world transitions, localdelay and full modern client/geometry behavior still need acceptance. No handler enabled.'])
out=ROOT/'tools/reference/immolation-field-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Immolation field evidence stale'
else:out.write_text(encoded)
print('Immolation:1176 complete point-footprint,40 lifetime/collision and2 room-order cases compiled.')
