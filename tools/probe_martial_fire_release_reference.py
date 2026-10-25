#!/usr/bin/env python3
"""Complete legacy progressive stack dispatch and fire area/ground placement callbacks."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_fists_of_fire_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/SKILLS/SkillAss.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();blocks={}
 def routine(name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end];return blocks[name]
 names=['sub_6FCF77E0','sub_6FCF5DE0','SKILLS_GetProgressiveSkillMissileId','SKILLS_EvaluateProgressiveSkillCalc','SKILLS_SrvDo038_FistsOfFire_BladesOfIce_ProgressiveFn2','SKILLS_SrvDo039_FistsOfFire_BladesOfIce_ProgressiveFn3','SKILLS_SrvDo143_FistsOfFire_RoyalStrike_ProgressiveFn','sub_6FCF6D70']
 # sub6D70 is unselected for Fists' missing server missileA; keep full143 body but abort that service.
 base=(STAGE/'fists-fire-builder-source-probe.cpp').read_text().split('int main(){',1)[0]
 base=base.replace('#include <algorithm>','#include <algorithm>\n#include <cstring>\n#include <vector>',1).replace('struct D2SkillStrc{};','struct D2SeedStrc{unsigned nLowSeed=0,nHighSeed=0;int index=0;};struct D2SkillStrc{};',1).replace('struct D2UnitStrc{','struct D2UnitStrc{D2SeedStrc pSeed;',1)
 base=base.replace('int nAuraState=4,','unsigned dwFlags[1]={3};int wSrvPrgFunc[3]={143,38,39},dwPrgCalc[3]={102,100,101},dwAuraRangeCalc=102,dwAuraFilter=0,wSrvMissile=-1,wSrvMissileA=-1,wSrvMissileB=-1,wSrvMissileC=557,nAuraState=4,',1).replace('dwFireDamage=0,','dwHitFlags=0,dwFireDamage=0,',1)
 old='return formula==375?375:formula==50?50:3*rank;';assert old in base;base=base.replace(old,'return formula==375?375:formula==50?50:formula==100||formula==101?4:formula==102?0:3*rank;',1)
 old='void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){drains++;}'
 new='std::vector<int> trace;void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){drains++;trace.push_back(1);}'
 assert old in base;base=base.replace(old,new)
 base+=r'''
constexpr int SKILLSFLAGINDEX_PROGRESSIVE=0,SKILLSFLAGINDEX_PRGSTACK=1;unsigned gdwBitMasks[]={1,2};
struct D2ActiveRoomStrc{} room;struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};
struct D2MissileStrc{int nMissile=0,dwFlags=0,nSkill=0,nSkillLevel=0,nX=0,nY=0;D2UnitStrc* pOwner=nullptr;};
using SkillDoFunc=int32_t(*)(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[200]={};int gnSkillSrvDoFns=200;
bool coordinates=true,roomAvailable=true;int ownerDraws=0,localDraws=0,areaCalls=0,areaRadius=0,freed=0,overlaysReleased=0;std::vector<std::vector<int>> hits,missiles;
unsigned ITEMS_RollRandomNumber(D2SeedStrc* p){++ownerDraws;return p->nLowSeed++;}
int ITEMS_RollLimitedRandomNumber(D2SeedStrc* p,int limit){++localDraws;return limit>0?int((p->nLowSeed+5*p->index++)%unsigned(limit)):0;}
void SEED_InitLowSeed(D2SeedStrc* p,unsigned n){p->nLowSeed=n;p->index=0;}
void SEED_GetSeeds(D2SeedStrc* p,unsigned* lo,unsigned* hi){*lo=p->nLowSeed;*hi=p->nHighSeed;}
void SEED_SetSeeds(D2SeedStrc* p,unsigned lo,unsigned hi){p->nLowSeed=lo;p->nHighSeed=hi;}
bool D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=10;*y=20;return coordinates;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*,int,int){return roomAvailable?&room:nullptr;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){missiles.push_back({p->nX,p->nY,p->nMissile,p->nSkill,p->nSkillLevel,p->dwFlags});trace.push_back(3);return &target;}
void D2GAME_RollPhysicalDamage_6FD14EC0(D2UnitStrc*,D2DamageStrc* d,int,int){d->dwPhysDamage=0;}
void sub_6FD10140(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* d,int){hits.push_back({d->dwPhysDamage,d->dwFireDamage,d->wResultFlags,d->dwHitFlags});d->dwFireDamage+=1;trace.push_back(2);}
void sub_6FD0FE80(D2GameStrc* g,D2UnitStrc* u,int x,int y,int range,unsigned,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* data,int,const char*,int){if(x!=10||y!=20)std::abort();++areaCalls;areaRadius=range;D2AuraCallbackStrc ctx{g,u,data};callback(&ctx,&rightWeapon);callback(&ctx,&leftWeapon);}
void sub_6FCF6D70(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int,int){std::abort();}
void D2Common_10474(D2UnitStrc*,D2StatListStrc*){exists=false;}
void STATLIST_FreeStatList(D2StatListStrc*){++freed;}
void STATES_UpdatePgsvFlags(D2UnitStrc*){}
void sub_6FCC6300(D2UnitStrc*,D2UnitStrc*,int,int,int,int,int){++overlaysReleased;}
'''
 for name in names[:-1]:base+='\n'+routine(name) if name!='sub_6FCF77E0' else ''
 base+='\n'+routine('sub_6FCF77E0')+r'''
int main(){gpSkillSrvDoFnTable_6FD40A20[143]=SKILLS_SrvDo143_FistsOfFire_RoyalStrike_ProgressiveFn;gpSkillSrvDoFnTable_6FD40A20[38]=SKILLS_SrvDo038_FistsOfFire_BladesOfIce_ProgressiveFn2;gpSkillSrvDoFnTable_6FD40A20[39]=SKILLS_SrvDo039_FistsOfFire_BladesOfIce_ProgressiveFn3;bool comma=false;printf("[");
for(int count:{1,2,3})for(int stored:{0,1,20})for(int live:{0,3,30})for(int gates=0;gates<64;++gates)for(int seed:{0,7}){
 row={};list={count,count*50,stored,259,321};currentRank=live;saved={};saved.wResultFlags=gates&1;selected=gates&2;inRange=gates&4;enabled=gates&8;exists=gates&16;coordinates=gates&32;record=true;roomAvailable=true;owner.pSeed={unsigned(seed),0,0};ownerDraws=localDraws=areaCalls=areaRadius=freed=overlaysReleased=drains=0;missiles.clear();hits.clear();trace.clear();sub_6FCF77E0(&game,&owner,&saved);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,[",count,stored,live,gates,seed,areaCalls,areaRadius,localDraws,freed,drains,owner.pSeed.nLowSeed);bool sep=false;for(auto h:hits){if(sep)printf(",");sep=true;printf("[%d,%d,%d,%d]",h[0],h[1],h[2],h[3]);}printf("],[");sep=false;for(auto m:missiles){if(sep)printf(",");sep=true;printf("[%d,%d,%d,%d,%d,%d]",m[0],m[1],m[2],m[3],m[4],m[5]);}printf("],[");sep=false;for(int t:trace){if(sep)printf(",");sep=true;printf("%d",t);}printf("]]");
}printf("]\n");}
'''
 path=STAGE/'martial-fire-release-source-probe.cpp';path.write_text(base);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==3456
 for count,stored,live,gates,seed,areas,radius,draws,freed,drains,ownerSeed,hits,missiles,trace in data:
  release=(gates&31)==31;rank=max(stored,live);apply=release and rank>0;coord=bool(gates&32);area=apply and count>=2;ground=apply and count==3
  assert (areas,radius,draws,freed,drains,ownerSeed)==(int(bool(area and coord)),4 if area and coord else 0,32 if ground and coord else 0,int(bool(release)),int(bool(area)),seed+int(bool(ground)))
  assert hits==([[0,rank*100,1,1]]*2 if area and coord else [])
  expected=[]
  if ground and coord:
   for i in range(16):
    x=4-(seed+10*i)%8;y=4-(seed+10*i+5)%8
    if x*x+y*y<=16:expected.append([10+x,20+y,557,259,rank,1])
  assert missiles==expected
  assert trace==(([1]+([2,2] if coord else [])) if area else [])+[3]*len(expected)
 fixture=STAGE/'martial-fire-release-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.fistsoffire',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),sanitizers='ASan+UBSan'),established=['Complete old progressive dispatcher temporarily selects charge levels1/2 for earlier stacked callbacks, restores full count, then executes final callback. Rank is max(stored,current). Source list removal occurs after dispatch even with zero rank; current consume-one policy is separate.','Missing Fists server missileA suppresses callback143. Charge2/3 callback38 drains parent before coordinate resolution, rolls an independent skill area packet and copies it per receiver. Explicit mutation of one receiver packet does not affect the next.','Charge3 callback39 consumes one owner seed before record/coordinate validation, then uses a local seed for16attempts at radius4, rejecting outside-circle points. Valid fields retain explicit557/259/rank/flags1 factory arguments.'],declaredServices=['Element roll supplies rank*100; physical skill roll0 reflects selected Fists zero physical endpoints, not a compiled damage getter. Area enumeration supplies two accepting receivers with a mutating damage service; complete target/filter/mitigation is not claimed.','Owner RNG is incrementing fixture state; local limited RNG is a deterministic declared sequence, not the actual D2 PRNG. Tests establish seed ownership/draw count/control flow and positions for those supplied draws.','Legacy state removal, item durability/primary commit, factory allocation and room validity are explicit services. No actual parent health/proc/death or native field lifecycle acceptance.','sub6FCF6D70 service aborts because real Fists has no missileA. Full143body compiles; Royal Strike positive-missile branches need their own proof.'],limits=['Current consume-one, full source PRNG/skill damage/mastery/area filter, generic field collision and native handler/input/persistence remain separate.','This proves full selected callback/dispatcher control flow, not a native Fists or modern executable certificate. Effects/icons are independently pending.'])
 out=ROOT/'tools/reference/martial-fire-release-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Martial fire release evidence stale'
 else:out.write_text(encoded)
 print('Martial fire release:3456 complete progressive dispatch/area/ground callback cases pass under declared RNG/damage/world services.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
