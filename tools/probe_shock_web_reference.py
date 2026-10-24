#!/usr/bin/env python3
"""Execute complete Shock Web dispatch/scatter/progressive getters and source seed helpers."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  b=text[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 raw=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';pin=json.loads((raw.parent.parent/'sources.json').read_text())['files']['json/skills.json']['sha256'];assert hashlib.sha256(raw.read_bytes()).hexdigest()==pin;skill=json.loads(raw.read_text())['256'];assert skill['srvdofunc']==43 and skill['srvmissilea']=='shock field in air' and not skill.get('progressive') and skill['prgcalc1']=="par1+lvl/par2+skill('Fire Trauma'.blvl)/3" and skill['aurarangecalc']=="(par1+lvl/par2+skill('Fire Trauma'.blvl)/3)/4+1" and (skill['Param1'],skill['Param2'])==(6,4)
 ass=source('source/D2Game/src/SKILLS/SkillAss.cpp');seed=source('source/D2Common/include/D2Seed.h');seedcpp=source('source/D2Common/src/D2Seed.cpp');path=source('source/D2Common/src/Path/Path.cpp')
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <array>
#define __fastcall
#define __stdcall
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2UnitStrc{D2SeedStrc pSeed;int x=100,y=200;}owner;
struct D2GameStrc{}game;struct D2StatListStrc{};
struct D2SkillsTxt{unsigned dwFlags[2]={0,0};int nAuraState=-1,wAuraStat[1]={-1},wSrvMissile=-1,wSrvMissileA=388,wSrvMissileB=-1,wSrvMissileC=-1,dwPrgCalc[3]={1,0,0},dwAuraRangeCalc=2;}row;
struct D2MissileStrc{int nSkill=0,nSkillLevel=0,nMissile=0,nTargetX=0,nTargetY=0;unsigned dwFlags=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};
struct Tables{int nStatesTxtRecordCount=200,nItemStatCostTxtRecordCount=400;}tables;Tables* sgptDataTables=&tables;unsigned gdwBitMasks[32];constexpr int SKILLSFLAGINDEX_PROGRESSIVE=0;
bool record=true,target=true,factory=true;int tx=108,ty=200,hard=0,draws=0;std::vector<std::array<int,2>> aims;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){assert(false);return nullptr;}int STATLIST_GetStatValue(D2StatListStrc*,int,int){assert(false);return 0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int rank){assert(id==256);int count=6+rank/4+hard/3;return formula==1?count:count/4+1;}
bool D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=tx;*y=ty;return target;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* m){assert(m->dwFlags==0x420 && m->nSkill==256 && m->nMissile==388 && m->pOwner==&owner && m->pOrigin==&owner && m->nSkillLevel>0);aims.push_back({m->nTargetX,m->nTargetY});return factory?&owner:nullptr;}
'''
 cpp+=routine(seedcpp,'SEED_InitLowSeed')+routine(seed,'SEED_RollRandomNumber').replace('0x6AC690C5i64','0x6AC690C5LL')+routine(seed,'SEED_RollLimitedRandomNumber')
 cpp+='uint32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc* p,int n){draws++;return SEED_RollLimitedRandomNumber(p,n);}\n'
 for text,name in [(path,'PATH_ComputeSquaredDistance'),(ass,'SKILLS_GetProgressiveSkillMissileId'),(ass,'SKILLS_EvaluateProgressiveSkillCalc'),(ass,'sub_6FCF8330'),(ass,'SKILLS_SrvDo043_ShockField')]:cpp+=routine(text,name)
 cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int exists:{0,1})for(int hasTarget:{0,1})for(int missile:{-1,388})for(int rank:{1,3,4,20,21,40,255})for(int points:{0,1,2,3,20})for(auto cursor:{std::array<int,2>{100,200},{101,200},{108,200},{108,208}})for(uint32_t high:{0u,666u,0xffffffffu})for(int make:{0,1}){
 owner={};owner.pSeed={.nLowSeed=0xdeadbeef,.nHighSeed=high};record=exists;target=hasTarget;factory=make;row={};row.wSrvMissileA=missile;tx=cursor[0];ty=cursor[1];hard=points;draws=0;aims.clear();int result=SKILLS_SrvDo043_ShockField(&game,&owner,256,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%u,%u,[",first?"":",",exists,hasTarget,missile,rank,points,tx,ty,high,make,result,draws,owner.pSeed.nLowSeed,owner.pSeed.nHighSeed);first=false;
 for(size_t i=0;i<aims.size();i++)printf("%s[%d,%d]",i?",":"",aims[i][0],aims[i][1]);printf("]]");}printf("]\n");}
'''
 p=STAGE/'shock-web-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==6720
 for exists,target,missile,rank,hard,tx,ty,high,make,result,draws,low_after,high_after,aims in data:
  ok=bool(exists and target and missile>=0);expected=[];low=tx if ok else 0xdeadbeef;hi=666 if ok else high;n=6+rank//4+hard//3;radius=n//4+1
  if ok:
   for _ in range(n):
    state=(hi+0x6AC690C5*low)&((1<<64)-1);low=state&0xffffffff;hi=state>>32;x=tx+low%(2*radius)-radius
    state=(hi+0x6AC690C5*low)&((1<<64)-1);low=state&0xffffffff;hi=state>>32;y=ty+low%(2*radius)-radius
    if (x-100)**2+(y-200)**2>=4:expected.append([x,y])
  assert(result,draws,low_after,high_after,aims)==(int(ok),2*n if ok else 0,low,hi,expected)
 out=STAGE/'shock-web-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.shockweb',handlerEnabled=False,legacyCommit=tree['sha'],pinnedSkillHash=pin,sourceHashes=hashes,compiledUnchangedRoutineHashes=blocks,portability='Only the MSVC integer literal suffix0x6AC690C5i64 is normalized toLL for g++; routine source hashes retain original text.',fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(data)),established=['Complete do43 and both progressive getters select srvmissileA/prgcalc1 because Shock Field has no progressive flag. Seven ranks and five hard Fire Blast counts feed declared count/radius formula services; no native handler is enabled.','Complete scatter invokes full SEED_InitLowSeed: despite its name it resets low=targetX AND high=666. Initial supplied high0/666/UINT_MAX has no effect. Complete seed recurrence and limited draw execute; independent Python integer recurrence compares every emitted coordinate, final seed and exact two-draw-per-attempt count.','Each of6+rank/4+hardFireBlast/3 attempts chooses square offsets[-range,range-1], skipping squared caster distance<4 without retry. Failure of the declared missile factory does not alter callback success or retries. Target lookup/invalid row/missile guards preserve seed and emit nothing.','Flags0x420/owner/origin/rank/skill256/missile388 are checked on every supplied factory call. Later full factory/path/landed-field behavior, original nested world seed/stat services and modern equivalence remain separate.'],services=['Formula callback supplies the independent pinned count and radius expressions; table/state lookups isolate actual nonprogressive fields. Unsupported progressive charge-state branches deliberately assert if entered.','Target coordinates, declared nonmutating missile allocator and minimal unit metadata are services. Complete selected routines/seed/distance bodies execute; this does not compile whole world allocation, original caster RNG consumers or current client/NextHit/field behavior.'])
 p=ROOT/'tools/reference/shock-web-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Shock Web:6720 complete do43/progressive/scatter/seed/distance cases pass independent coordinate/seed/guard assertions; field/native delivery pending.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
