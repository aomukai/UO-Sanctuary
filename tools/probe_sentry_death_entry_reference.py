#!/usr/bin/env python3
"""Compose regular trap death entry with real common stat/state/owner bodies.

The outer animation scheduler and death-event subscribers remain explicit services;
this proves cleanup/ownership transitions, not final GUID retention duration.
"""
import argparse,hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable,str(ROOT/'tools/probe_sentry_death_stats_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    parent=json.loads((ROOT/'tools/reference/sentry-death-stats-source-evidence.json').read_text())
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes=dict(parent['sourceHashes']);routines=dict(parent['compiledRoutineHashes'])
    def source(relative):
        raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def routine(relative,name):
        text=source(relative);m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        end,depth=text.index('{',m.start())+1,1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    units=source('source/D2Common/include/Units/Units.h')
    constants={name:int(re.search(r'\b'+name+r'\s*=\s*(0x[0-9A-Fa-f]+)',units)[1],16) for name in ['UNITFLAG_TARGETABLE','UNITFLAG_HASTXTMSG','UNITFLAGEX_STOREOWNERINFO','UNITFLAG_NOTC']}
    pin=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';raw=pin.read_bytes();digest=hashlib.sha256(raw).hexdigest();manifest=json.loads((pin.parent.parent/'sources.json').read_text());assert digest==manifest['files']['json/monstats.json']['sha256']
    traps={k:json.loads(raw)[k] for k in ['chargeboltsentry','wakeofdestruction','lightningsentry','infernosentry','deathsentry']}
    assert all(not r.get('deathDmg') and not r.get('SplEndDeath') for r in traps.values())
    base=(STAGE/'sentry-death-stats-source-probe.cpp').read_text().split('int main()',1)[0]
    base=base.replace('#include <cassert>','#include <cassert>\n#include <cstdlib>\nusing BOOL=int;using DWORD=uint32_t;using D2UnitGUID=int;\n#define D2_ASSERT assert\n#define D2_CALLOC_STRC_POOL(pool,type) new type{}')
    base=base.replace('struct D2StatListStrc;struct D2UnitStrc{','struct D2AiControlStrc;struct D2ActiveRoomStrc;struct D2PathStrc{int direction=-1;};struct D2MonsterRegionStrc;struct D2StatListStrc;struct D2UnitStrc{D2AiControlStrc* ai=nullptr;D2ActiveRoomStrc* room=nullptr;D2PathStrc* pDynamicPath=nullptr;void* pHoverText=nullptr;unsigned dwFlags=0,dwFlagEx=0;int dwClassId=410,dwAnimMode=1,dwOwnerType=0,dwOwnerGUID=1;D2UnitStrc* pListNext=nullptr;uint32_t state[4]={},gfx[4]={};bool hide=false;')
    base=base.replace('struct D2GameStrc{void* pMemoryPool=', 'struct D2GameStrc{D2UnitStrc* pUnitList[5][128]={};D2UnitStrc* pTileList=nullptr;D2MonsterRegionStrc** pMonReg=nullptr;void* pMemoryPool=')
    base=base.replace('struct Tables{int nMonLvlTxtRecordCount=', 'struct Tables{int nMonStatsTxtRecordCount=1000;uint32_t fStateMasks[3][4]={};int nMonLvlTxtRecordCount=')
    base=base.replace('constexpr uint32_t gdwBitMasks[]={1};','constexpr uint32_t gdwBitMasks[]={1,2,4,8};')
    base+='\nconstexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_MISSILE=3,UNIT_TILE=6,MONMODE_DEATH=0,UNITEVENT_DEATH=1,MONSTATSFLAGINDEX_DEATHDMG=1,MONSTATS2FLAGINDEX_DEADCOL=1,UNIT_ALIGNMENT_EVIL=2,MONSTER_BONEFETISH1=900,MONSTER_SIEGEBEAST1=901,MONSTER_BONEWALL=902,MISSILE_MONSTERCORPSEEXPLODE=999;\n'
    base+='\nconstexpr unsigned '+','.join(f'{n}={v}u' for n,v in constants.items())+';\n'
    base+=r'''
struct D2MinionListStrc{int dwMinionGUID=-1;D2MinionListStrc* pNext=nullptr;};
struct D2AiControlStrc{D2GameStrc* pGame=nullptr;int dwOwnerType=1,dwOwnerGUID=-1,dwOwnerTypeEx=1,dwOwnerGUIDEx=-1;uint16_t nAiFlags=0;D2MinionListStrc* pMinionList=nullptr;};
struct D2ActiveRoomStrc{int nLastDeadGUIDs[4]={-1,-1,-1,-1},nCurrentDeathIndex=0;}room;
struct D2MonsterRegionStrc{int dwMonKillCount=0;}region;
struct D2MonStatsTxt{unsigned dwMonStatsFlags=0;int nSplEndDeath=0,wMinion[2]={-1,-1},nBaseId=410;}monster;
struct D2MonStats2Txt{unsigned dwFlags=0;}model;
struct D2ModeChangeStrc{D2UnitStrc* pUnit=nullptr;int unk0x14[1]={0};};
struct D2DamageStrc{int dwPhysDamage=0;};struct D2DifficultyLevelsTxt{int dwMonsterCEDmgPercent=100;};struct D2MonStatsInitStrc{int nMaxHP=0;};
constexpr int STATEMASK_BOSSSTAYDEATH=0,STATEMASK_MONSTAYDEATH=1,STATEMASK_PLRSTAYDEATH=2;
int hoverFrees=0,refreshes=0,eventDeletes=0,lootAttempts=0,deathEvents=0,ownerChanges=0;bool arrayPresent=true,boss=false;
D2AiControlStrc* AIGENERAL_GetAiControlFromUnit(D2UnitStrc* u){return u?u->ai:nullptr;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &monster;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &model;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* u){return u?u->room:nullptr;}
void UNITROOM_RefreshUnit(D2UnitStrc*){refreshes++;}
uint32_t* D2COMMON_STATES_GetListGfxFlags_6FDB8AC0(D2UnitStrc* u){return arrayPresent?u->gfx:nullptr;}
uint32_t* D2COMMON_STATES_GetStatFlags_6FDB8A90(D2UnitStrc* u){return arrayPresent?u->state:nullptr;}
bool MONSTERS_IsBoss(D2MonStatsTxt*,D2UnitStrc*){return boss;}
bool STATES_CheckStateMaskHideOnUnit(D2UnitStrc* u){return u->hide;}
void CHAT_FreeHoverMsg(void*,void* value){assert(value);hoverFrees++;}
void D2GAME_TARGETS_Last_6FC40380(D2GameStrc*,D2UnitStrc*){}
void PATH_SetUnitDeadCollision(D2UnitStrc* u,int value){assert(value==1);u->stats[30]++;}
void PATH_SetDirection(D2PathStrc* p,DWORD d){assert(p);p->direction=d;}
void MONSTER_DeleteEvents(D2GameStrc*,D2UnitStrc*){eventDeletes++;}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc* u,int mode){assert(mode==0||mode==12);u->dwAnimMode=mode;}
int COLLISION_CheckMask(D2ActiveRoomStrc*,int,int,unsigned){assert(false);return 0;}
void FOG_DisplayAssert(const char*,const char*,int){assert(false);}
void sub_6FC631B0(D2GameStrc*,D2UnitStrc*,int,D2ModeChangeStrc*){lootAttempts++;assert(false);}
int MONSTER_CheckSummonerFlag(D2UnitStrc*,unsigned){return 1;}
int MONSTER_GetLevelId(D2UnitStrc*){return 0;}
int STATLIST_GetUnitAlignment(D2UnitStrc*){return 1;}
void SUNITEVENT_Trigger(D2GameStrc*,int e,D2UnitStrc*,int,int){assert(e==UNITEVENT_DEATH);deathEvents++;}
void MONSTER_Reinitialize(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}void MONSTER_UpdateAiCallbackEvent(D2GameStrc*,D2UnitStrc*){assert(false);}
void SUNITDMG_KillMonster(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void AIGENERAL_AllocMinionList(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);}
int CLIENTS_GetUnitX(D2UnitStrc*){assert(false);return 0;}int CLIENTS_GetUnitY(D2UnitStrc*){assert(false);return 0;}
D2UnitStrc* sub_6FD11420(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){assert(false);return nullptr;}
D2DifficultyLevelsTxt* DATATBLS_GetDifficultyLevelsTxtRecord(int){assert(false);return nullptr;}
void DATATBLS_CalculateMonsterStatsByLevel(int,int,int,int,int,D2MonStatsInitStrc*){assert(false);}
int ITEMS_RollLimitedRandomNumber(int*,int){assert(false);return 0;}
void SUNITDMG_SetMissileDamageFlagsForNearbyUnits(D2GameStrc*,D2UnitStrc*,int,int,int,D2DamageStrc*,int,int,void*,int){assert(false);}
void sub_6FC64090(D2GameStrc*,D2UnitStrc*){assert(false);}void sub_6FC63FD0(D2GameStrc*,D2UnitStrc*){assert(false);}
'''
    # The unreachable death-damage branch still needs the source seed field in the declared ABI.
    base=base.replace('int dwClassId=410,','int pSeed=0,dwClassId=410,')
    ai='source/D2Game/src/AI/AiGeneral.cpp';util='source/D2Game/src/AI/AiUtil.cpp';mode='source/D2Game/src/MONSTER/MonsterMode.cpp';common='source/D2Game/src/UNIT/SUnit.cpp'
    for relative,name in [(common,'SUNIT_GetUnitList'),(common,'SUNIT_GetServerUnit'),(common,'SUNIT_GetOwner'),(ai,'AIGENERAL_GetMinionOwner'),(util,'AIUTIL_CheckAiControlFlag'),(util,'AIUTIL_ToggleAiControlFlag'),(ai,'AIGENERAL_AllocAiControl'),(ai,'AIGENERAL_SetOwnerData'),(ai,'AIGENERAL_UpdateMinionList'),('source/D2Common/src/D2States.cpp','STATES_UpdateStayDeathFlags'),('source/D2Game/src/MONSTER/MonsterRegion.cpp','sub_6FC68240'),('source/D2Common/src/D2Dungeon.cpp','DUNGEON_SaveKilledUnitGUID'),(mode,'D2GAME_MONSTERMODE_Unk_6FC63040'),(mode,'sub_6FC63E80'),(mode,'sub_6FC63B30'),(mode,'sub_6FC641D0')]:base+=routine(relative,name)
    base+=r'''
int main(){printf("[");bool first=true;
for(int inherited:{0,17,1000})for(int flags:{0,1,3})for(int hover:{0,1})for(int deadcol:{0,1})for(int hide:{0,1})for(int arrays:{0,1})for(int bossControl:{0,1})for(int deathIndex:{0,3})for(int direction:{0,7}){
 game={};owner={};pet={};root={};room={};region={};monster={};model={};D2PathStrc path;int text=7;D2MonsterRegionStrc* regions[]={&region};game.pMonReg=regions;
 owner.dwUnitType=0;owner.dwUnitId=1;pet.dwUnitId=17;pet.dwUnitType=1;pet.room=&room;pet.pDynamicPath=&path;pet.pHoverText=hover?&text:nullptr;pet.dwFlags=0xFFFFFFFFu;pet.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;pet.dwOwnerType=0;pet.dwOwnerGUID=1;pet.hide=hide;
 pet.ai=AIGENERAL_AllocAiControl(&game);AIGENERAL_SetOwnerData(&game,&pet,1,0,0,0);assert(pet.ai->nAiFlags==0&&pet.ai->pMinionList==nullptr);pet.ai->nAiFlags=flags;
 game.pUnitList[0][1]=&owner;game.pUnitList[1][17]=&pet;room.nCurrentDeathIndex=deathIndex;
 root.values=pet.stats;root.dwFlags=STATLIST_EXTENDED;pet.pStatListEx=&root;rank=21;freed=0;owner.stats[20]=inherited;owner.stats[21]=-17;owner.stats[22]=25;pet.stats[STAT_MAXHP]=25600;
 skill={};for(int&i:skill.nPassiveStat)i=-1;for(int&i:skill.wAuraStat)i=-1;for(int&i:skill.wSumSkill)i=-1;
 for(int i=0;i<3;i++){skill.nPassiveStat[i]=5+i;skill.dwPassiveCalc[i]=i;skill.wSumSkill[i]=100+i;skill.dwSumSkCalc[i]=12+i;}
 D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,262,21,1);
 D2StatListStrc temporary,protectedList;temporary.pPrevLink=&protectedList;protectedList.dwFlags=STATLIST_BASIC;root.pMyLastList=&temporary;
 for(int i=0;i<4;i++){pet.state[i]=0xF0F0F0F0u;pet.gfx[i]=0;sgptDataTables->fStateMasks[0][i]=0xAAAAAAAAu;sgptDataTables->fStateMasks[1][i]=0x55555555u;}
 arrayPresent=arrays;boss=bossControl;model.dwFlags=deadcol?2:0;hoverFrees=refreshes=eventDeletes=lootAttempts=deathEvents=0;
 D2ModeChangeStrc change{&pet,{direction}};int entered=sub_6FC63B30(&game,&change);unsigned afterFlags=pet.dwFlags;
 bool entryOwner=SUNIT_GetOwner(&game,&pet)==&owner&&AIGENERAL_GetMinionOwner(&pet)==&owner;
 sub_6FC641D0(&game,&pet);bool endOwner=SUNIT_GetOwner(&game,&pet)==&owner&&AIGENERAL_GetMinionOwner(&pet)==&owner;
 assert(entered==1&&pet.dwAnimMode==12&&SUNIT_GetServerUnit(&game,1,17)==&pet&&entryOwner&&endOwner);
 assert(pet.stats[5]==inherited&&pet.stats[6]==-17&&pet.stats[7]==25&&skills[0]==21&&skills[1]==3&&skills[2]==4);
 assert(temporary.freed&&!protectedList.freed&&freed==1&&hoverFrees==hover&&eventDeletes==1&&lootAttempts==0&&deathEvents==1&&path.direction==direction);
 assert(pet.stats[30]==!deadcol&&room.nLastDeadGUIDs[deathIndex]==17&&room.nCurrentDeathIndex==((deathIndex+1)&3));
 unsigned expectedFlags=0xFFFFFFFFu&0xFFFF7FF3u;if(hide)expectedFlags&=~UNITFLAG_TARGETABLE;assert(afterFlags==expectedFlags);
 unsigned mask=boss?0xAAAAAAAAu:0x55555555u;assert(pet.state[0]==(arrays?(0xF0F0F0F0u&mask):0xF0F0F0F0u));
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%d,%d]",first?"":",",inherited,flags,hover,deadcol,hide,arrays,bossControl,deathIndex,direction,afterFlags,pet.stats[5],pet.stats[6],pet.stats[7],freed,int(entryOwner),int(endOwner),pet.dwAnimMode);first=false;delete pet.ai;
}printf("]\n");}
'''
    path=STAGE/'sentry-death-entry-source-probe.cpp';path.write_text(base);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sentry-death-entry-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    data=json.loads(run.stdout);assert len(data)==1152
    fixture=STAGE/'sentry-death-entry-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',nativePolicyChanged=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutineHashes=routines,pinnedMonstatsSha256=digest,actualUnitFlagValues=constants,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data)),
        established=['Complete regular death entry and cleanup, AI ownership helpers, common stat cleanup, StayDeath state flags, region bookkeeping, killed-GUID history and death-end bodies execute together on one modeled unit.',
                     'The inherited root fire stats and assigned skills survive common cleanup and both ownership paths still resolve the registered caster after entry and dead mode12. Actual unit flag values distinguish dwFlags cleanup from the separate dwFlagEx owner-info bit.',
                     'Complete AI allocation starts flags0/list-null, and SetOwnerData with0/0 leaves those flags unchanged. Empty-child-list controls for flags1/3 also preserve this actor own caster link; update-minion-list is not an unconditional owner clear.',
                     'Regular SplEndDeath0 completion does not unlink this actor. Killed-unit GUID bookkeeping writes a four-slot room history and is not registry deletion. The complete death loot wrapper returns immediately for retained UNITFLAG_NOTC; animation scheduling/timing is not supplied by this test.'],
        services=['Typed registration, zeroed allocation, root stat storage, nonextended attachment unlink/free, state-array access, source AI-control access and regular-friendly region inputs are explicit fixtures. Owner lookup and the transition bodies execute unchanged.',
                  'Combat-mode assignment, chat/target/path/event services and empty death-event subscribers are checked boundaries. Outer mode-change animation scheduling is separate. Nonempty child-minion graphs, death-damage monsters, special death-end modes and other event consumers assert through unused services.',
                  'No final corpse/GUID lifetime, room-unload compression/removal, current executable timing or native retired-actor damage policy is certified. Existing native living-actor gates are unchanged; no live immortal or player-substituted damage proxy is introduced.'],
        sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out=ROOT/'tools/reference/sentry-death-entry-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Sentry death entry evidence stale'
    else:out.write_text(encoded)
    print('Sentry death entry:1152 full cleanup/owner/state/death-end contexts; root fire stats and registered owner links survive; scheduling/removal lifetime remain explicit boundaries.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
