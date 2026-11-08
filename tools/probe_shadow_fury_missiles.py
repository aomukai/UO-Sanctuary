#!/usr/bin/env python3
"""New NPC Fury flag/event boundary; authenticate existing impact/hit proofs."""
import hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-missiles-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());retained={}
 # Authenticate inputs and full routines; do not rerun prior compiler matrices.
 hit=json.loads((ROOT/'tools/reference/assassin-trap-attack-engine-evidence.json').read_text());impact=json.loads((ROOT/'tools/reference/weapon-missile-impact-evidence.json').read_text())
 for rel,h in impact['sourceHashes'].items():assert sha(BASE/'d2moo-pinned-source'/rel)==h
 assert sha(ROOT/hit['fixture']['path'])==hit['fixture']['sha256']
 unit=(BASE/'d2moo-pinned-source'/hit['source']['path']).read_text();assert hashlib.sha256(function(unit,hit['compiledUnchangedRoutine']['name']).encode()).hexdigest()==hit['compiledUnchangedRoutine']['sha256']
 for name in ['assassin-trap-attack-engine-evidence','weapon-missile-impact-evidence','shadow-fury-packets-source-evidence']:
  p=ROOT/'tools/reference'/f'{name}.json';retained[str(p.relative_to(ROOT))]=sha(p)
 rel='source/D2Game/src/MISSILES/MissMode.cpp';path=BASE/'d2moo-pinned-source'/rel;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);body=function(raw.decode(),'MISSMODE_SetDamageFlags')
 cpp=r'''#include <cstdio>
#include <cstdint>
#include <cassert>
#define __fastcall
constexpr int UNIT_MISSILE=3,STATE_UNINTERRUPTABLE=1,MISSILESFLAGINDEX_GETHIT=0,MISSILESFLAGINDEX_SOFTHIT=1;
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,DAMAGERESULTFLAG_GETHIT=4,DAMAGERESULTFLAG_KNOCKBACK=8,DAMAGERESULTFLAG_BLOCK=16,DAMAGERESULTFLAG_AVOID=256,DAMAGERESULTFLAG_EVADE=512,DAMAGERESULTFLAG_SOFTHIT=16384,DAMAGEHITFLAG_32=32,DAMAGEHITFLAG_128=128,STAT_DAMAGE_FRAMERATE=0;
struct D2GameStrc{};struct D2UnitStrc{int dwUnitType=0,dwClassId=0;uint64_t pSeed=0;}owner;
struct D2DamageStrc{uint16_t wResultFlags=0;int dwPhysDamage=0,dwHitClass=0,nHitClassActiveSet=0,dwHitFlags=0,dwPiercePct=0;};
struct D2MissilesTxt{uint32_t dwMissileFlags=1;int nKnockBack=0;}row;
uint32_t gdwBitMasks[32];int defense=0,events=0,returns=0,crits=0,physicalArgument=-1;
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return &owner;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int id){assert(id==506);return &row;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
uint32_t ITEMS_RollRandomNumber(uint64_t*){assert(false);return 0;}
uint32_t SUNITDMG_ApplyBlockOrDodge(D2GameStrc*,D2UnitStrc* p,D2UnitStrc*,int avoid,bool physical){assert(p==&owner&&avoid==1);physicalArgument=physical;return defense;}
int MISSILE_GetHitClass(D2UnitStrc*){return 10;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc* p,D2UnitStrc*,D2DamageStrc*){assert(p==&owner);crits++;} // Own Shadow monster Crit=0.
int MISSILE_GetFlags(D2UnitStrc*){return 1;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int hit,D2DamageStrc*){assert(hit==1);events++;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){returns++;}
'''+body+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;for(int block=0;block<32;block++)for(int crit:{0,1})for(int phys:{0,256}){defense=block;events=returns=crits=0;D2GameStrc g;D2UnitStrc m,t;m.dwUnitType=UNIT_MISSILE;m.dwClassId=506;D2DamageStrc d;d.wResultFlags=crit?8192:0;d.dwPhysDamage=phys;MISSMODE_SetDamageFlags(&g,&m,&t,&d);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",block,crit,phys,d.wResultFlags,d.dwHitClass,d.dwHitFlags,events,returns,crits,physicalArgument);first=false;}printf("]\n");}
'''
 cpp=cpp.replace('#include <cstdio>','#include <cstdio>\n#include <initializer_list>');generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 r=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(r.stderr);assert r.returncode==0 and not r.stderr;cases=json.loads(r.stdout);assert len(cases)==128
 casepath=STAGE/'cases.json';casepath.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=128,completeRoutine={'MISSMODE_SetDamageFlags':hashlib.sha256(body.encode()).hexdigest()},source={str(path.relative_to(ROOT)):sha(path)},retainedProof=retained,retainedCases={'npcAccuracy':6804,'impactReader':5184,'ownCreator':16},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [generated,binary,casepath,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]},scope='Complete unchanged flag/event/return routine,all32defense masks,physical eligibility,creation critical bit. Declared services: bladefragment1 GetHit/HitClass/flags and own intrinsic monster Crit0,block mask,event/return counters,interruptible mapped NPC. Reuses existing source-qualified NPC hit and impact reader;no prior compiler matrices rerun. Prepared critical/child seed supplied;current executable equivalence,full NPC channel/AI and caster-removal lifetime not claimed.')
 (ROOT/'tools/reference/shadow-fury-missiles-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Own Fury missiles:128 complete source flag/event/return contexts;ASan/UBSan clean;prior hit/impact source proofs authenticated without recompiling.')
if __name__=='__main__':main()
