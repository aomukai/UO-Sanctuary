#!/usr/bin/env python3
"""Complete legacy kick physical packet composition; explicitly bounded fill/charge services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_dragon_kick_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/SKILLS/SkillAss.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();name='sub_6FCF7CE0';m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 body=text[m.start():end]
 cpp=(STAGE/'dragon-kick-damage-source-probe.cpp').read_text().split('int main(){',1)[0]
 cpp='#include <algorithm>\n'+cpp
 cpp=cpp.replace('struct D2UnitStrc{','struct D2SeedStrc{};struct D2UnitStrc{D2SeedStrc pSeed;')
 cpp+=r'''
constexpr int BODYLOC_LARM=5;
struct D2GameStrc{};struct D2DamageStrc{int dwEnDmgPct=0,dwPhysDamage=0,dwHitFlags=0,dwLifeLeech=0,dwManaLeech=0;};
int tigerDamage=0,draw=0,spanObserved=0,drawCalls=0,fillCalls=0,elementCalls=0;D2GameStrc game;D2UnitStrc target;
void sub_6FCF5680(D2UnitStrc*,D2DamageStrc* d){d->dwEnDmgPct+=tigerDamage;}
int MONSTERUNIQUE_CalculatePercentage(int value,int percent,int divisor){return int(int64_t(value)*percent/divisor);}
int ITEMS_RollLimitedRandomNumber(D2SeedStrc*,int span){++drawCalls;spanObserved=span;return span>0?std::min(draw,span-1):0;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){++elementCalls;}
void SUNITDMG_FillDamageValues(D2GameStrc*,D2UnitStrc* p,D2UnitStrc*,D2DamageStrc* d,int mode,uint8_t source){if(mode!=1||source!=128||!(d->dwHitFlags&1))std::abort();++fillCalls;sub_6FC7C900(p,mode);}
void sub_6FCF5870(D2UnitStrc*,D2DamageStrc* d){d->dwLifeLeech+=40;d->dwManaLeech+=40;}
'''
 cpp+='\n'+body+r'''
int main(){first.id=1;second.id=2;boot.dwUnitType=UNIT_ITEM;bool comma=false;printf("[");
for(int rank:{1,6,20})for(int tiger:{0,100,360})for(int s:{0,150})for(int d:{0,100})for(int bi=-1;bi<15;++bi)for(int flat:{0,7})for(int pen:{-100,0,99})for(int roll:{0,255,100000})for(int mask:{0,1,3})for(int inventory:{0,1}){
 owner.dwUnitType=UNIT_PLAYER;owner.dwClassId=PCLASS_ASSASSIN;owner.pInventory=inventory?(void*)1:nullptr;strength=s;dexterity=d;bootIndex=bi;boot.dwClassId=bi;kick=flat;penalty=pen;hands=owned=active=inventory?mask:0;primary=1;mergeCalls=trace=0;tigerDamage=tiger;draw=roll;drawCalls=fillCalls=elementCalls=0;spanObserved=0;D2DamageStrc damage;damage.dwEnDmgPct=5+7*(rank-1);sub_6FCF7CE0(&game,&damage,&owner,&target,255,rank);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,tiger,s,d,bi,flat,pen,roll,mask,inventory,damage.dwPhysDamage,spanObserved,damage.dwEnDmgPct,damage.dwHitFlags,active,fillCalls,damage.dwLifeLeech,damage.dwManaLeech);
}printf("]\n");}
'''
 path=STAGE/'dragon-kick-packet-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==62208
 armor=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/armor.json').read_bytes());boots=[v for v in armor.values() if v.get('type')=='boot']
 def percent(value,pct):return abs(value*pct)//100*(1 if value*pct>=0 else -1)
 for rank,tiger,s,d,bi,flat,pen,roll,mask,inventory,physical,span,enhanced,flags,active,fill,life,mana in data:
  h=mask if inventory else 0;sv=s+(11 if h&1 else 0)+(17 if h&2 else 0);dv=d+(3 if h&1 else 0)+(5 if h&2 else 0);bodyStat=max(1,sv+dv-20);ed=5+7*(rank-1)+tiger
  lo=bodyStat//4*256;hi=bodyStat//3*256;lo+=percent(lo,ed);hi+=percent(hi,ed)
  bootMin=bootMax=flat;pct=ed
  if inventory and bi>=0:
   b=boots[bi];bootMin+=flat+b.get('mindam',0);bootMax=max(bootMin,bootMax+flat+b.get('maxdam',0));pct+=23+max(-90,b.get('StrBonus',0)*sv//100+b.get('DexBonus',0)*dv//100+pen)
  lo+=bootMin*256+percent(bootMin*256,pct);hi+=bootMax*256+percent(bootMax*256,pct)
  expected=lo+(min(roll,hi-lo-1) if hi>lo else 0)
  assert(physical,span,enhanced,flags,active,fill,life,mana)==(expected,hi-lo,ed,3,h&1,int(bool(inventory)),40 if inventory else 0,40 if inventory else 0)
 fixture=STAGE/'dragon-kick-packet-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines={name:hashlib.sha256(body.encode()).hexdigest()},reusedCompleteRoutines=['SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage','SKILLS_CalculateKickDamage','UNITS_CanDualWield','sub_6FC7C7B0','sub_6FC7C900'],reuseEvidence=['tools/reference/dragon-kick-damage-source-evidence.json','tools/reference/dragon-talon-hands-source-evidence.json'],fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),sanitizers='ASan+UBSan'),declaredServices=['Tiger ED is supplied by a packet additive callback; complete Tiger consumer is separately proved. Cobra callback supplies40life/40mana to observe placement only; no charge delivery/consumption proof here.','FillDamageValues is an explicit call boundary: verifies flags3, a5=1 and source128; invokes complete source hand-context wrapper. Does not compile full elemental/leech/critical/proc collector.','Kick skill elemental callback is a counter/no-op for the declared zero-elemental Dragon Talon row. Integer percentage uses declared signed64-bit multiply/truncation; bounded inputs do not overflow source endpoints. Limited RNG returns min(draw,span-1), or0for nonpositive span.'],established=['Complete packet body composes separately enhanced body and boot endpoints, rolls one exclusive-end physical packet, sets flags3, invokes skill element hook and only with inventory proceeds to fill and Cobra callback.','No-inventory control retains initial flat-kick contribution and skips boot/fill/Cobra; full per-item contexts are separate.','Both damage-stat lists are disabled for boot arithmetic; primary is restored before/within fill. Final active primary is retained, with secondary disabled.'],limits=['Native prepared component/handler/timing/input/modern charge-consumption/block semantics remain pending. This proves bounded physical composition with explicit downstream services.','Current flat-kick double-add behavior and integer/RNG/executable equivalence require corroboration. Full typed damage, weapon leech and item event acceptance remain separate.'])
 out=ROOT/'tools/reference/dragon-kick-packet-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Dragon kick packet evidence stale'
 else:out.write_text(encoded)
 print('Dragon kick packet:62208 complete physical-composition cases pass with declared fill/charge services; native integration pending.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
