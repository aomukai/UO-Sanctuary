#!/usr/bin/env python3
"""Compile pinned impact-time weapon-missile rolls without replacing their arithmetic."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={};blocks={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();blob=next(v['sha'] for v in tree['tree'] if v['path']==path)
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 blocks[name]=text[m.start():end];return blocks[name]+'\n'
text=source('source/D2Game/src/MISSILES/MissMode.cpp');roll=routine(text,'MISSMODE_RollDamageValue');fill=routine(text,'MISSMODE_FillDamageParams')
stats=sorted(set(re.findall(r'\bSTAT_\w+',fill+roll)));fields=sorted(set(re.findall(r'pDamage->(\w+)',fill)))
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <iterator>
#include <initializer_list>
#define __fastcall
#define __stdcall
constexpr int UNIT_MONSTER=1,DAMAGERESULTFLAG_CRITICALSTRIKE=2,DAMAGEHITFLAG_BYPASSUNDEAD=4,DAMAGEHITFLAG_BYPASSDEMON=8,DAMAGEHITFLAG_BYPASSBEASTS=16;
struct D2UnitStrc{int dwUnitType=0,dwClassId=0,pSeed=0,kind=0,stats[64]={};} missile,target;
struct D2MonStatsTxt{int wMonType=0;} mon;
struct D2StatStrc{int nLayer=0,nValue=0;};
struct Tables{int nMonTypeTxtRecordCount=1,nMonTypeIndex=1,pMonTypeNest[1]={0};} tables;
auto* sgptDataTables=&tables;unsigned gdwBitMasks[32]={};
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
int ITEMS_RollLimitedRandomNumber(int* seed,int limit){return limit<=0?0:std::clamp(*seed,0,limit-1);}
int MONSTERUNIQUE_CalculatePercentage(int value,int n,int d){return int(int64_t(value)*n/d);}
bool MONSTERS_IsDemon(D2UnitStrc* p){return p->kind==2;}
bool MONSTERS_IsUndead(D2UnitStrc* p){return p->kind==3;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
int STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,int){return 0;}
'''
cpp+='enum Stats{'+','.join(stats)+'};\nstruct D2DamageStrc{'+''.join('int '+n+';' for n in fields)+'};\n'+roll+fill
cpp+=r'''int main(){std::printf("[");bool first=true;
for(int lo:{0,257,1024})for(int hi:{0,513,1024})for(int draw:{0,1,511})for(int percent:{-200,0,37,200})for(int kind:{0,1,2,3})for(int critical:{0,1})for(int mastery:{0,25})for(int count:{0,1,2}){
missile={};target={};missile.pSeed=draw;target.dwUnitType=UNIT_MONSTER;target.kind=kind;
'''
for name in ['MINDAMAGE','FIREMINDAM','LIGHTMINDAM','MAGICMINDAM','COLDMINDAM','POISONMINDAM','BURNINGMIN']:cpp+=f'missile.stats[STAT_{name}]=lo;\n'
for name in ['MAXDAMAGE','FIREMAXDAM','LIGHTMAXDAM','MAGICMAXDAM','COLDMAXDAM','POISONMAXDAM','BURNINGMAX']:cpp+=f'missile.stats[STAT_{name}]=hi;\n'
for name in ['FIRE','LTNG','MAG','COLD','POIS']:cpp+=f'missile.stats[STAT_PASSIVE_{name}_MASTERY]=mastery;\n'
cpp+=r'''missile.stats[STAT_DAMAGEPERCENT]=percent;missile.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=31;missile.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=47;
missile.stats[STAT_ITEM_DEADLYSTRIKE]=critical;missile.stats[STAT_COLDLENGTH]=13;missile.stats[STAT_POISONLENGTH]=101;missile.stats[STAT_POISON_COUNT]=count;
D2DamageStrc damage;MISSMODE_FillDamageParams(&missile,kind?&target:nullptr,&damage);
if(!first)std::printf(",");first=false;
std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",lo,hi,draw,percent,kind,critical,mastery,count,damage.dwPhysDamage,damage.dwFireDamage,damage.dwMagDamage,damage.dwLtngDamage,damage.dwColdDamage,damage.dwPoisDamage,damage.dwPoisLen,damage.wResultFlags);
}std::printf("]\n");}
'''
path=STAGE/'weapon-missile-impact-probe.cpp';path.write_text(cpp);binary=STAGE/'weapon-missile-impact-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
def trunc(n,d):return abs(n)//d*(1 if n>=0 else -1)
def rolled(lo,hi,draw,mastery=0):
 if hi<=0 or lo<=0:return 0
 lo,hi=sorted((lo,hi));lo+=lo*mastery//100;hi+=hi*mastery//100
 return lo+(min(draw,hi-lo-1) if hi>lo else 0)
for lo,hi,draw,pct,kind,critical,mastery,count,physical,fire,magic,lightning,cold,poison,length,flags in data:
 p=rolled(lo,hi,draw);bonus=max(-90,pct+(31 if kind==2 else 47 if kind==3 else 0))
 if kind:p+=trunc(p*bonus,100)
 p*=1+critical;e=rolled(lo,hi,draw,mastery)
 assert (physical,fire,magic,lightning,cold,poison,length,flags)==(p,e,e,e,e,e,101//count if count>1 else 101,2*critical)
(STAGE/'weapon-missile-impact-probe.json').write_text(json.dumps(data,separators=(',',':'))+'\n')
compiled=list(blocks)
routine(text,'MISSMODE_SetDamageFlags')
damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');routine(damage,'SUNITDMG_IsHitSuccessful')
creation=source('source/D2Game/src/MISSILES/Missiles.cpp');routine(creation,'MISSILES_CreateMissileFromParams')
common=source('source/D2Common/src/Units/Missile.cpp');routine(common,'MISSILE_CalculateDamageData')
assert 'MISSILE_HasBonusStats(pOwner, pItem)' in blocks['MISSILE_CalculateDamageData']
# Retain the collision-time owner/stat and pierce gates verbatim for review.
start=text.index('            if (pMissilesTxtRecord->dwMissileFlags & gdwBitMasks[MISSILESFLAGINDEX_PIERCE]')
end=text.index('\n    else\n',text.index('            if (!SUNITDMG_IsHitSuccessful',start))
collision=text[start:end];assert 'SUNITDMG_IsHitSuccessful(pOwner, pUnit, nToHit, 1)' in collision
e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,
 probe=dict(totalCases=len(data),compiledRoutines=compiled,blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},dataPath='.local/class-skills/weapon-missile-impact-probe.json'),
 collisionBlockHash=hashlib.sha256(collision.encode()).hexdigest(),
 established=['The unchanged impact reader rolls saved missile endpoints, adds the saved damage percent plus saved demon/undead percent based on the current victim, clamps the percentage at-90, then doubles physical once for the saved critical flag. It does not calculate fresh weapon damage from the equipped item.',
 'Each typed endpoint rolls separately; source elemental mastery belongs to the missile stat read, not an automatic fresh wielder enhancement. Poison duration divides by saved count only when count>1. A nonpositive maximum or minimum yields0 before endpoint swapping.',
 'The reviewed collision function calls IsHitSuccessful with the owner and saved missile ToHit bonus. That helper reads current owner attack/level/ignore-defense and current target defense. Prior checkpoint launch-time AR snapshot plans are withdrawn.',
 'The reviewed collision branch consumes a saved pierce index only while the current owner has item or skill pierce. A launch budget alone is insufficient after the owner loses pierce.',
 'Reviewed factory damage creation calls the critical helper for each created weapon missile. Do8 creates each fan missile independently; a launch builder must not share one critical roll across the whole fan. Each missile retains its own resulting flag.'],
 limits=['RollLimitedRandomNumber, unit stat access, monster classification and hierarchy lookup are declared services. The compiled impact reader has no monster-type bonus rows; full nested hierarchy copying remains unsupported.',
 'Legacy body arithmetic is not a modern executable certificate. Exact current RNG/activation/order, all hit/dodge/block/proc paths and live client remain separate.',
 'Collision-time AR/owner pierce gates are reviewed rather than compiled here. Source pierce seed and NHD global state are superseded/limited by modern references; do not silently copy legacy global JUSTHIT across skills.',
 'Launch-time source-stat collection, material/Enchant/aura mastery composition, immunity bypasses, item event callbacks, life/mana drain and full offline/owner transitions remain distinct integration dependencies.'])
(ROOT/'tools/reference/weapon-missile-impact-evidence.json').write_text(json.dumps(e,indent=2)+'\n')
print('Weapon missile impact source cases:',len(data))
