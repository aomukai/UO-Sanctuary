#!/usr/bin/env python3
"""Compile complete older Dragon Tail start/do/area/allocation bodies with declared damage services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());blocks={};hashes={}
 groups={'source/D2Game/src/SKILLS/SkillAss.cpp':['SKILLS_SrvSt27_DragonTail','SKILLS_SrvDo050_DragonTail','sub_6FCF5DE0'],'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD10200'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_AllocCombat']}
 for rel,names in groups.items():
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode()
  for name in names:
   m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
   while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
   blocks[name]=text[m.start():end]
 cpp=r'''
#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <initializer_list>
#define __fastcall
struct D2DamageStrc{uint16_t wResultFlags=0;uint32_t dwHitFlags=0;int dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwMagDamage=0,dwColdDamage=0,dwPoisDamage=0,dwLifeLeech=0;};
struct D2CombatStrc;struct D2GameStrc{void* pMemoryPool=nullptr;};
struct D2UnitStrc{int dwUnitType=0,dwUnitId=1,x=10,y=20;D2CombatStrc* pCombat=nullptr;};
struct D2CombatStrc{D2GameStrc* pGame;int dwAttackerType,dwAttackerId,dwDefenderType,dwDefenderId;D2DamageStrc tDamage;D2CombatStrc* pNext;};
struct D2SkillsTxt{int dwParam[4]={50,20,6,-40},dwCalc[1]={1},dwAuraRangeCalc=2;uint8_t nSrcDam=0;};
struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,DAMAGERESULTFLAG_DODGE=4,DAMAGERESULTFLAG_AVOID=8,DAMAGERESULTFLAG_EVADE=16,DAMAGERESULTFLAG_WEAPONBLOCK=32,DAMAGERESULTFLAG_WILLDIE=64,DAMAGEHITFLAG_2=2,UNIT_PLAYER=0,UNIT_MONSTER=1,STAT_HITPOINTS=6,STAT_PROGRESSIVE_TOHIT=100,STAT_PASSIVE_FIRE_MASTERY=101;
D2GameStrc game;D2UnitStrc owner,target,areaTargets[2];D2SkillsTxt row;int record=1,hasTarget=1,successful=1,rawPhysical=256,resistance=0,mastery=0,killOwner=0,speed=0,allocations=0,fill=0,reduce=0,progressive=0,drain=0,area=0,delivered[2]={},observedAR=0,rankValue=1,flags=0,radius=0,source=0;bool dead=false;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){if(id!=270)std::abort();return record?&row:nullptr;}
void sub_6FD15470(D2UnitStrc*,int rate){speed=rate;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&target:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){if(stat==STAT_PROGRESSIVE_TOHIT)return 75;if(stat==STAT_PASSIVE_FIRE_MASTERY)return mastery;if(stat==STAT_HITPOINTS)return 100000000;std::abort();}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int rank){return 20+15*(rank-1);}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){observedAR=ar;return successful;}
void sub_6FCF7CE0(D2GameStrc*,D2DamageStrc* d,D2UnitStrc*,D2UnitStrc*,int,int){d->dwHitFlags=3;d->dwPhysDamage=rawPhysical;}
void SUNITDMG_FillDamageValues(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*,int,int){++fill;}
void SUNITDMG_CalculateTotalDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* d){++reduce;d->dwPhysDamage=int(int64_t(d->dwPhysDamage)*(100-resistance)/100);}
D2CombatStrc* alloc(){++allocations;return new D2CombatStrc;}
#define D2_ALLOC_STRC_POOL(pool,type) alloc()
D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc* p,D2UnitStrc*){return p->pCombat?&p->pCombat->tDamage:nullptr;}
void sub_6FCF77E0(D2GameStrc*,D2UnitStrc*,D2DamageStrc* d){++progressive;d->dwPhysDamage+=123456;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){++drain;dead=killOwner;owner.pCombat->tDamage.dwPhysDamage+=654321;}
int SUNIT_IsDead(D2UnitStrc*){return dead;}
int MONSTERUNIQUE_CalculatePercentage(int value,int percent,int divisor){return int(int64_t(value)*percent/divisor);}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int rank){return formula==1?50+20*(rank-1):6;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
void sub_6FD10140(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p,D2DamageStrc* d,int){int i=p==&areaTargets[0]?0:1;delivered[i]=d->dwFireDamage;if(d->wResultFlags!=9||d->dwPhysDamage!=0)std::abort();d->dwFireDamage=-1;}
using Callback=int(*)(D2AuraCallbackStrc*,D2UnitStrc*);
void sub_6FD0FE80(D2GameStrc* g,D2UnitStrc* p,int x,int y,int r,uint32_t filter,Callback callback,void* args,int noAura,const char*,int){++area;flags=filter;radius=r;if(x!=target.x||y!=target.y||noAura)std::abort();D2AuraCallbackStrc a{g,p,args};callback(&a,&areaTargets[0]);callback(&a,&areaTargets[1]);}
int32_t sub_6FD10200(D2GameStrc*,D2UnitStrc*,int,int,int,D2DamageStrc*,uint32_t);
'''
 for name in ['SUNITDMG_AllocCombat','sub_6FCF5DE0','sub_6FD10200','SKILLS_SrvSt27_DragonTail','SKILLS_SrvDo050_DragonTail']:cpp+='\n'+blocks[name]+'\n'
 cpp+=r'''
int main(){bool comma=false;printf("[");target.dwUnitType=UNIT_MONSTER;
for(int rank:{1,6,20,255})for(int raw:{0,1,256,4097})for(int res:{0,50,100})for(int fireMastery:{0,25})for(int rec:{0,1})for(int tgt:{0,1})for(int hit:{0,1})for(int killed:{0,1}){
 record=rec;hasTarget=tgt;successful=hit;rawPhysical=raw;resistance=res;mastery=fireMastery;killOwner=killed;speed=allocations=fill=reduce=progressive=drain=area=observedAR=flags=radius=source=0;delivered[0]=delivered[1]=0;dead=false;owner.pCombat=nullptr;
 int start=SKILLS_SrvSt27_DragonTail(&game,&owner,270,rank);int prepared=owner.pCombat?owner.pCombat->tDamage.dwPhysDamage:-1;int result=SKILLS_SrvDo050_DragonTail(&game,&owner,270,rank);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,raw,res,fireMastery,rec,tgt,hit,killed,start,result,speed,allocations,fill,reduce,prepared,progressive,drain,area,delivered[0],delivered[1],flags,observedAR);delete owner.pCombat;
}printf("]\n");}
'''
 path=STAGE/'dragon-tail-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True)
 rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==1536
 for rank,raw,res,mastery,record,target,hit,dead,start,result,speed,allocated,fill,reduced,physical,prog,drain,area,a,b,flags,ar in rows:
  accepted=bool(record and target and hit);burst=accepted and not dead;post=raw*(100-res)//100
  assert (start,result,speed,allocated,fill,reduced,physical,prog,drain,area,a,b,flags,ar)==(int(accepted),int(accepted),-40 if record else 0,int(accepted),0,int(accepted),post if accepted else -1,int(accepted),int(accepted),int(burst),post*(mastery+50+20*(rank-1))//100 if burst else 0,post*(mastery+50+20*(rank-1))//100 if burst else 0,0x8583 if burst else 0,75+20+15*(rank-1) if record and target else 0)
 fixture=STAGE/'dragon-tail-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.dragontail',sourceId=270,installed=False,handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(path.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Start27posts Param4=-40attack rate before target/hit validation,adds progressive AR to skill AR,and allocates a successful kick with source128fallback.','Complete allocation skips duplicate fill for kick flags3,invokes total-damage reduction before storing the packet. Do50copies that stored physical before progressive/durability work. Under the declared resistance service,a physically immune target seeds zero fire.','Do50surviving owner uses copied physical times (current fire mastery+current skill calc1)/100. Physical mutations by progressive/durability services do not alter that copy. Owner death after durability suppresses burst.','Complete area wrapper defaults to0x8583and the complete callback copies the fire-only flags9packet independently for each recipient.'],declaredServices=['Kick physical preparation is a supplied raw packet with flags3. Complete prepared kick composition and15boot bases are separately proved in dragon-kick-packet-source-evidence.json;not recomposed here.','Full SUNITDMG_AllocCombat is compiled;FillDamageValues is an unused counter for kick flags3. CalculateTotalDamage is a declared signed64bit physical-resistance service;this does not prove the full source mitigation/stat collector.','Progressive release and durability are call boundaries that mutate the packet to test capture order. Full item/proc/leech/health/charge consumption is not executed. Area enumeration dispatches two declared recipients;actual range/filter/block/dodge/damage delivery are separate.','Current Param8charged guaranteed-hit and per-finisher one-charge consumption are absent from this older start/do. Do not infer modern finisher semantics from this source. Payment,KK scheduling/-40attack-rate lifetime and native input remain unimplemented.'],reuse=['tools/reference/dragon-kick-native-evidence.json','tools/reference/dragon-kick-packet-source-evidence.json','tools/reference/phoenix-native-evidence.json'],limits=['Source preparation only;no native Dragon Tail handler,modern executable or graphical parity certificate.','Resolve current charged hit/block,one-charge consumption,full packet/mitigation/proc ordering,knockback and KK/action scheduling before learned integration.','Final kick/explosion/charge/audio effects and talent/left/right/quick icons remain separately inventoried placeholders.'])
 out=ROOT/'tools/reference/dragon-tail-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Dragon Tail evidence stale'
 else:out.write_text(encoded)
 print('Dragon Tail:1536 complete start/do/allocation/area/copy contexts pass with declared packet/mitigation/progressive/delivery services;native handler remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
