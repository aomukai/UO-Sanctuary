#!/usr/bin/env python3
"""Compile unchanged player leech arithmetic, caps and victim-mana tail."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
source='source/D2Game/src/UNIT/SUnitDmg.cpp';raw=(STAGE/'d2moo-pinned-source'/source).read_bytes()
assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==source)
text=raw.decode();blocks={}
def block(token,after=0):
 start=text.index(token,after);end=text.index('{',start)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 result=text[start:end];blocks[token]=result;return result
life=block('int32_t __fastcall SUNITDMG_AddLeechedLife(');mana=block('int32_t __fastcall SUNITDMG_AddLeechedMana(')
event=text.index('void __fastcall SUNITDMG_ExecuteEvents(')
cap_start=text.index('\tconst int32_t nHp =',event);cap_end=text.index('\n\n',cap_start);cap=text[cap_start:cap_end]
shift='pDamage->dwLifeLeech <<= 6;\n'+text[text.index('\t\t\tpDamage->dwManaLeech <<= 6;',event):text.index('\n\n',text.index('\t\t\tpDamage->dwManaLeech <<= 6;',event))]
assert 'pDamage->dwLifeLeech <<= 6;' in text
divs=block('if (pDifficultyLevelsTxtRecord->dwLifeStealDiv)',event)+'\n'+block('if (pDifficultyLevelsTxtRecord->dwManaStealDiv)',event)
healing=block('if (!nUnitType)',event);tail=block('if (pDamage->dwManaLeech > 0)',event)
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
enum{STAT_HITPOINTS,STAT_MANA,STATE_DEATH_DELAY};
struct D2UnitStrc{int hp=0,mana=0,maxHp=0,maxMana=0,pSeed=0;bool dead=false,delay=false;};
struct D2DamageStrc{int dwPhysDamage,dwLifeLeech,dwManaLeech;};
struct Difficulty{int dwLifeStealDiv,dwManaStealDiv;};
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_HITPOINTS?p->hp:p->mana;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){if(stat==STAT_HITPOINTS)p->hp=value;else p->mana=value;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* p){return p->maxHp;}
int STATLIST_GetMaxManaFromUnit(D2UnitStrc* p){return p->maxMana;}
bool SUNIT_IsDead(D2UnitStrc* p){return p->dead;}
bool STATES_CheckState(D2UnitStrc* p,int){return p->delay;}
int MONSTERUNIQUE_CalculatePercentage(int v,int n,int d){return int(int64_t(v)*n/d);}
int ITEMS_RollLimitedRandomNumber(int*,int){return 0;}
void UNITS_SetOverlay(D2UnitStrc*,int,int){}
'''+life+'\n'+mana+r'''
void apply(D2UnitStrc* pAttacker,D2UnitStrc* pDefender,D2DamageStrc* pDamage,int nDrain,int ld,int md){
 Difficulty difficulty{ld,md};auto* pDifficultyLevelsTxtRecord=&difficulty;int nUnitType=0;
'''+cap+r'''
 if((pDamage->dwLifeLeech || pDamage->dwManaLeech) && nDrain>0){
'''+shift+'\n'+divs+'\n'+healing+'\n}\n'+tail+r'''
}
int main(){bool first=true;std::printf("[");
for(int physical:{0,1,255,257,12864})for(int hp:{0,257,51200})for(int life:{0,1,7,33})for(int mana:{0,1,5,17})for(int drain:{0,50,75,100})for(int pair=0;pair<8;pair++){
 int pairs[8][2]={{0,0},{1,1},{2,2},{3,3},{1,3},{3,1},{0,3},{3,0}};int ld=pairs[pair][0],md=pairs[pair][1];
 D2UnitStrc owner{1000,777,100000,100000},victim{hp,1337,100000,100000};D2DamageStrc damage{physical,life,mana};
 apply(&owner,&victim,&damage,drain,ld,md);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",physical,hp,life,mana,drain,ld,md,owner.hp-1000,owner.mana-777,victim.mana);
}std::printf("]\n");}
'''
path=STAGE/'weapon-leech-source-probe.cpp';path.write_text(cpp);binary=STAGE/'weapon-leech-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==7680
for physical,hp,life,mana,drain,ld,md,hl,hm,remaining in data:
    physical=min(physical,hp);ls,ms=life,mana
    if (life or mana) and drain>0:ls,ms=life*64//max(1,ld),mana*64//max(1,md)
    recover=lambda p:((physical*p//100)*drain//100)//64 if physical>0 and drain>0 else 0
    expected_mana=1337-ms if ms>0 else 1337
    if ms>0 and expected_mana<256:expected_mana=0
    assert (hl,hm,remaining)==(recover(ls),recover(ms),expected_mana)
fixture=STAGE/'weapon-leech-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
pin=ROOT/'tools/reference/weapon-leech-difficulty-source.json';difficulty_raw=pin.read_bytes()
assert hashlib.sha1(b'blob '+str(len(difficulty_raw)).encode()+b'\0'+difficulty_raw).hexdigest()=='1bf58cf8273d40a0e79db35a105141e4bf0ddf10'
diff=json.loads(difficulty_raw);divisors={k:{f:diff[k][f] for f in ['LifeStealDivisor','ManaStealDivisor']} for k in ['Normal','Nightmare','Hell']}
assert list(divisors.values())==[dict(LifeStealDivisor=x,ManaStealDivisor=x) for x in [1,2,3]]
e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],source=dict(path=source,sha256=hashlib.sha256(raw).hexdigest()),
 probe=dict(cases=len(data),dataPath=str(fixture.relative_to(ROOT)),dataSha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),compiledBlocks={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},capBlock=cap,shiftBlock=shift),
 difficultySource=dict(path=str(pin.relative_to(ROOT)),url='https://raw.githubusercontent.com/blizzhackers/d2data/fa1286949266498b294c40c2942521a7c0d7207a/json/difficultylevels.json',gitBlob='1bf58cf8273d40a0e79db35a105141e4bf0ddf10',license='MIT; see tools/reference/d2r-named-equipment/LICENSE.txt',sha256=hashlib.sha256(difficulty_raw).hexdigest(),divisors=divisors),
 established=['Positive physical is capped to current victim FP256 life. Life/mana percent is shifted6, then divided by its respective nonzero difficulty divisor; physical percentage, drain effectiveness and divide64 each retain integer truncation.',
  'Compiled original owner-heal functions cap fractional life/mana independently. The harness supplies live player actors with ample missing resources; native resource-cap/death checks are separate.',
  'Literal older event tail also subtracts the mutated mana-leech field from victim mana, with a below256 cutoff. It runs even when physical is0; drain0 retains the unshifted field. This unusual behavior is implemented as explicitly legacy-derived, not silently replaced by an invented proportional mana burn.',
  'Reviewed parent event places leech outside central-only attacker DOMISSILEDAMAGE suppression. Current player damage aggregation does not add life-leech percent to total HP damage.'],
 services=['Owner/victim stat values, player branch, drain and difficulty are explicit services. Physical is already mitigated. Combat/absorb/item callbacks, full source PRNG/overlay draw and complete allocation/health dispatch are not compiled as an engine.',
  'Six-bit shift and percentage operations are tested only with bounded nonnegative inputs. Native wide intermediates prevent unrelated overflow outside this proof.'],
 limits=['No modern executable certificate; particularly the literal victim-mana side effect, full event/death/corpse/overlay/RNG order and all owner/type branches require current comparison.',
  'Sanctuary projects difficulty from the existing victim spawn identity; D2 uses global game difficulty. Mixed-world/global context equivalence is not established.',
  'Nonplayer/hireling leech, Life Tap callbacks, central item events and older live-gear/integer-loss GemCombat.Leech remain separate integration work.'])
mon_path=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';monsters=json.loads(mon_path.read_text());fields=['Drain','Drain(N)','Drain(H)']
examples={k:[monsters[k].get(f,0) for f in fields] for k in ['zombie1','skeleton1']}
assert examples=={'zombie1':[100,75,50],'skeleton1':[0,0,0]}
e['pinnedDrainExamples']=dict(path=str(mon_path.relative_to(ROOT)),sha256=hashlib.sha256(mon_path.read_bytes()).hexdigest(),fields=fields,rows=examples)
(ROOT/'tools/reference/weapon-leech-engine-evidence.json').write_text(json.dumps(e,indent=2)+'\n')
print('Weapon leech:7680 unchanged player arithmetic/health-cap/victim-mana cases and pinned difficulty divisors verified.')
