#!/usr/bin/env python3
"""Execute unchanged nonplayer leech block and victim-mana tail from ExecuteEvents."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 rel='source/D2Game/src/UNIT/SUnitDmg.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 source=raw.decode();event=source.index('void __fastcall SUNITDMG_ExecuteEvents(')
 def block(start):
  end=source.index('{',start)+1;depth=1
  while depth:depth+=(source[end]=='{')-(source[end]=='}');end+=1
  return source[start:end]
 token='\t\t\t\tconst int32_t nMana = STATLIST_UnitGetStatValue(pDefender, STAT_MANA, 0);';start=source.index(token,event);opening=source.rfind('{',event,start);npc=block(opening)
 tail=block(source.index('if (pDamage->dwManaLeech > 0)',event));capstart=source.index('\tconst int32_t nHp =',event);cap=source[capstart:source.index('\n\n',capstart)]
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
enum {STAT_MANA,STAT_STAMINA,STAT_HITPOINTS};
struct Unit{int hp=1000,maxHp=100000,mana=1337,stamina=0;};
struct Damage{int dwPhysDamage=0,dwLifeLeech=0,dwManaLeech=0,dwStamLeech=0;};
int STATLIST_UnitGetStatValue(Unit* u,int s,int){return s==STAT_HITPOINTS?u->hp:s==STAT_MANA?u->mana:u->stamina;}
int STATLIST_GetMaxLifeFromUnit(Unit* u){return u->maxHp;}
void STATLIST_AddUnitStat(Unit* u,int s,int value,int){assert(s==STAT_HITPOINTS);u->hp+=value;}
void STATLIST_SetUnitStat(Unit* u,int s,int value,int){assert(s==STAT_MANA);u->mana=value;}
int MONSTERUNIQUE_CalculatePercentage(int v,int n,int d){return int(int64_t(v)*n/d);}
void UNITS_SetOverlay(Unit*,int overlay,int){assert(overlay==151);}
void apply(Unit* pAttacker,Unit* pDefender,Damage* pDamage,int nDrain){
'''+cap+r'''
if((pDamage->dwLifeLeech || pDamage->dwManaLeech) && nDrain>0){pDamage->dwLifeLeech<<=6;pDamage->dwManaLeech<<=6;
'''+npc+'\n}\n'+tail+r'''
}
int main(){bool first=true;printf("[");
for(int physical:{0,1,255,257,12864})for(int hp:{0,257,51200})for(int life:{0,1,7,513})for(int mana:{0,1,5,517})for(int victimMana:{0,1,255,256,1337})for(int drain:{0,50,75,100})for(int missing:{0,1,1000}){
 Unit owner,victim;owner.maxHp=1000+missing;victim.hp=hp;victim.mana=victimMana;Damage damage{physical,life,mana,0};apply(&owner,&victim,&damage,drain);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",physical,hp,victimMana,missing,life,mana,drain,owner.hp-1000,victim.mana);first=false;
}printf("]\n");}
'''
 p=STAGE/'nonplayer-leech-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'nonplayer-leech-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-2000:]
 rows=json.loads(result.stdout);assert len(rows)==14400
 for physical,hp,vm,missing,life,mana,drain,healed,remaining in rows:
  expected=min(missing,(min(life,min(physical,hp))+min(mana,vm))*drain//100) if drain>0 else 0
  left=vm-mana if mana>0 else vm
  if mana>0 and left<256:left=0
  assert (healed,remaining)==(expected,left)
 fixture=STAGE/'nonplayer-leech-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],source=dict(path=rel,sha256=hashlib.sha256(raw).hexdigest()),compiledUnchangedBlocks={n:hashlib.sha256(s.encode()).hexdigest() for n,s in [('cap',cap),('nonplayer',npc),('victimMana',tail)]},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(rows)),sanitizers='ASan/UBSan; zero runtime stderr required',established=['The ordinary non-hireling NPC branch removes the six-bit preparation shift before treating life/mana/stamina drain as flat FP256 amounts, capped to physical/victim resources. Their sum, after drain effectiveness, heals the NPC only up to its missing life. No player difficulty leech divisors or player mana recovery apply.','The victim-mana tail subtracts the original nonplayer mana drain field and applies the below256 cutoff only when that field is positive. Zero drain leaves sub-point mana unchanged.'],limits=['Bounded selected arithmetic blocks, not full ExecuteEvents. Owner/victim stat services, zero stamina drain, already-mitigated physical, NPC branch selection and positive drain guard are explicit.','The parent CalculateTotalDamage separately adds positive NPC life-drain to direct HP damage. That parent/table/mitigation route is reviewed source evidence and is not compiled by this arithmetic probe.','Modern executable acceptance of these legacy-derived NPC drain quirks remains uncertain. No original-player leech or item-event ownership is inferred.'])
 p=ROOT/'tools/reference/nonplayer-leech-engine-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Nonplayer leech:14400 unchanged arithmetic/resource/cap cases pass sanitizers.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
