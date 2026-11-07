#!/usr/bin/env python3
"""Execute complete older stat initialization with current Shadow inputs."""
import hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-initializer-source';PIN='0c0bbd1e6bef9181a3da1c046523af622a925416'
def digest(b):return hashlib.sha256(b).hexdigest()
def routine(text,name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 i=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
 return text[m.start():i]
def main():
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==PIN;hashes={};blocks={}
 def source(path):
  raw=(BASE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==path)
  hashes[path]=digest(raw);return raw.decode()
 def function(text,name):
  body=routine(text,name);blocks[name]=digest(body.encode());return body+'\n'
 monster=source('source/D2Game/src/MONSTER/Monster.cpp');header=source('source/D2Common/include/DataTbls/MonsterTbls.h');items=source('source/D2Game/src/ITEMS/Items.cpp');seed=source('source/D2Common/include/D2Seed.h')
 data=json.loads((ROOT/'tools/reference/shadow-initializer-data-evidence.json').read_text());assert len(data['shadows'])==2 and all(r['componentChoiceCounts']==[0,1]+[0]*14 and r['monsterProperty']==65535 for r in data['shadows'])
 parent=json.loads((ROOT/'tools/reference/blade-spawn-engine-evidence.json').read_text());prefix=(BASE/'blade-spawn-source-probe.cpp').read_text().split('int main(){',1)[0]
 for name,h in parent['compiledUnchangedRoutines'].items():assert digest(routine(prefix,name).encode())==h,name
 blocks.update(parent['compiledUnchangedRoutines']);hashes.update(parent['sourceHashes'])
 prefix=re.sub(r'struct D2MonStatsTxt\{.*?\}mon;',r'''struct D2MonStatsTxt{uint32_t dwMonStatsFlags=0;int nAlign=1,nLevel[3]={},nToBlock[3]={28,28,28},nResistances[6][3]={};
int nMinHP[3]={400,400,400},nMaxHP[3]={480,480,480},nAC[3]={141,152,164},nExp[3]={},nA1TH[3]={250,300,350},nA1MinD[3]={},nA1MaxD[3]={},nA2TH[3]={},nA2MinD[3]={},nA2MaxD[3]={},nS1TH[3]={},nS1MinD[3]={},nS1MaxD[3]={},nElMinD[3][3]={},nElMaxD[3][3]={},nElDur[3][3]={};
uint32_t dwDamageRegen=12;int nSkill[8]={-1,-1,-1,-1,-1,-1,-1,-1};uint8_t nSkLvl[8]={};int8_t nSkillMode[8]={};int wMonProp=65535;}mon;''',prefix,flags=re.S)
 prefix=prefix.replace('struct D2MonsterDataStrc{D2MonStatsTxt* pMonstatsTxt=&mon;}data;', 'struct D2MonsterDataStrc{D2MonStatsTxt* pMonstatsTxt=&mon;uint8_t nComponent[16]={};}data;')
 prefix=prefix.replace('struct D2UnitStrc{D2MonsterDataStrc* pMonsterData=&data;}unit;', '''struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2UnitStrc{int dwUnitType=1,dwClassId=357,dwUnitId=1;uint32_t dwFlags=0;D2SeedStrc pSeed{};D2MonsterDataStrc* pMonsterData=&data;void*pInventory=nullptr,*pSkills=nullptr;int stats[64]={};}unit;''')
 prefix=prefix.replace('struct D2GameStrc{uint8_t nDifficulty=0;}game;', 'struct D2GameStrc{uint8_t nDifficulty=0;bool bExpansion=true;int nGameType=1,dwGameType=0;void*pMemoryPool=nullptr;}game;')
 prefix=prefix.replace('assert(id==413)','assert(id==417||id==418)')
 extra=r'''
#include <cstdlib>
#define D2_ASSERT assert
#define UNIT_MONSTER 1
#define MONSTER_DOOMKNIGHT2 312
#define MONSTER_DOOMKNIGHT3 316
#define UNITFLAG_CANBEATTACKED 1
#define UNITFLAG_PETIGNORE 2
#define D2GAME_ITEMMODE_ServerStatlistCallback_6FC41910 nullptr
constexpr uint8_t operator""ui8(unsigned long long n){return uint8_t(n);}
template<class T>T D2Clamp(T x,T low,T high){return std::clamp(x,low,high);}
enum Stat{STAT_LEVEL,STAT_MONSTER_PLAYERCOUNT,STAT_DAMAGERESIST,STAT_MAGICRESIST,STAT_FIRERESIST,STAT_LIGHTRESIST,STAT_COLDRESIST,STAT_POISONRESIST,STAT_TOBLOCK,STAT_ATTACKRATE,STAT_VELOCITYPERCENT,STAT_OTHER_ANIMRATE,STAT_LAST_SENT_HP_PCT,STAT_MAXHP,STAT_HITPOINTS,STAT_ARMORCLASS,STAT_EXPERIENCE,STAT_HPREGEN};
'''+re.search(r'enum D2C_MonStats2TxtFlagIndices\s*\{.*?\};',header,re.S)[0]+r'''
struct D2MonStats2Txt{uint32_t dwFlags=0;uint8_t counts[16]={};}secondary;
struct D2MonRegDataStrc{int nComponentVariantsMax=0;uint8_t nComponentVariants[3][16]={};}region;
struct D2PropertyStrc{int nProperty=-1;};struct D2MonPropTxt{D2PropertyStrc props[3][6];uint8_t nChance[3][6];};
struct D2SkillStrc{int id;};struct D2StatListStrc{};
int draws=0,propertyLookups=0,equipmentCalls=0;
#include <vector>
#include <array>
std::vector<std::array<int,3>> assigned;D2SkillStrc skill;
uint64_t SEED_RollRandomNumber(D2SeedStrc*s){++draws;return s->lSeed=0x6ac690c5ULL*s->nLowSeed+s->nHighSeed;}
'''+function(seed,'SEED_RollLimitedRandomNumber')+function(items,'ITEMS_RollLimitedRandomNumber')+function(items,'ITEMS_RollRandomNumber')+r'''
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int id){assert(id==417||id==418);return &secondary;}
int D2COMMON_11068_GetCompInfo(D2UnitStrc*,int i){return secondary.counts[i];}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
int DATATBLS_GetMonsterLevelInArea(int,int,bool){std::abort();}
int DUNGEON_GetLevelIdFromRoom(D2ActiveRoomStrc*){return 1;}
void STATLIST_AllocStatListEx(D2UnitStrc*,int,void*,D2GameStrc*){}
void STATLIST_SetUnitStat(D2UnitStrc*u,int stat,int value,int){u->stats[stat]=value;}
void MONSTERS_ApplyClassicScaling(D2UnitStrc*,bool,uint8_t){assert(mon.nAlign==1);}
D2StatListStrc* STATLIST_AllocStatList(void*,int,int,int,int){return nullptr;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void* SUNITPROXY_GetNpcInventory(D2GameStrc*,int){std::abort();}
void INVENTORY_AllocInventory(void*,D2UnitStrc*u){u->pInventory=&data;}
void* SKILLS_AllocSkillList(void*){return &data;}
void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int id,int rank,int flags){assert(flags==0);assigned.push_back({id,rank,-1});}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int id){assert(assigned.back()[0]==id);skill.id=id;return &skill;}
void SKILLS_SetSkillMode(D2SkillStrc*s,int mode){assert(assigned.back()[0]==s->id);assigned.back()[2]=mode;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int id){assert(id==417||id==418);return &mon;}
D2MonPropTxt* MONSTER_GetMonPropTxtRecord(int id){assert(id==65535);++propertyLookups;return nullptr;}
void D2COMMON_11292_ItemAssignProperty(int,void*,D2UnitStrc*,void*,int,int,D2PropertyStrc*,int,int,void*){std::abort();}
void sub_6FCF9580(D2GameStrc*,D2UnitStrc*owner,D2UnitStrc*,int skill,int rank,int level,int init){assert(owner==nullptr&&skill==-1&&rank==0&&level==0&&init==1);++equipmentCalls;}
'''
 # The full initializer also needs the primary-record count. The rest of the
 # table is the unchanged accepted noRatio calculator and player-count helpers.
 prefix=prefix.replace('struct Tables{int nMonLvlTxtRecordCount=111;', 'struct Tables{int nMonStatsTxtRecordCount=751;int nMonLvlTxtRecordCount=111;')
 cpp=prefix+extra+function(source('source/D2Game/src/MONSTER/MonsterUnique.cpp'),'MONSTERUNIQUE_CalculatePercentage')+function(monster,'MONSTER_SetComponents')+function(monster,'MONSTER_InitializeStatsAndSkills')+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=uint32_t(1)<<i;bool first=true;std::printf("[");
for(int monster:{417,418})for(uint64_t seed:{0ULL,1ULL,0x29a00000000ULL,0x29a00000001ULL,0x29affffffffULL,0xffffffffffffffffULL,0xdeadbeef12345678ULL})
for(int difficulty:{0,1,2})for(int expansion:{0,1})for(int p:{1,2,8,64})for(int variant:{0,1,2,3}){
mon.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_NORATIO]|gdwBitMasks[MONSTATSFLAGINDEX_INVENTORY];game.nDifficulty=difficulty;game.bExpansion=expansion;game.nGameType=expansion;
players=p;playerQueries=draws=propertyLookups=equipmentCalls=0;unit={};data={};secondary={};region={};secondary.dwFlags=gdwBitMasks[MONSTATS2FLAGINDEX_ISATT];unit.pSeed.lSeed=seed;
unit.dwClassId=monster;assigned.clear();secondary.counts[1]=1;mon.dwDamageRegen=monster==417?3:2;
for(int d=0;d<3;d++){mon.nMinHP[d]=mon.nMaxHP[d]=376;mon.nAC[d]=196;mon.nToBlock[d]=50;mon.nResistances[0][d]=40;}
int ids[8]={259,266,274,260,275,269,0,-1};for(int i=0;i<8;i++){mon.nSkill[i]=ids[i];mon.nSkLvl[i]=i<7?1:0;mon.nSkillMode[i]=i<6?14:5;}
if(variant==2)region.nComponentVariantsMax=1;if(variant==3)region.nComponentVariantsMax=3;
MONSTER_InitializeStatsAndSkills(&game,&room,&unit,variant==0?nullptr:&region);
if(!first)std::printf(",");first=false;std::printf("[%d,\"%llu\",%d,%d,%d,%d,\"%llu\",%d,%d,%d,%d,%d,[",monster,(unsigned long long)seed,difficulty,expansion,p,variant,(unsigned long long)unit.pSeed.lSeed,draws,playerQueries,propertyLookups,equipmentCalls,int(unit.pInventory!=nullptr));
for(int i=0;i<18;i++)std::printf(i?",%d":"%d",unit.stats[i]);std::printf("],[");for(int i=0;i<16;i++)std::printf(i?",%d":"%d",unit.pMonsterData->nComponent[i]);std::printf("],[");for(size_t i=0;i<assigned.size();i++)std::printf(i?",[%d,%d,%d]":"[%d,%d,%d]",assigned[i][0],assigned[i][1],assigned[i][2]);std::printf("]]");
}std::printf("]\n");}
'''
 path=STAGE/'probe.cpp';path.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(path),'-o',str(binary)],check=True)
 with (STAGE/'sanitizers.log').open('w')as err:run=subprocess.run([str(binary)],stdout=subprocess.PIPE,stderr=err,check=True)
 assert not(STAGE/'sanitizers.log').read_text();cases=json.loads(run.stdout);assert len(cases)==1344
 def roll(s):s=0x6ac690c5*(s&0xffffffff)+(s>>32);return s,s&0xffffffff
 for monster,original,diff,expansion,players,variant,after,draws,queries,prop,equip,inventory,stats,components,skills in cases:
  seed=int(original);seed,_=roll(seed);seed,_=roll(seed)
  assert [str(seed),draws,queries,prop,equip,inventory]==[after,2,0,1,1,1]
  assert components==[0]*16 and stats==[0,1,40,0,0,0,0,0,50,100,75,100,128,96256,96256,196,0,70 if monster==417 else 47]
  assert skills==[[id,1+[0,3,7][diff],14 if i<6 else 5]for i,id in enumerate([259,266,274,260,275,269,0])]
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 proof=dict(referenceVersion='3.3.93847',legacyCommit=PIN,currentExecutableParity=False,contexts=len(cases),completeRoutineHashes=blocks,sourceHashes=hashes,currentDataEvidenceSha256=digest((ROOT/'tools/reference/shadow-initializer-data-evidence.json').read_bytes()),inputs={str(Path(__file__).resolve().relative_to(ROOT)):digest(Path(__file__).read_bytes())},artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in [path,binary,fixture,STAGE/'sanitizers.log']},scope=['Complete older stats/skills initializer,component setter,limited draw,percentage,noRatio table and player-count helpers with current417/418inputs. Component bound1 consumes the first draw;fixed HP consumes the second. No-cache,empty cache,one/three identical valid variant entries have the same continuation.', 'Both Shadows,all three difficulties,both game contexts,four supplied player counts and seven edge seeds:fixed376HP,196AC,40physical resist,50block,unwritten AR0,level0,attack/other rates100,velocity75,regeneration70/47,zero mana/stamina and seven initial skill/mode assignments at table rank1 plus the current difficulty bonus0/3/7.'],services=['Declared table/stat/skill/inventory/memory/room services are not a binary ABI. The skill assignment service records requested skill/rank/mode;it does not execute allocation/refresh/permission or assign animation sequences. NoRatio avoids ratio callbacks and classic scaling is a friendly no-op. Compiled65535monprop is absent;onInit equipment call is recorded and all current Shadow rows disable the guard.', 'Outer unit allocation,AI/quest/animation callbacks and post-initializer helper/equipment remain separate. Valid current variant entries are supplied rather than claiming region-cache construction is certified. No hidden RNG is supplied by callbacks.'],limits=['No complete current monster initializer execution,live Shadow actor,resource admission,AI or learned cast is certified. Captured current birth helper is independently established. Source initializer and table contracts must remain labeled separately from current executable parity.'])
 (ROOT/'tools/reference/shadow-initializer-source-evidence.json').write_text(json.dumps(proof,indent=2)+'\n');print('Shadow initializer:1344 complete source contexts,both Shadows/seven skills,two fixed component/HP draws,valid-cache parity,ASan/UBSan clean.')
if __name__=='__main__':main()
