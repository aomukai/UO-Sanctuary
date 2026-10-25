#!/usr/bin/env python3
"""Compile complete Thunder progressive dispatch, rings, PRNG and child init."""
import argparse, hashlib, json, re, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_martial_fire_release_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  blocks[name]=text[m.start():i];return blocks[name]
 ass=source('source/D2Game/src/SKILLS/SkillAss.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');seed=source('source/D2Common/include/D2Seed.h');seedcpp=source('source/D2Common/src/D2Seed.cpp');items=source('source/D2Game/src/ITEMS/Items.cpp');path=source('source/D2Common/src/Path/Path.cpp');miss=source('source/D2Common/src/Units/Missile.cpp')
 base=(STAGE/'martial-fire-release-source-probe.cpp').read_text().split('int main(){',1)[0]
 def replace(old,new):
  nonlocal base
  assert base.count(old)==1,old;base=base.replace(old,new)
 replace('#include <cstring>','#include <cstring>\n#include <array>\n#include <cassert>\n#define __stdcall\nconstexpr int64_t operator""i64(unsigned long long x){return int64_t(x);}')
 replace('struct D2SeedStrc{unsigned nLowSeed=0,nHighSeed=0;int index=0;}', 'struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};int index=0;D2SeedStrc():lSeed(0){}}')
 replace('struct D2UnitStrc{D2SeedStrc pSeed;', 'struct D2DynamicPathStrc{static constexpr int MAXPATHLEN=78;uint8_t nDist=0,nDistMax=0;int type=0,builds=0;};struct D2MissileDataStrc{struct{int nX=0;}pTargetCoords;};struct D2UnitStrc{D2SeedStrc pSeed;int x=10,y=20,total=0,current=0;D2DynamicPathStrc* pDynamicPath=nullptr;D2MissileDataStrc* pMissileData=nullptr;')
 replace('} row;', '} row,royal;')
 replace('D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}', 'D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return record?(id==280?&royal:&row):nullptr;}')
 replace('int nMissile=0,dwFlags=0,nSkill=0,nSkillLevel=0,nX=0,nY=0;', 'int nMissile=0,dwFlags=0,nSkill=0,nSkillLevel=0,nX=0,nY=0,nTargetX=0,nTargetY=0,nVelocity=0,pInitArgs=0;void(*pInitFunc)(D2UnitStrc*,int)=nullptr;')
 replace('unsigned ITEMS_RollRandomNumber(D2SeedStrc* p){++ownerDraws;return p->nLowSeed++;}', routine(seed,'SEED_RollRandomNumber')+'\n'+routine(items,'ITEMS_RollRandomNumber'))
 replace('void SEED_InitLowSeed(D2SeedStrc* p,unsigned n){p->nLowSeed=n;p->index=0;}', routine(seedcpp,'SEED_InitLowSeed'))
 replace('D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){missiles.push_back({p->nX,p->nY,p->nMissile,p->nSkill,p->nSkillLevel,p->dwFlags});trace.push_back(3);return &target;}', 'D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*);')
 base+=r'''
constexpr int PATHTYPE_CHARGEDBOLT=6,UNIT_MISSILE=3,SKILL_ROYALSTRIKE=280;
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int DATATBLS_GetMissileVelocityFromMissilesTxt(int id,int){return id==559?24:12;}
uint8_t MISSILE_GetTotalFrames(D2UnitStrc* p){return uint8_t(p->total);}
void MISSILE_SetTotalFrames(D2UnitStrc* p,int n){p->total=n;}
void MISSILE_SetCurrentFrame(D2UnitStrc* p,int n){p->current=n;}
void PATH_SetType(D2DynamicPathStrc* p,int n){p->type=n;}
void D2Common_10142(D2DynamicPathStrc* p,D2UnitStrc*,int n){assert(n==0);++p->builds;}
void sub_6FCF6C10(D2UnitStrc*,int);
void sub_6FD14170(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int,int,int);
void sub_6FCF6600(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int,int);
'''
 base+='\n'+routine(path,'PATH_SetNewDistance')+'\n'+routine(miss,'MISSILE_SetTargetX')
 for name in ['SKILLS_SrvDo036_ClawsOfThunder_ProgressiveFn2','SKILLS_SrvDo037_ClawsOfThunder_ProgressiveFn3','sub_6FCF6600','sub_6FCF6C10']:base+='\n'+routine(ass,name)
 base+='\n'+routine(skills,'sub_6FD14170')
 for name in ['sub_6FCF77E0','SKILLS_GetProgressiveSkillMissileId','SKILLS_EvaluateProgressiveSkillCalc']:assert base.count(routine(ass,name))==1
 base+=r'''
std::array<D2UnitStrc,80> units;std::array<D2DynamicPathStrc,80> paths;std::array<D2MissileDataStrc,80> data;int allocationKind=0;
std::vector<std::array<int,12>> emitted;
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){
 int index=emitted.size();bool ok=!allocationKind||index%2==0;emitted.push_back({p->nX,p->nY,p->nTargetX,p->nTargetY,p->nMissile,p->nSkill,p->nSkillLevel,p->dwFlags,p->nVelocity,p->pInitArgs,int(p->pInitFunc!=nullptr),int(ok)});
 if(!ok)return nullptr;auto& u=units.at(index);u={};paths[index]={};data[index]={};u.dwUnitType=UNIT_MISSILE;u.pDynamicPath=&paths[index];u.pMissileData=&data[index];u.total=u.current=p->nMissile==558?98:13;
 if(p->pInitFunc)p->pInitFunc(&u,p->pInitArgs);return &u;
}
int main(){gpSkillSrvDoFnTable_6FD40A20[36]=SKILLS_SrvDo036_ClawsOfThunder_ProgressiveFn2;gpSkillSrvDoFnTable_6FD40A20[37]=SKILLS_SrvDo037_ClawsOfThunder_ProgressiveFn3;printf("[");bool comma=false;
 for(int count:{1,2,3})for(int rank:{0,1,20})for(int gates=0;gates<64;++gates)for(int low:{0,7,-1})for(int alloc:{0,1}){
 row={};royal={};royal.dwParam[1]=6;row.wSrvPrgFunc[0]=0;row.wSrvPrgFunc[1]=36;row.wSrvPrgFunc[2]=37;row.wSrvMissileB=559;row.wSrvMissileC=558;row.dwPrgCalc[0]=row.dwPrgCalc[1]=102;row.dwPrgCalc[2]=100;row.dwCalc[0]=102;
 list={count,count*50,rank,269,321};currentRank=rank;saved={};saved.wResultFlags=gates&1;selected=gates&2;inRange=gates&4;enabled=gates&8;exists=gates&16;coordinates=gates&32;record=true;SEED_InitLowSeed(&owner.pSeed,low);ownerDraws=localDraws=freed=0;emitted.clear();allocationKind=alloc;sub_6FCF77E0(&game,&owner,&saved);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%u,%u,%d,[",count,rank,gates,low,alloc,owner.pSeed.nLowSeed,owner.pSeed.nHighSeed,freed);bool sep=false;for(size_t i=0;i<emitted.size();++i){if(sep)printf(",");sep=true;printf("[");for(int j=0;j<12;++j)printf("%s%d",j?",":"",emitted[i][j]);auto& u=units[i];auto& p=paths[i];printf(",%u,%u,%d,%d,%d,%d,%d,%d]",emitted[i][11]?u.pSeed.nLowSeed:0,emitted[i][11]?u.pSeed.nHighSeed:0,emitted[i][11]?u.total:0,emitted[i][11]?u.current:0,emitted[i][11]?p.type:0,emitted[i][11]?p.nDist:0,emitted[i][11]?p.builds:0,emitted[i][11]?data[i].pTargetCoords.nX:0);}printf("]]");
 }printf("]\n");}
'''
 cpp=STAGE/'martial-lightning-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==3456
 def roll(low,hi=666):v=hi+0x6AC690C5*(low&0xffffffff);return v&0xffffffff,v>>32
 # Source arrays are the evidence, not regenerated trigonometry.
 body=blocks['sub_6FD14170'];offsets=[]
 for axis in ['x','y']:offsets.append([int(x) for x in re.search(axis+r'Offsets\[\]\s*=\s*\{([^}]+)',body,re.S)[1].split(',') if x.strip()])
 for count,rank,gates,low,alloc,endLow,endHigh,freed,emitted in rows:
  release=(gates&31)==31;active=release and rank>0;nova=active and count>=2;bolt=active and count==3 and bool(gates&32);expected=[]
  owner=roll(low) if bolt else (low&0xffffffff,666);assert(endLow,endHigh,freed)==(*owner,int(release))
  if nova:
   for i in range(64):expected.append([10,20,offsets[0][i],offsets[1][i],559,269,rank,7,24,0,0])
  if bolt:
   localLow,localHi=roll(low)
   localLow,localHi=localLow,666 # callback37 passes only the owner's new low seed into the private seed initializer
   for i in range(0,64,4):
    localLow,localHi=roll(localLow,localHi);signed=localLow if localLow<2**31 else localLow-2**32
    expected.append([10,20,offsets[0][i],offsets[1][i],558,269,rank,3,0,signed,1])
  assert len(emitted)==len(expected)
  for i,(actual,want) in enumerate(zip(emitted,expected)):
   ok=not alloc or i%2==0;assert actual[:12]==want+[int(ok)],(count,rank,gates,low,actual,want)
   tail=([want[9]&0xffffffff,666,77,77,6,77,1,7] if want[10] else [0,0,13,13,0,0,0,0]) if ok else [0]*8
   assert actual[12:]==tail,actual
 fixture=STAGE/'martial-lightning-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.clawsofthunder',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},reusedComposition='tools/reference/martial-fire-release-source-evidence.json',fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete progressive dispatcher temporarily sets count2 for the stacked nova callback before restoring count3 for the bolt callback. Rank0 skips emission but source list removal still occurs after a valid release.','Nova emits64directions from the target with velocity24,flags7,no private seed initializer. Bolts emit16directions(step4) from the target with flags3 and a distinct charged initializer.','Complete seed initializer/LCG/item wrapper establish unchanged owner seed for nova and exactly one owner draw for the bolt trigger; each of16bolts consumes one private child-seed draw even if allocation fails.','Complete charged initializer caps98frames to77,sets both total/current frames,initializes low seed with high666,sets charged path and clamps distance77 before invoking declared path construction. After factory returns,the full target-X setter writes Royal Strike Param2+1=7; null failed allocations are safe.'],services=['Real current skill constants and separate Royal Strike Param2=6 are supplied. Generic formula/stat-list/target coordinates/factory allocation reuse bounded existing services; current damage and native life/procs are not claimed.','Missile factory supplies current nova13/bolt98table lifetimes and preserves callback arguments; allocationKind1fails alternate attempts. Actual factory damage/stat/owner/event creation is not compiled.','Path type and frame accessors are declared services. Complete distance setter is compiled, but D2Common10142 only records its call; final charged path and frame-by-frame collision remain separate existing evidence/native adapters.','The existing limited RNG stub in unused inherited Fists callbacks is not exercised. All selected owner/child draws use the complete original LCG and item wrapper.'],modernNextHit=dict(source='https://news.blizzard.com/en-us/article/23938388/diablo-ii-resurrected-ladder-season-4-has-concluded',rule='Released2.7 notes give each martial charge-up skill trigger a distinct cast for missile hit-delay purposes. Thunder nova/bolt children of that trigger share its four-frame delay; separate trigger casts do not suppress one another.',inference='Grouping nova and bolt children by their Thunder trigger follows the source stacked callback composition and official per-trigger rule; exact pinned executable acceptance remains open.'),limits=['Source preparation only; no learned Thunder handler or damaging native missile is enabled by this evidence.','Old source full-list charge removal is a control,not modern consume-one policy. Native ordinary Attack already has a separate explicitly scoped current policy.','No full current executable/animation/stat/packet/event/property/terrain/slow/NextHit/client certificate. Effects/icons remain separate.'])
 out=ROOT/'tools/reference/martial-lightning-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Martial lightning evidence stale'
 else:out.write_text(encoded)
 print('Thunder:3456 complete progressive/ring/LCG/child-init/distance/target-setter cases pass; native missile lifecycle remains separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
