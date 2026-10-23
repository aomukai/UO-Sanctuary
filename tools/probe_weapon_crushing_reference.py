#!/usr/bin/env python3
"""Compile the unchanged Crushing Blow event and review classification conflicts."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,token):
 start=text.index(token);end=text.index('{',start)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
item=source('source/D2Game/src/SKILLS/SkillItem.cpp');cb=routine(item,'int32_t __fastcall SKILLITEM_EventFunc16_CrushingBlow(')
header=source('source/D2Common/include/D2Monsters.h');flags={k:int(v,16) for k,v in re.findall(r'(MONTYPEFLAG_\w+) = (0x[0-9A-Fa-f]+)',header)}
assert flags['MONTYPEFLAG_SUPERUNIQUE']==2 and flags['MONTYPEFLAG_CHAMPION']==4 and flags['MONTYPEFLAG_UNIQUE']==8
unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp');type_check=routine(unique,'int32_t __fastcall MONSTERUNIQUE_CheckMonTypeFlag(')
monsters=source('source/D2Common/src/Monsters/Monsters.cpp');boss=routine(monsters,'BOOL __stdcall MONSTERS_IsBoss(')
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
using BOOL=int;constexpr int FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,STAT_MONSTER_PLAYERCOUNT=0,STAT_HITPOINTS=1,STAT_DAMAGERESIST=2,MONSTATSFLAGINDEX_BOSS=0;
struct Monster{uint16_t nTypeFlag;};struct D2MonStatsTxt{int dwMonStatsFlags;};
struct D2UnitStrc{int dwUnitType,dwClassId,pSeed,stats[3],chance;Monster* pMonsterData;bool hire;};
struct D2DamageStrc{int wResultFlags;};struct D2GameStrc{};
int bossFlag=0,hpBonus=0,calls=0,draw=0;int gdwBitMasks[1]={1};D2MonStatsTxt mon;
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int){mon.dwMonStatsFlags=bossFlag;return &mon;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,unsigned,int){return p->chance;}
int ITEMS_RollRandomNumber(int*){calls++;return draw;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p->hire;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]=value;}
int MONSTER_GetHpBonus(int){return hpBonus;}
int MONSTERUNIQUE_CalculatePercentage(int v,int n,int d){return int(int64_t(v)*n/d);}
void UNITS_SetOverlay(D2UnitStrc*,int,int){}
'''+type_check+'\n'+boss+'\n'+cb+r'''
int main(){bool first=true;std::printf("[");
for(int hp:{257,1025,1280000})for(int flags:{0,2,4,8})for(int isBoss:{0,1})for(int ranged:{0,1})for(int resist:{-50,0,33,100})for(int bonus:{0,50,350})for(int roll:{0,99}){
 Monster data{uint16_t(flags)};D2UnitStrc owner{UNIT_PLAYER,0,0,{},50,nullptr,false},victim{UNIT_MONSTER,0,0,{1,hp,resist},0,&data,false};D2DamageStrc damage{};
 bossFlag=isBoss;hpBonus=bonus;draw=roll;calls=0;int result=SKILLITEM_EventFunc16_CrushingBlow(nullptr,ranged?6:5,&owner,&victim,&damage,0,0);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",hp,flags,isBoss,ranged,resist,bonus,roll,victim.stats[1],calls,result);
}std::printf("]\n");}
'''
path=STAGE/'weapon-crushing-source-probe.cpp';path.write_text(cpp);binary=STAGE/'weapon-crushing-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==1152
for hp,flags,is_boss,ranged,resist,bonus,roll,remaining,calls,result in data:
 div=8 if is_boss or flags&2 else 4;div+=div*bonus//100;div*=2 if ranged else 1
 loss=hp//div;loss-=loss*min(max(resist,0),100)//100
 assert (remaining,calls,result)==(hp-loss if roll<50 else hp,1,int(roll<50))
fixture=STAGE/'weapon-crushing-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',status='Source-only callback preparation; no Crushing Blow behavior changed by this probe.',legacyCommit=tree['sha'],sourceHashes=hashes,
 probe=dict(cases=1152,dataPath=str(fixture.relative_to(ROOT)),dataSha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),bodySha256=hashlib.sha256(cb.encode()).hexdigest()),actualTypeFlags=flags,
 established=['The unchanged event rolls current owner chance once, reads current victim FP256 life, divides by target/ranged/player-HP factors, then subtracts a separately truncated positive-resistance share. Negative physical resistance does not amplify CB.',
  'CB writes target life directly before ordinary weapon damage. Existing Native CrushingLoss reads displayed whole HP and combines some divisions; it cannot represent the same fractional contract.',
  'The actual enum and helper establish numeric2 as SuperUnique only; MONSTERS_IsBoss reads the MonStats boss flag. The older callback therefore differs from the official guide for ordinary Champions/Uniques. Do not reinterpret numeric2 using synthetic harness constants from unrelated probes.'],
 classificationConflict=dict(officialGuide='https://classic.battle.net/diablo2exp/items/magic.shtml',publishedRule='Champions, Uniques and Bosses have the halved fraction, with missile weapons halving again.',legacyRule='Boss or SuperUnique flag2 only in the compiled older body.',native='Current NamedCombat.Elite includes Champion/Unique/SuperUnique and mapped Boss, matching the published broader grouping.',policy='Retain existing native grouping pending current executable evidence; source-only arithmetic proof must not silently replace it with the narrower older classification.'),
 services=['Current chance50 and fixed draw0/99, live FP256 stat fields, non-hireling monster targets and declared HP-bonus0/50/350 are supplied. Actual player-count-to-HP-bonus generation is not compiled.',
  'Boss/type helper bodies compile unchanged with explicit table/bit-mask services. Overlay and full item-event dispatch remain services.'],
 next=['Implement a shared FP256 CB helper through existing health setters and current classification, with the conflict explicit. Source reduction stages/order must be preserved independently of unsupported classifications.', 'Integrate central callbacks before captured-leech victim-life clipping, then ordinary packet commitment; keep outer flags, defense loss and prevent-heal paths separate.', 'Verify real eligible crushing gear, fractional victim life, resistance, blocked/missed/outer shots, CB+leech ordering, and save/lifecycle/asset placeholders.'])
(ROOT/'tools/reference/weapon-crushing-engine-evidence.json').write_text(json.dumps(e,indent=2)+'\n')
print('Crushing Blow:1152 unchanged source event cases; actual enum verified and Champion/Unique reference conflict recorded.')
