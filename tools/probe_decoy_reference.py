#!/usr/bin/env python3
"""Compile Decoy's unchanged do15/base-level/percentage rules with declared services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';REF=ROOT/'tools/reference/d2r-named-equipment'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end];return blocks[name]+'\n'
 do15=routine(source('source/D2Game/src/SKILLS/SkillAma.cpp'),'SKILLS_SrvDo015_Dopplezon')
 nec=source('source/D2Game/src/SKILLS/SkillNec.cpp');base=routine(nec,'D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10');passive=routine(nec,'D2GAME_SetSummonPassiveStats_6FD0C530')
 unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp');percent=routine(unique,'MONSTERUNIQUE_CalculatePercentage');expiry=routine(unique,'sub_6FC6DFC0')
 life=passive[passive.index('    const int32_t nMaxHp ='):passive.index('    for (int32_t i = 0; i < 5;',passive.index('    const int32_t nMaxHp ='))]
 assert 'SKILLS_EvaluateSkillFormula(pUnit, pSkillsTxtRecord->dwCalc[0]' in life and 'STAT_HITPOINTS, nNewMaxHp' in life
 callback=routine(source('source/D2Game/src/ITEMS/ItemMode.cpp'),'D2GAME_ITEMMODE_ServerStatlistCallback_6FC41910')
 maxhp=callback[callback.index('    case STAT_MAXHP:'):callback.index('    case STAT_MAXMANA:')]
 units=source('source/D2Common/src/Units/Units.cpp');routine(units,'UNITS_GetBlockRate')
 routine(source('source/D2Common/src/D2StatList.cpp'),'STATLIST_NotifyUnitOfStatValueChange')
 monster=source('source/D2Game/src/MONSTER/Monster.cpp');assert 'STATLIST_SetUnitStat(pUnit, STAT_HPREGEN, (nShiftedHp * pMonStatsTxtRecord->dwDamageRegen) >> 12, 0);' in monster
 ai=source('source/D2Game/src/AI/AiUtil.cpp');assert 'nThreat = pMonStatsTxtRecord->nThreat;' in ai
 tables={};pins={};manifest=json.loads((REF/'sources.json').read_text())['files']
 for name in ['skills','monstats','monstats2','pettype','monlvl','itemstatcost']:
  p=REF/'json'/f'{name}.json';sha=hashlib.sha256(p.read_bytes()).hexdigest();assert sha==manifest['json/'+name+'.json']['sha256'];pins[name]=sha;tables[name]=json.loads(p.read_text())
 row=tables['skills']['28'];mon=tables['monstats']['dopplezon'];model=tables['monstats2']['dopplezon']
 assert tables['itemstatcost']['maxhp']['fCallback']==1
 assert row['calc1']=='lvl*par4' and row['calc2']=='ln12' and row['calc3']=='par3' and row['petmax']==1 and row['Param3']==50 and row['Param4']==10
 assert mon['AI']=='Idle' and mon['threat']==14 and mon['DamageRegen']==15 and not mon.get('inventory') and not mon.get('NoShldBlock')
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int AISPECIALSTATE_NONE=0,UNITFLAG_SKSRVDOFUNC=1,STAT_HITPOINTS=0,STAT_MAXHP=1,STAT_LEVEL=2,STAT_ARMORCLASS=3,STAT_TOHIT=4,STAT_HPREGEN=5,UNIT_MONSTER=1,EVENTTYPE_MONUMOD=0,STATE_UNINTERRUPTABLE=0,UNIT_PLAYER=0,MONMODE_DEATH=12;
struct D2MonStatsTxt{int dwDamageRegen=15;} monstats;struct MonsterData{D2MonStatsTxt* pMonstatsTxt=&monstats;} monsterData;
struct D2UnitStrc{MonsterData* pMonsterData=&monsterData;int dwFlags=0,dwNodeIndex=23,dwUnitId=5,dwUnitType=0;int stats[6]={0};} owner,pet;
struct D2GameStrc{int dwGameFrame=700,nDifficulty=0,nGameType=1,dwGameType=0;}game;
struct D2SkillsTxt{int nPetType=8,dwPetMax=3,dwCalc[3]={0,1,2};}row;
struct D2SummonArgStrc{int nAiSpecialState=0,nPetType=0,nHcIdx=0,nMonMode=0,nPetMax=0;D2UnitStrc* pOwner=nullptr;} arg;
struct D2MonLvlTxt{int dwLAC[3]={},dwAC[3]={},dwLTH[3]={},dwTH[3]={};};
struct Tables{int nPetTypeTxtRecordCount=20,nMonLvlTxtRecordCount=101;D2MonLvlTxt pMonLvlTxt[101];}tables;auto* sgptDataTables=&tables;
struct D2ModeChangeStrc{};
int rank=1,guard=0,stage=0,eventAt=0,overlay=0,umod=0,aiNode=0,stored=0,removed=0,mode=0;bool unt=false,dead=false;D2UnitStrc* expiryOwner=&owner;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return guard==1?nullptr:&row;}
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*,int,int,int,int* spawn,int,int){*spawn=1;return guard==2?-1:356;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int level){return formula==0?level*10:formula==1?250+125*(level-1):formula==2?50:1;}
D2UnitStrc* D2GAME_SummonPet_6FD14430(D2GameStrc*,D2SummonArgStrc* a){arg=*a;return guard==3?nullptr:&pet;}
void UNITS_StoreOwner(D2UnitStrc*,D2UnitStrc*){stored++;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* p){return p->stats[STAT_MAXHP];}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
void DecoyMaxCallback(D2GameStrc*,D2UnitStrc*,int,int);
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){int old=p->stats[stat];p->stats[stat]=value;if(stat==STAT_MAXHP){DecoyMaxCallback(&game,p,old,value);}}
void STATLIST_AddUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]+=value;}
int D2Clamp(int v,int lo,int hi){return std::clamp(v,lo,hi);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int at,int,int){eventAt=at;}
void D2GAME_BOSSES_AssignUMod_6FC6FF10(D2GameStrc*,D2UnitStrc*,int v,int){umod=v;}
void UNITS_SetOverlay(D2UnitStrc*,int v,int){overlay=v;}
void D2GAME_UpdateSummonAI_6FC401F0(D2GameStrc*,D2UnitStrc*,int,int v){aiNode=v;}
bool SUNIT_IsDead(D2UnitStrc*){return dead;}
bool STATES_CheckState(D2UnitStrc*,int){return unt;}
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc*){return expiryOwner;}
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc*,int v,D2ModeChangeStrc*){mode=v;}
void D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc*,int){}
void sub_6FC7D260(D2GameStrc*,D2UnitStrc*,int,int){removed++;}
'''+percent+'\n'+base+'\nvoid DecoyMaxCallback(D2GameStrc* pGame,D2UnitStrc* pOwner,int nPreviousValue,int nNewValue){switch(STAT_MAXHP){\n'+maxhp+'\n}}\n'+r'''
int D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc* pUnit,D2UnitStrc* pPet,int nSkillId,int nSkillLevel,int){auto* pSkillsTxtRecord=&row;
'''+life+'return 1;}\n'+do15+'\n'+expiry+r'''
int main(){for(int i=0;i<101;i++){tables.pMonLvlTxt[i].dwLAC[0]=6*i;tables.pMonLvlTxt[i].dwLTH[0]=11*i;}bool first=true;std::printf("{\"summons\":[");
for(int level:{1,20,99})for(int hp:{1,255,256,257,25600+37,1048576,1048577,65000*256})for(rank=1;rank<=40;rank++){
 owner={};pet={};pet.dwUnitType=UNIT_MONSTER;pet.stats[STAT_HITPOINTS]=2560;pet.stats[STAT_HPREGEN]=9;owner.stats[STAT_MAXHP]=hp;owner.stats[STAT_LEVEL]=level;pet.stats[STAT_ARMORCLASS]=141;pet.stats[STAT_MAXHP]=10*256;guard=0;stored=overlay=umod=aiNode=eventAt=0;
 int ok=SKILLS_SrvDo015_Dopplezon(&game,&owner,28,rank);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",level,hp,rank,ok,pet.stats[STAT_MAXHP],pet.stats[STAT_LEVEL],pet.stats[STAT_ARMORCLASS],eventAt,arg.nPetMax,overlay,umod,stored,aiNode,pet.stats[STAT_HPREGEN]);}
 std::printf("],\"guards\":[");first=true;for(int g:{1,2,3}){guard=g;stored=0;int ok=SKILLS_SrvDo015_Dopplezon(&game,&owner,28,rank);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",g,ok,stored);}
 std::printf("],\"expiry\":[");first=true;guard=0;
 for(int gone:{0,1})for(int locked:{0,1})for(int kind:{-1,0,1}){dead=gone;unt=locked;expiryOwner=kind<0?nullptr:&owner;owner.dwUnitType=kind;removed=mode=eventAt=0;sub_6FC6DFC0(&game,&pet,21,0);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",gone,locked,kind,eventAt,removed,mode);}
 std::printf("]}\n");}
'''
 p=STAGE/'decoy-source-probe.cpp';p.write_text(cpp);binary=STAGE/'decoy-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in data.items()}==dict(summons=960,guards=3,expiry=12)
 for level,hp,rank,ok,life256,petlevel,defense,at,cap,overlay,umod,stored,node,regen in data['summons']:
  def pct(v,n):return n*(v//100) if v>1048576 and 100<=v>>4 else v*n//100
  initial=pct(hp,50);expected=initial+pct(initial,rank*10);pl=min(max(1,rank+3*level//4),level)
  assert regen==((15*(life256>>8))>>4 if life256>0 else 9)
  assert (ok,life256,petlevel,defense,at,cap,overlay,umod,stored,node)==(1,expected,pl,141+6*pl,700+250+125*(rank-1),1,171,21,1,23)
 assert data['guards']==[[1,0,0],[2,0,0],[3,0,0]]
 for dead,locked,kind,at,removed,mode in data['expiry']:
  assert (at,removed,mode)==((0,0,0) if dead else (703,0,0) if locked else (0,1,0) if kind==0 else (0,0,12))
 fixture=STAGE/'decoy-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,pinnedTableHashes=pins,compiledRoutines=['SKILLS_SrvDo015_Dopplezon','D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10','MONSTERUNIQUE_CalculatePercentage','sub_6FC6DFC0'],compiledMaxHpCallbackExcerptSha256=hashlib.sha256(maxhp.encode()).hexdigest(),compiledPassiveLifeExcerptSha256=hashlib.sha256(life.encode()).hexdigest(),reviewedBlockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},pinnedSkill=row,pinnedMonster=mon,pinnedModel=model,pinnedPet=tables['pettype']['dopplezon'],probe=dict(caseCounts={k:len(v) for k,v in data.items()},fixture=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),
 established=['do15 creates one stationary Idle species356/NU pet, stores owner, sets life from caster maxHP*50percent, then base stats and passive life bonus10percent per effective rank. Duration250+125*(rank-1) frames; rank and life/defense are creation snapshots.',
 'Base summon helper overwrites the earlier caster-level assignment with clamp(rank+3*casterLevel/4,1,casterLevel), then adds game/difficulty monlvl AC/TH. Matrix supplies Normal expansion L-AC=6*level and L-TH=11*level as declared services.',
 'Full unchanged percentage routine has a large-value branch: above1048576 raw life it divides the value by100 before multiplying. Both half-life and later skill bonus use it. Do not silently replace this with one combined percentage or floating multiply.',
 'Expiry UMod21 retries3frames while uninterruptable; dead units do nothing. Otherwise a player owner removes the pet by identity; absent/nonplayer owner requests death. Overlay171 and AI owner node assignment are observed.',
 'Pinned elemental resists are min(rank*4,85), one-pet maximum, right-only SC, fractional mana max(1,(76-3*(rank-1))/4). Model has onlyTR/S1 pieces, Idle AI and threat14. No summon skills or inventory equipment table are present.',
 'Monster initialization starts from initial HP, but the registered maxHP stat callback recalculates HPREGEN when positive life changes. The unchanged maxHP callback excerpt is executed by stat writes in this probe: regen=DamageRegen*(newMaxHP>>8)>>4. Do not infer fixed initial10life regeneration from the absence of a direct write in do15. Full source spawn/stat-list callback wiring and modern order remain acceptance.',
 'Pinned ToBlock28 alone does not prove28percent block. Reviewed GetBlockRate requires a shield component for this species without NoShldBlock; model has no shield component. No player Dodge/Avoid/Evade skills are supplied by the Decoy row.'],
 services=['Summon allocation/placement/replacement, target coordinates, row/formula lookup, unit/stat/event storage, overlay/owner/AI and minion removal are declared recording services. No actual world/collision/AI/casting/mana dispatch is compiled. Maximum-life writes execute the unchanged maxHP callback excerpt; other stat-list dependencies remain services.',
 'Passive helper is represented only by its unchanged life assignment excerpt. Aura resistance/stat lists, item events, summon skills, equipment generation and overlays require native integration separately. Full helper hash is retained; do not claim it compiled.',
 'Source monster base stats, Normal monlvl values and caster maximum are supplied explicitly. Matrix does not execute monster spawning, source RNG/player-count scaling or block functions. It executes the maxHP resource callback excerpt. Pinned maxhp fCallback1 and unchanged stat notification/monster callback registration are reviewed; full dependency wiring is not compiled.'],
 limits=['Older supplementary engine proof is not an exact D2R3.3.93847 executable certificate. Native casting/placement/target acquisition/defense/regen/expiry/lifecycle/persistence and actual client remain to implement and verify.',
 'Source monster size/threat/animation/appearance, exact placement fallback/collision/LOS/range, special uninterruptable expiry and difficulty/game context require declared native adapters or individual gates. No arbitrary forced taunt or inherited Necromancer movement/attack/commands.'])
 target=ROOT/'tools/reference/decoy-engine-evidence.json';rendered=json.dumps(out,indent=2)+'\n'
 if check:assert target.read_text()==rendered,'Decoy evidence changed'
 else:target.write_text(rendered)
 print('Decoy:960 compiled summon/life/level/maxHP-callback,3 guard and12 expiry cases pass; native/current acceptance separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
