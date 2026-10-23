#!/usr/bin/env python3
"""Compile War Cry physical endpoint/roll/stun/block and missile flag contracts."""
from pathlib import Path
import csv,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'};hashes={};blocks={}
def source(p):
 raw=(STAGE/'d2moo-pinned-source'/p).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[p],p
 hashes[p]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(s,n):
 m=re.search(r'^.*\b'+re.escape(n)+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,n
 e=s.index('{',m.start())+1;d=1
 while d:d+=(s[e]=='{')-(s[e]=='}');e+=1
 blocks[n]=s[m.start():e];return blocks[n]
common=source('source/D2Common/src/D2Skills.cpp');units=source('source/D2Common/src/Units/Units.cpp');miss=source('source/D2Game/src/MISSILES/MissMode.cpp');dmg=source('source/D2Game/src/UNIT/SUnitDmg.cpp');missileSource=source('source/D2Common/src/Units/Missile.cpp');factory=source('source/D2Game/src/MISSILES/Missiles.cpp');header=source('source/D2Game/include/UNIT/SUnitDmg.h')
components=source('source/D2Game/src/MONSTER/Monster.cpp');componentCodes=source('source/D2Common/src/Monsters/Monsters.cpp')
names=[(components,'MONSTER_SetComponents'),(common,'SKILLS_CalculateDamageBonusByLevel'),(common,'SKILLS_GetMinPhysDamage'),(common,'SKILLS_GetMaxPhysDamage'),(miss,'MISSMODE_RollDamageValue'),(miss,'MISSMODE_SrvDmg07_Warcry_ShockWave'),(units,'UNITS_GetBlockRate'),(dmg,'SUNITDMG_ApplyDodge'),(dmg,'SUNITDMG_ApplyBlockOrDodge'),(miss,'MISSMODE_SetDamageFlags')]
for s,n in names:routine(s,n)
joined='\n'.join(blocks.values());stats=sorted(set(re.findall(r'\bSTAT_[A-Z0-9_]+',joined)));indices=sorted(set(re.findall(r'\b(?:SKILLS|MISSILES|MONSTATS)FLAGINDEX_[A-Z0-9_]+',joined)))
flag_values={n:int(v,16) for n,v in re.findall(r'^\s*((?:DAMAGERESULTFLAG|DAMAGEHITFLAG|BLOCKFLAG)_\w+)\s*=\s*(0x[0-9A-Fa-f]+)',header,re.M)}
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <map>
#include <initializer_list>
#define __stdcall
#define __fastcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
using BOOL=int;constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,PLRMODE_WALK=2,PLRMODE_RUN=3,MONMODE_WALK=2,MONMODE_RUN=15,STATE_UNINTERRUPTABLE=1,MONSTER_DIABLO=243,MONSTER_DOOMKNIGHT1=310,MONSTER_DIABLOCLONE=333,MONSTER_ACT3HIRE=359,ITEMTYPE_SHIELD=2;
'''
cpp+='enum {'+','.join(stats)+'};\n'+'enum {'+','.join(indices)+'};\n'
cpp+='constexpr uint32_t '+','.join(n+'='+str(v) for n,v in flag_values.items())+';\n'
cpp+='constexpr uint32_t gdwBitMasks[]={'+','.join(str(1<<i)+'u' for i in range(32))+'};\n'
cpp+=r'''struct MissileData{int nSkill=154;};struct MonsterData{uint8_t nComponent[16]={};};using D2MonsterDataStrc=MonsterData;struct D2MonRegDataStrc{int nComponentVariantsMax=0;uint8_t nComponentVariants[1][16]={};};
struct D2UnitStrc{int dwUnitType=0,dwClassId=1,dwAnimMode=1,pSeed=0,synergy=0;void* pInventory=nullptr;MonsterData* pMonsterData=nullptr;MissileData* pMissileData=nullptr;D2UnitStrc* owner=nullptr;std::map<int,int> stats;};
struct D2GameStrc{bool bExpansion=true;} game;
struct D2SkillsTxt{uint32_t dwFlags[2]={};int nSrcDam=0,nToHitShift=8,dwMinDam=30,dwMaxDam=40,dwMinLvlDam[5]={8,9,10,12,14},dwMaxLvlDam[5]={8,9,10,12,14},dwDmgSymPerCalc=1,dwParam[8]={25,5};} row;
struct D2MissilesTxt{int dwDmgParam[2]={},nKnockBack=0;uint32_t dwMissileFlags=0;} missileRow;
struct D2CharStatsTxt{int nBlockFactor=30;} chars[8];struct D2MonStatsTxt{uint32_t dwMonStatsFlags=0;} mon;
struct D2ItemsTxt{int wType[2]={2,0};} item;
struct Tables{int nCharStatsTxtRecordCount=8,nMonStatsTxtRecordCount=500;D2CharStatsTxt* pCharStatsTxt=chars;} tables;Tables* sgptDataTables=&tables;
struct D2DamageStrc{int dwPhysDamage=0,dwStunLen=0,dwHitClass=0,nHitClassActiveSet=0,dwHitFlags=0,dwPiercePct=0;uint32_t wResultFlags=0;};
bool hasSkill=true,hasMissile=true,shield=false,hasMon=true,moving=false,uninterruptable=false;int component=0,weaponBlock=0,weaponClass=13,eventCalls=0,commitCalls=0,blockRoll=99,physicalRoll=0;uint32_t flags=0;
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return hasSkill?&row:nullptr;}D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return DATATBLS_GetSkillsTxtRecord(id);}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasMissile?&missileRow:nullptr;}
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int){return hasMon?&mon:nullptr;}
D2ItemsTxt* DATATBLS_GetItemRecordFromItemCode(int code,int*){return code==1?&item:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p?p->stats[s]:0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int s,int x){return STATLIST_UnitGetStatValue(p,s,x);}
int STATLIST_GetMinDamageFromUnit(D2UnitStrc*,int){return 999;}int STATLIST_GetMaxDamageFromUnit(D2UnitStrc*,int){return 999;}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return nullptr;}int INVENTORY_GetWieldType(D2UnitStrc*,void*){return 0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int,int,int){return p->synergy;}
int ITEMS_RollLimitedRandomNumber(int*,int span){return span>0?std::clamp(physicalRoll,0,span-1):0;}
uint32_t ITEMS_RollRandomNumber(int*){return blockRoll;}
int MONSTERUNIQUE_CalculatePercentage(int v,int n,int d){return int(int64_t(v)*n/d);}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* p){return p->owner;}
int MISSILE_GetSkill(D2UnitStrc*){return 154;}int MISSILE_GetLevel(D2UnitStrc* p){return p->stats[STAT_LEVEL];}
int MISSILE_GetHitClass(D2UnitStrc*){return 0;}int MISSILE_GetFlags(D2UnitStrc*){return flags;}
bool STATES_CheckState(D2UnitStrc*,int){return uninterruptable;}
D2UnitStrc* INVENTORY_GetEquippedShield(void*,void*){static D2UnitStrc s;return shield?&s:nullptr;}
int D2Common_11069(D2UnitStrc*,int,int){return component;}bool UNITS_IsInMovingMode(D2UnitStrc*){return moving;}
int SUNITDMG_GetWeaponBlock(D2UnitStrc*){return weaponBlock;}
void COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int* out,int,int){*out=weaponClass;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc*){eventCalls++;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){commitCalls++;}
'''
cpp+='int componentCount=1;int D2COMMON_11068_GetCompInfo(D2UnitStrc*,int){return componentCount;}\n'+blocks['MONSTER_SetComponents']+'\n'
for n in ['SKILLS_CalculateDamageBonusByLevel','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage','MISSMODE_RollDamageValue','MISSMODE_SrvDmg07_Warcry_ShockWave','UNITS_GetBlockRate','SUNITDMG_ApplyDodge','SUNITDMG_ApplyBlockOrDodge','MISSMODE_SetDamageFlags']:cpp+=blocks[n]+'\n'
cpp+=r'''int main(){D2UnitStrc owner,target,shot;target.dwUnitType=1;shot.dwUnitType=3;shot.owner=&owner;shot.stats[STAT_LEVEL]=1;bool first=true;std::printf("{\"physicalCases\":[");
for(int rank:{1,2,8,9,16,17,20,21,22,23,28,29,48})for(int hard:{0,1,3,20,40,60}){owner.synergy=hard*6;int a=SKILLS_GetMinPhysDamage(&owner,154,rank,0),b=SKILLS_GetMaxPhysDamage(&owner,154,rank,0);shot.stats[STAT_MINDAMAGE]=a;shot.stats[STAT_MAXDAMAGE]=b;owner.synergy=0;for(int draw:{0,1,255,256,99999}){physicalRoll=draw;int result=MISSMODE_RollDamageValue(&shot,STAT_MINDAMAGE,STAT_MAXDAMAGE,-1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",rank,hard,draw,a,b,result);}}
std::printf("],\"stunCases\":[");first=true;for(int rank:{0,1,20,21,60})for(int param:{0,7})for(int valid:{0,1})for(int missile:{0,1}){D2DamageStrc d;d.dwStunLen=999;d.dwHitClass=1;hasSkill=valid;hasMissile=missile;missileRow.dwDmgParam[0]=param;shot.stats[STAT_LEVEL]=rank;MISSMODE_SrvDmg07_Warcry_ShockWave(&game,&shot,&target,&d);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",rank,param,valid,missile,d.dwStunLen,d.dwHitClass);}
hasSkill=hasMissile=true;missileRow.dwDmgParam[0]=0;
std::printf("],\"monsterBlockCases\":[");first=true;
for(int cls:{1,243,310,333,359})for(int innate:{0,1})for(int comp:{0,1,2,3})for(int rate:{0,10,75,90}){target.dwClassId=cls;mon.dwMonStatsFlags=innate?gdwBitMasks[MONSTATSFLAGINDEX_NOSHLDBLOCK]:0;component=comp;target.stats[STAT_TOBLOCK]=rate;int result=UNITS_GetBlockRate(&target,true);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",cls,innate,comp,rate,result);}
std::printf("],\"blockFlagCases\":[");first=true;target.dwClassId=1;component=0;mon.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_NOSHLDBLOCK];
for(int rate:{0,10,75})for(int roll:{0,9,10,74,75,99})for(int phys:{0,7680})for(int uninterrupt:{0,1}){target.stats.clear();target.stats[STAT_TOBLOCK]=rate;blockRoll=roll;uninterruptable=uninterrupt;missileRow.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_GETHIT];D2DamageStrc d;d.dwPhysDamage=phys;d.dwStunLen=25;d.dwHitClass=0x60;eventCalls=commitCalls=0;MISSMODE_SetDamageFlags(&game,&shot,&target,&d);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%u,%d,%d,%d]",rate,roll,phys,uninterrupt,d.wResultFlags,eventCalls,commitCalls,d.dwStunLen);}
std::printf("],\"dodgeCases\":[");first=true;uninterruptable=false;
for(int mode:{1,2,15})for(int avoid:{0,1})for(int passive:{0,35})for(int roll:{0,34,35,99}){target.dwAnimMode=mode;target.stats.clear();target.stats[STAT_PASSIVE_AVOID]=target.stats[STAT_PASSIVE_DODGE]=target.stats[STAT_PASSIVE_EVADE]=passive;blockRoll=roll;int result=SUNITDMG_ApplyDodge(&owner,&target,avoid);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",mode,avoid,passive,roll,result);}
std::printf("],\"componentCases\":[");first=true;MonsterData model;target.pMonsterData=&model;target.dwUnitType=1;D2MonRegDataStrc region;for(int i=0;i<16;i++)region.nComponentVariants[0][i]=i;
for(int count:{1,2,3,4,5})for(int draw:{0,1,4,99})for(int pool:{0,1}){componentCount=count;physicalRoll=draw;region.nComponentVariantsMax=pool;MONSTER_SetComponents(&region,&target);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",count,draw,pool,model.nComponent[7]);}
std::printf("]}\n");}
'''
path=STAGE/'warcry-source-probe.cpp';path.write_text(cpp);binary=STAGE/'warcry-source-probe';subprocess.run(['g++','-std=c++20','-Wno-multichar','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
def tier(rank):
 extra=max(0,rank-1);total=0
 for count,inc in zip((7,8,6,6,1000),(8,9,10,12,14)):
  n=min(extra,count);total+=n*inc;extra-=n
 return total
for rank,hard,draw,lo,hi,rolled in data['physicalCases']:
 a,b=30+tier(rank),40+tier(rank);a=(a+a*(hard*6)//100)*256;b=(b+b*(hard*6)//100)*256
 assert (lo,hi,rolled)==(a,b,a+min(draw,b-a-1))
for rank,param,valid,missile,stun,hitclass in data['stunCases']:assert (stun,hitclass)==((param if param>0 else 25+5*(rank-1) if valid and rank>0 else 0,0x60) if missile else (999,1))
for cls,innate,component,rate,result in data['monsterBlockCases']:assert result==(min(75,rate) if innate or cls in (243,310,333) or cls!=359 and component==1 else 0)
for rate,roll,physical,uninterrupt,result,events,commit,stun in data['blockFlagCases']:
 blocked=bool(physical and roll<rate);expected=(0x10 if blocked else 1)+(0 if uninterrupt else 4)
 assert (result,events,commit,stun)==(expected,int(not blocked),1,25)
for mode,avoid,passive,roll,result in data['dodgeCases']:assert result==(8 if mode in (2,15) else 2 if avoid else 4) if roll<passive else result==0
for count,draw,pool,choice in data['componentCases']:assert choice==(7 if pool else min(draw,count-1))
(STAGE/'warcry-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
for s,n in [(componentCodes,'D2Common_11069'),(missileSource,'MISSILE_CalculateDamageData'),(missileSource,'MISSILE_SetDamageStats'),(factory,'MISSILES_CreateMissileFromParams'),(dmg,'SUNITDMG_ExecuteEvents'),(dmg,'SUNITDMG_ExecuteMissileDamage')]:routine(s,n)
modernPath=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json'
modern=list(json.loads(modernPath.read_text()).values())
legacyPath=ROOT/'.local/research/foundation/MonStats.txt'
legacy=list(csv.DictReader(legacyPath.open(),delimiter='\t'))
assert {r['Id'] for r in modern if r.get('ShieldBlockOverride')==1}=={r['Id'] for r in legacy if r.get('NoShldBlock')=='1'}
assert [r['Id'] for r in modern if r.get('ShieldBlockOverride')==2]==['act3hire']
assert {r.get('ShieldBlockOverride',0) for r in modern}<={0,1,2}
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],status='Bounded compiled War Cry source prerequisites; native integration pending.',sourceHashes=hashes,probe=dict(caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},dataPath='.local/class-skills/warcry-source-probe.json'),established=[
'Physical skill endpoints add tier damage, truncate synergy percentage in whole points, then shift by HitShift. This differs from the existing elemental Damage256 order. Current War Cry source SrcDam0 excludes the weapon branch of these getters.',
'Missile creation reads physical endpoints into missile damage stats; collision rolls against those stored min/max with exclusive upper endpoint. Changing current owner synergy does not change the already supplied per-hit endpoint fixture. Creation/writer are reviewed separately, not compiled here.',
'Damage callback7 selects explicit positive missile dParam1 or positive-level source skill Param1/2, else0; valid callback writes hit class0x60. These parameters agree with pinned War Cry calc4 ln12.',
'Full block-rate getter distinguishes innate noshldblock, three special classes, Act3hire, actual component shield item type and cap75; a ToBlock number alone does not establish a block chance for every monster. Native source-component mappings must precede broad target support.',
'Full block/dodge and missile flag routines compile. Physical presence enables shield block, moving actors select evade, standing missile avoids select avoid, and any block/dodge result clears successful hit. Uninterruptable suppresses GetHit but is a separate later damage gate.',
'Flag dispatch executes hit events only on success but always requests the final missile presentation handler. Stun length remains in the packet on block; native implementation must follow the event/health guard order rather than applying stun solely because its length is nonzero.',
'Compiled component assignment retains selected regional appearance pools or chooses independent component indices when no pool exists. Native SH-only lazy selection/persistence uses the latter primitive; regional pools, source RNG sequence and visual correspondence remain provisional.',
'Source missile writer copies DAMAGE_VS_MONTYPE layers from owner even for a no-weapon spell; this is distinct from ordinary strength/mastery/weapon ED/deadly/leech paths. Keep this special item/stat branch explicit.'],modernBlockCrosswalk=dict(tableHash=hashlib.sha256(modernPath.read_bytes()).hexdigest(),legacyDataHash=hashlib.sha256(legacyPath.read_bytes()).hexdigest(),renamingSource='https://news.blizzard.com/en-us/article/23827590/diablo-ii-resurrected-ladder-season-two-has-concluded',finding='Blizzard patch2.5 documents NoShldBlock renamed ShieldBlockOverride. All18 current override1 rows match the supplemental NoShldBlock rows exactly; the only override2 row is act3hire, which the compiled getter separately excludes. This table/source crosswalk is an inference for the current rows, not a compiled modern engine or arbitrary modded override contract.'),stubs=[
'Unchanged tier/physical getters/per-hit roll/damage7/full block-rate/dodge/block dispatch/missile damage flags with declared tables/items/components/RNG/owner/event/commit/stat services.',
'Physical vectors exercise War Cry no-kick/no-weapon SrcDam0 and hard-synergy formula supplied by fixture. Other kick/weapon branches compile but are outside these vectors. Player block/walking, weapon-block inventory and nonzero passive weapon-block combinations are outside the matrix.',
'Monster shield component codes/item types and innate flags are supplied fixtures. Full source monster component generation/selection and actual native saved shield appearance are not certified.',
'Missile creation, endpoint writer and final hit-event/health/presentation consumers are hash-reviewed, not compiled in this program. Event/commit calls are recorded independently; full damage/stun commitment remains a separate trace.'],limits=[
'No native War Cry handler or modern executable certificate. Complete source snapshot/typed damage/stun/event/physical immunity and native source-component block mapping remain integration work.',
'Native SC/FCR/geometry/terrain/NextHit caster/cast scope, snapshots/gear/generic monster-type stat layers/owner/client acceptance remain open.',
'Final effects and talent/hotkey icons remain identified placeholders; no asset work or new class completion claim.'])
(ROOT/'tools/reference/warcry-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n');print('War Cry bounded source prerequisites passed:',out['probe']['caseCounts'])
