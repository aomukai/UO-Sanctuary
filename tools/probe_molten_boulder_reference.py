#!/usr/bin/env python3
"""Execute complete older Boulder callbacks; engine/world services stay explicit."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};bodies={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(path,name):
  s=source(path);m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  text=s[m.start():i];bodies[name]=hashlib.sha256(text.encode()).hexdigest();return text+'\n'
 manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text());pins={};tables={}
 for name in ['skills','missiles']:
  raw=(ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json').read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(raw)
 skill=next(v for v in tables['skills'].values() if v.get('*Id')==229);assert skill['srvmissile']=='moltenboulderemerge' and skill['aurarangecalc']=='par1' and skill['Param1']==7
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,MONSTATS2FLAGINDEX_SMALL=0,MONSTATS2FLAGINDEX_LARGE=1;
uint32_t gdwBitMasks[2]={1,2};
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2GameStrc{}game;struct D2ActiveRoomStrc{}room;
struct D2DynamicPathStrc{int dwFlags=0,x=320,y=405;}path;
struct D2UnitStrc{int dwUnitType=UNIT_MISSILE,dwClassId=452,x=300,y=400,frames=37,skill=229,rank=20,nSeqCurrentFramePrecise=0;D2DynamicPathStrc* pDynamicPath=nullptr;D2SeedStrc pSeed{};}actor,owner,target;
struct D2MissilesTxt{int wSubMissile[1]={455},wHitSubMissile[1]={455},nSubStart=12,nSubStop=36;uint32_t dwHitPar[3]={0,1,0};int dwDmgParam[2]={1,50};uint32_t dwHitFlags=2;int wResultFlags=5;}row;
struct D2SkillsTxt{int dwAuraRangeCalc=0;}skill;
struct D2MonStats2Txt{uint32_t dwFlags=0;}species;
struct D2DamageStrc{int dwPhysDamage=0,dwFireDamage=0;uint32_t dwHitFlags=0;int wResultFlags=0;};
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;int nX=0,nY=0,nSkill=0,nSkillLevel=0,nMissile=0,nTargetX=0,nTargetY=0,nRange=0;};
std::vector<D2MissileStrc> creates;struct Area{int range;D2DamageStrc damage;};std::vector<Area> areas;
bool hasOwner=true,hasRow=true,hasSkill=true,hasSpecies=true,areaSuccess=true;int auraRange=7,collisionResult=1,collisions=0,masks=0,fills=0;
int CLIENTS_GetUnitX(D2UnitStrc* p){assert(p);return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){assert(p);return p->y;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* p){return p&&hasOwner?&owner:nullptr;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRow?&row:nullptr;}D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int n){assert(n==229);return hasSkill?&skill:nullptr;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return hasSpecies?&species:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return auraRange;}
int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}int MISSILE_GetCurrentFrame(D2UnitStrc* p){return p->frames;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->x;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->y;}
int D2Common_10234(D2DynamicPathStrc* p){return p?p->dwFlags&8:0;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){creates.push_back(*p);return p->pOwner?&actor:nullptr;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisions++;return collisionResult;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}int UNITS_GetUnitSizeX(D2UnitStrc*){return 2;}
void COLLISION_SetMaskWithSize(D2ActiveRoomStrc*,int x,int y,int size,int mask){assert(x==300&&y==400&&size==2&&mask==0x40);masks++;}
void MISSMODE_FillDamageParams(D2UnitStrc*,D2UnitStrc*,D2DamageStrc* damage){fills++;damage->dwPhysDamage=257;damage->dwFireDamage=514;damage->wResultFlags=64;}
bool sub_6FD10200(D2GameStrc*,D2UnitStrc* p,int x,int y,int range,D2DamageStrc* damage,int trailing){assert(p==&owner&&x==300&&y==400&&trailing==0);areas.push_back({range,*damage});return areaSuccess;}
void Reset(uint32_t low=19){actor={};owner={};target={};actor.pDynamicPath=&path;actor.pSeed.nLowSeed=low;actor.pSeed.nHighSeed=666;owner.dwUnitType=UNIT_PLAYER;target.dwUnitType=UNIT_MONSTER;path={};row={};species={};creates.clear();areas.clear();hasOwner=hasRow=hasSkill=hasSpecies=areaSuccess=true;auraRange=7;collisionResult=1;collisions=masks=fills=0;}
'''
 for name in ['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']:cpp+=routine('source/D2Common/include/D2Seed.h',name).replace('0x6AC690C5i64','0x6AC690C5ULL')
 for name in ['ITEMS_RollRandomNumber','ITEMS_RollLimitedRandomNumber']:cpp+=routine('source/D2Game/src/ITEMS/Items.cpp',name)
 rel='source/D2Game/src/MISSILES/MissMode.cpp'
 for name in ['MISSMODE_CreateMeteor_MoltenBoulderSubmissiles','MISSMODE_SrvDo05_FireWall_ImmolationFire_MeteorFire_MoltenBoulderFirePath','MISSMODE_SrvDo06_MoltenBoulder_FireWallMaker','MISSMODE_SrvHit47_MoltenBoulder','MISSMODE_SrvHit48_MoltenBoulderEmerge','MISSMODE_SrvDmg14_MoltenBoulder']:cpp+=routine(rel,name)
 cpp+=r'''
void PrintCreates(){printf("[");for(size_t i=0;i<creates.size();i++){auto p=creates[i];printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.nMissile,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nRange,p.nSkillLevel,p.pOwner?1:0);}printf("]");}
int main(){bool first=true;printf("{\"emerge\":[");
for(int guard:{0,1,2,3,4,5})for(int aimX:{0,320})for(int aimY:{0,405}){Reset();row.wHitSubMissile[0]=guard==3?-1:guard==5?0:452;hasRow=guard!=2;hasOwner=guard!=4;path.x=aimX;path.y=aimY;
 int result=MISSMODE_SrvHit48_MoltenBoulderEmerge(&game,guard==1?nullptr:&actor,nullptr);printf("%s[%d,%d,%d,%d,",first?"":",",guard,aimX,aimY,result);first=false;PrintCreates();printf("]");}
printf("],\"maker\":[");first=true;
for(int guard:{0,1,2,3,4,5,6})for(int flags:{0,8,9})for(int end:{1,2}){Reset();hasOwner=guard!=1;hasRow=guard!=3;row.wSubMissile[0]=guard==4?-1:guard==6?0:455;path.dwFlags=flags;if(guard==5)actor.pDynamicPath=nullptr;collisionResult=end;
 int result=MISSMODE_SrvDo06_MoltenBoulder_FireWallMaker(&game,guard==2?nullptr:&actor);printf("%s[%d,%d,%d,%d,%d,",first?"":",",guard,flags,end,result,collisions);first=false;PrintCreates();printf("]");}
printf("],\"disc\":[");first=true;
for(int child:{-1,0,455})for(int range:{-1,0,1,7})for(int step:{-1,0,1,2,20}){Reset();MISSMODE_CreateMeteor_MoltenBoulderSubmissiles(&game,&actor,child,range,step);printf("%s[%d,%d,%d,",first?"":",",child,range,step);first=false;PrintCreates();printf("]");}
printf("],\"contact\":[");first=true;
for(int guard:{0,1,2,3,4,5,6,7})for(int flags:{0,1,2,3})for(int radius:{0,3})for(int aura:{0,7})for(int step:{0,2})for(bool success:{false,true}){
 Reset();hasRow=guard!=2;hasOwner=guard!=3;hasSpecies=guard!=6;hasSkill=guard!=7;if(guard==5)target.dwUnitType=UNIT_PLAYER;species.dwFlags=flags;row.dwHitPar[0]=radius;row.dwHitPar[1]=step;auraRange=aura;areaSuccess=success;
 int result=MISSMODE_SrvHit47_MoltenBoulder(&game,guard==1?nullptr:&actor,guard==4?nullptr:&target);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",guard,flags,radius,aura,step,success,result,fills);first=false;
 for(size_t i=0;i<areas.size();i++){auto a=areas[i];printf("%s[%d,%d,%d,%u,%d]",i?",":"",a.range,a.damage.dwPhysDamage,a.damage.dwFireDamage,a.damage.dwHitFlags,a.damage.wResultFlags);}printf("],");PrintCreates();printf("]");}
printf("],\"knockback\":[");first=true;
for(int guard:{0,1,2,3,4,5})for(int unitType:{0,1,4})for(int flags:{0,1,2,3})for(int param:{0,1,2})for(int chance:{0,50})for(uint32_t low:{0u,1u,123u,0xFFFFFFFFu}){
 if(guard && (unitType!=1||flags!=1||param!=1||chance!=50||low!=1))continue;
 Reset(low);hasRow=guard!=3;hasSpecies=guard!=4;if(guard==2)actor.dwUnitType=UNIT_MONSTER;target.dwUnitType=unitType;species.dwFlags=flags;row.dwDmgParam[0]=param;row.dwDmgParam[1]=chance;D2DamageStrc damage;damage.wResultFlags=64;
 MISSMODE_SrvDmg14_MoltenBoulder(&game,guard==1?nullptr:&actor,guard!=1&&guard!=5?&target:nullptr,&damage);
 printf("%s[%d,%d,%d,%d,%d,%u,%d,%llu]",first?"":",",guard,unitType,flags,param,chance,low,damage.wResultFlags,(unsigned long long)actor.pSeed.lSeed);first=false;}
printf("],\"field\":[");first=true;
for(int guard:{0,1,2})for(int remaining:{1,11,12,13,37})for(int animation:{0,2561,2944,3327,9218})for(uint32_t low:{0u,123u}){
 Reset(low);hasRow=guard!=2;actor.frames=remaining;actor.nSeqCurrentFramePrecise=animation;
 int result=MISSMODE_SrvDo05_FireWall_ImmolationFire_MeteorFire_MoltenBoulderFirePath(&game,guard==1?nullptr:&actor);
 printf("%s[%d,%d,%d,%u,%d,%d,%d,%d,%llu]",first?"":",",guard,remaining,animation,low,result,collisions,masks,actor.nSeqCurrentFramePrecise,(unsigned long long)actor.pSeed.lSeed);first=false;}
printf("]}\n");}
'''
 STAGE.mkdir(parents=True,exist_ok=True);path=STAGE/'molten-boulder-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('')
 subprocess.run(['g++','-std=c++20','-O1','-g','-Wall','-Wextra','-Werror','-Wno-unused-parameter','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'molten-boulder-source-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-2000:]
 rows=json.loads(result.stdout);counts={key:len(value) for key,value in rows.items()};assert counts==dict(emerge=24,maker=42,disc=60,contact=512,knockback=293,field=150),counts
 fixture=STAGE/'molten-boulder-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 # Independent checks of central boundary/dispatch invariants; C# comparison follows separately.
 for guard,x,y,ret,created in rows['emerge']:
  assert ret==1 and len(created)==int(guard not in [1,2,3])
 for guard,flags,end,ret,collisions,created in rows['maker']:
  valid=guard in [0,6];assert ret==(end if valid else 2) and collisions==int(valid) and len(created)==int(valid and flags&8!=0)
 for child,range_,step,created in rows['disc']:
  assert len(created)==(0 if child<0 else len(range(0,18,max(1,step))))
  for request in created:assert request[0]==(0x8001 if range_>0 else 1) and request[6]==max(0,range_)
 for guard,flags,radius,aura,step,success,ret,fills,areas,created in rows['contact']:
  large=(flags&2)!=0;valid=guard in [0,7] and large;assert fills==int(valid)
  area=valid and (radius>0 or guard!=7);assert len(areas)==int(area)
  if area:assert ret==(1 if success else 3) and areas[0]==[radius or max(aura,1),257,514,2,69]
  elif guard in [1,2,3] or valid:assert ret==1
  else:assert ret==2
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,sourceHashes=hashes,routineHashes=bodies,pinnedInputHashes=pins,caseCounts=counts,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),sanitizers='ASan/UBSan exit0 and empty stderr',portableSubstitutions=['Explicit minimal ABI/service fixtures; SMALL/LARGE are named logical flag inputs, not inferred from Size.','MSVC hexadecimal i64 suffix changed to ULL without changing uint64 arithmetic. Complete source RNG and item wrappers execute with each selected callback.'],established=['Complete emergence hit48 preserves coordinates/aim/rank and attempts rolling452 creation; missing owner is delegated to the factory, not invented as an early callback guard.','Complete do6 emits a flags33 same-position trail child on the previous movement flag, before generic collision/lifetime service. Zero child ID differs from negative ID.','Complete hit47 handles actual LARGE flags, captures one controlled packet for its area request, adds row hit/result flags and builds the18-offset disc. Source return1/3 versus2 is retained without claiming the whole generic dispatcher was compiled.','Positive disc range is also a factory lifetime override. Trail455 default37frames and explosion children override7frames are distinct current routes.','Damage14 runs complete named SMALL/LARGE/player branches and actual RNG. Generic row KnockBack/event/eligibility composition remains separate.','Server do5 animation branches mutate the same seed before generic collision. The input animation clock is supplied explicitly; no guessed server sequence increment is introduced.'],limits=['Creation, owner/species/row lookup, packet filling, area damage and generic collision are declared recording services. No native world movement, damage, knockback or learned handler is enabled by these fixtures.','The complete generic collision/hit dispatcher, factory InitSteps, rolling/explosion damage getter composition, field animation clock producer and native eligibility/room/control paths remain to compose.','Current data is pinned; these are older reconstructed bodies, not a modern executable certificate. Missing/uncertain modern mechanics must remain explicit.','Final effects/audio and separate talent/left/right/quick icons remain pending.'],next='Compare reusable C# Boulder request/contact/knockback/field-animation kernels to these source outputs, then connect native creation/motion/damage/control and casting.')
 ep=ROOT/'tools/reference/molten-boulder-source-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert ep.read_text()==text
 else:ep.write_text(text)
 print(sum(counts.values()),'complete Boulder callback/RNG contexts pass; no native handler enabled.')
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
