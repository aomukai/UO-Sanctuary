#!/usr/bin/env python3
"""Compose Hydra start/do with retained summon/FIFO and full passive helpers."""
import argparse, ast, hashlib, json, re, subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/hydra-creation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True)
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 source_hashes={};bodies={}
 def source(rel,names):
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes()
  assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
  source_hashes[rel]=sha(p)
  for name in names:bodies[name]=function(raw.decode(),name)
 source('source/D2Common/include/D2Monsters.h',[])
 modes=dict(re.findall(r'(MONMODE_\w+),\s*//0x([0-9A-Fa-f]+)',(ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Common/include/D2Monsters.h').read_text()));assert int(modes['MONMODE_SKILL2'],16)==9
 source('source/D2Game/src/PLAYER/PlayerPets.cpp',['sub_6FC7DA40','sub_6FC7D7A0'])
 source('source/D2Game/src/SKILLS/Skills.cpp',['D2GAME_SummonPet_6FD14430','sub_6FD14C30','D2GAME_SetSkills_6FD14C60'])
 source('source/D2Game/src/MONSTER/MonsterUnique.cpp',['MONSTERUNIQUE_CalculatePercentage'])
 source('source/D2Game/src/SKILLS/SkillNec.cpp',['D2GAME_SetSummonPassiveStats_6FD0C530'])
 source('source/D2Game/src/SKILLS/SkillSor.cpp',['SKILLS_SrvSt14_Hydra','SKILLS_SrvDo144_Hydra'])
 # Reuse the trap summon ABI/list services; every canonical body is extracted
 # unchanged. Only the declared fixture services/struct fields are extended.
 parent=ROOT/'tools/probe_assassin_trap_summon_reference.py'
 scaffold=next(n.value.value for n in ast.walk(ast.parse(parent.read_text())) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets) and isinstance(n.value,ast.Constant) and isinstance(n.value.value,str))
 cpp=scaffold.replace('#include <array>','#include <array>\n#include <cstring>')
 cpp=cpp.replace('struct D2UnitStrc{','struct D2AiControlStrc{int value=0;};struct D2MonsterDataStrc{D2AiControlStrc* pAiControl=nullptr;};\nstruct D2UnitStrc{int stats[512]={},skills[400]={},expiry=-1,event=-1,equipment=-1;D2MonsterDataStrc* pMonsterData=nullptr;')
 cpp=cpp.replace('struct D2SkillsTxt{','struct D2SkillsTxt{int dwParam[2]={250,0},nPassiveStat[5]={329,333,189,-1,-1},dwPassiveCalc[5]={100,101,102,0,0},wAuraStat[6]={-1,-1,-1,-1,-1,-1},dwAuraStatCalc[6]={},nAuraState=0,dwCalc[1]={103},wSumSkill[5]={337,36,47,-1,-1},dwSumSkCalc[5]={104,105,106,0,0},wAuraEvent[3]={-1,-1,-1},wAuraEventFunc[3]={},wSumUMod=0,wSumOverlay=0;')
 cpp=cpp.replace('int nMonStatsTxtRecordCount=500,nPetTypeTxtRecordCount=20;','int nMonStatsTxtRecordCount=500,nPetTypeTxtRecordCount=20,nSkillsTxtRecordCount=500,nStatesTxtRecordCount=100,nOverlayTxtRecordCount=500;')
 def line(prefix,replacement):
  nonlocal cpp
  old=[s for s in cpp.splitlines() if s.startswith(prefix)];assert len(old)==1,prefix;cpp=cpp.replace(old[0],replacement)
 # Variables used by the replacement services must precede those services.
 cpp=cpp.replace('int summon=413,',r'''D2UnitStrc heads[3],master;D2AiControlStrc controls[3];D2MonsterDataStrc data[3];
int guard=0,kind=0,rank=1,mask=7,fallback=0,active=0,attempt[3]={},made[3]={},assignCalls=0,alignmentCalls=0,eventCalls=0;
std::vector<int> removals;
int summon=351,''')
 line('int D2GAME_GetSummonIdFromSkill_6FD15580', 'int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*,int,int id,int r,int* mode,int*,int*){assert(id==62 && r==rank);*mode=9;return guard==2?-1:351;}')
 line('int SKILLS_EvaluateSkillFormula', 'int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int f,int id,int r){assert(id==62&&r==rank);switch(f){case 1:return 18;case 100:return p->stats[329];case 101:return p->stats[333];case 102:return p->stats[189];case 103:return 250;case 104:return r;case 105:return p->skills[36];case 106:return p->skills[47];}assert(false);return 0;}')
 line('D2ActiveRoomStrc* UNITS_GetRoom', 'D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return guard==5?nullptr:&room;}')
 line('int D2GAME_GetXAndYFromTargetUnit_6FD14020', 'int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=108;*y=208;return guard!=6;}')
 line('D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070', 'D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc* room,int,int){return room&&guard!=7?room:nullptr;}')
 line('int DUNGEON_IsRoomInTown', 'int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return guard==8;}')
 line('D2UnitStrc* D2GAME_SpawnMonster_6FC69F10', 'D2UnitStrc* D2GAME_SpawnMonster_6FC69F10(D2GameStrc*,D2ActiveRoomStrc*,int x,int y,int id,int mode,int radius,int flags){int i=id-351;assert(i>=0&&i<3&&mode==9&&flags==66);assert(radius==(attempt[i]?4:-1));attempt[i]++;spawnCalls++;active=i;heads[i].x=x;heads[i].y=y;if((fallback&&radius==-1)||!(mask&(1<<i)))return nullptr;made[i]=1;return &heads[i];}')
 line('void AIGENERAL_SetOwnerData', 'void AIGENERAL_SetOwnerData(D2GameStrc*,D2UnitStrc* p,int id,int type,int a,int b){assert(p==&heads[active]&&a==0&&b==0);p->owner=id==owner.dwUnitId?&owner:&master;assert(p->owner->dwUnitType==type);ownerStored++;}')
 line('void* AIGENERAL_GetAiControlFromUnit', 'void* AIGENERAL_GetAiControlFromUnit(D2UnitStrc* p){return p->pMonsterData?p->pMonsterData->pAiControl:nullptr;}')
 line('void EVENT_SetEvent', 'void EVENT_SetEvent(D2GameStrc*,D2UnitStrc* p,int type,int frame,int a,int b){assert(type==EVENTTYPE_AITHINK&&a==0&&b==0);p->event=frame;eventCalls++;}')
 line('int D2GAME_SetSummonPassiveStats_6FD0C530', 'int D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int,int);')
 line('void sub_6FCBDD30', 'void sub_6FCBDD30(D2UnitStrc*,unsigned alignment,int one){assert(alignment==2&&one==1);alignmentCalls++;}')
 line('void PLAYERPETS_RemovePetFromList', 'void PLAYERPETS_RemovePetFromList(D2GameStrc*,D2PetListStrc* l,int id,int kill){assert(l==&list&&l->pPetData->nUnitGUID==id&&kill==1);removals.push_back(id);auto* p=l->pPetData;l->pPetData=p->pNext;delete p;l->nCurrent--;}')
 cpp+=r'''
constexpr int STAT_LEVEL=0,STAT_MAXHP=3,STAT_HITPOINTS=4,SKILLSFLAGINDEX_AURA=0;
using D2C_UnitEventTypes=int;
struct D2ItemStatCostTxt{int wItemEvent[2]={},wItemEventFunc[2]={};}cost;
struct D2StatListStrc{};
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id<0||id==62&&guard==1?nullptr:&row;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p==&owner&&(kind==1||kind==2||kind==4);}
void AITHINK_SetAiControlParams(D2AiControlStrc* c,int a,int b,int d){assert(b==-666&&d==-666);c->value=a;heads[active].expiry=a;}
unsigned STATLIST_GetUnitAlignment(D2UnitStrc*){return 2;}
void SKILLS_RefreshPassiveSkills(D2UnitStrc*){}
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
void SKILLS_AssignSkill(D2UnitStrc* p,int s,int r,int remove,const char*,int){assert(remove==0||s==62&&remove==1);p->skills[s]=r;assignCalls++;}
D2ItemStatCostTxt* SKILLS_GetItemStatCostTxtRecord(int){return &cost;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* p){return p->stats[STAT_MAXHP];}
void STATLIST_SetUnitStat(D2UnitStrc* p,int s,int n,int){p->stats[s]=n;}
void STATLIST_AddUnitStat(D2UnitStrc* p,int s,int n,int){p->stats[s]+=n;}
bool SUNITEVENT_GetEvent(D2GameStrc*,D2UnitStrc*,int,int,int){assert(false);return false;}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){assert(false);}
D2StatListStrc* STATLIST_AllocStatList(void*,int,int,int,int){assert(false);return nullptr;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){assert(false);}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int,int,int){assert(false);}
void STATES_ToggleState(D2UnitStrc*,int,int){assert(false);}
void STATLIST_SetState(D2StatListStrc*,int){assert(false);}
void D2GAME_AssignSkill_6FD13800(D2UnitStrc*,int,int,int){assert(false);}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void D2GAME_BOSSES_AssignUMod_6FC6FF10(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void UNITS_SetOverlay(D2UnitStrc*,int,int){assert(false);}
void sub_6FCF9580(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p,int,int,int level,int){p->equipment=level;}
int D2Clamp(int n,int low,int high){return std::clamp(n,low,high);}
'''
 cpp+='\n'.join(bodies.values())
 cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int k=0;k<5;k++)for(int g=0;g<=9;g++)for(int bits=0;bits<8;bits++)for(int count:{0,16,17,18})for(int r:{0,1,20,255})for(int fb:{0,1}){
kind=k;guard=g;rank=r;mask=bits;fallback=fb;owner={};master={};minion={};owner.dwUnitId=1;master.dwUnitId=2;owner.dwUnitType=k?UNIT_MONSTER:UNIT_PLAYER;master.dwUnitType=k==4?UNIT_MONSTER:UNIT_PLAYER;owner.owner=k==2?nullptr:&master;
for(auto* p:{&owner,&master}){p->stats[STAT_LEVEL]=99;p->stats[329]=p==&owner?77:139;p->stats[333]=p==&owner?17:23;p->stats[189]=p==&owner?1:0;p->skills[36]=p==&owner?7:13;p->skills[47]=p==&owner?11:17;}
row={};row.nPetType=g==3?-1:g==4?20:19;row.dwPetMax=1;fill(18,count);removals.clear();spawnCalls=registered=ownerStored=passiveCalls=assignCalls=alignmentCalls=eventCalls=specialCalls=0;
for(int i=0;i<3;i++){heads[i]={};heads[i].dwUnitType=UNIT_MONSTER;heads[i].dwUnitId=303+i;heads[i].stats[STAT_MAXHP]=100*256;controls[i]={};data[i]={&controls[i]};heads[i].pMonsterData=g==9&&i==1?nullptr:&data[i];attempt[i]=made[i]=0;}
int start=SKILLS_SrvSt14_Hydra(&game,&owner,62,rank);int result=SKILLS_SrvDo144_Hydra(&game,&owner,62,rank);
printf("%s[[%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d],[",first?"":",",kind,guard,mask,count,rank,fallback,start,result,spawnCalls,registered,ownerStored,assignCalls,alignmentCalls,eventCalls);first=false;
for(int i=0;i<3;i++){auto& p=heads[i];printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",made[i],p.x,p.y,p.owner?p.owner->dwUnitId:0,p.owner?p.owner->dwUnitType:-1,p.dwFlags,p.expiry,p.skills[62],p.skills[337],p.skills[36],p.skills[47],p.stats[329],p.stats[333],p.stats[189],p.stats[STAT_MAXHP],p.event);}
printf("],[");for(auto* p=list.pPetData;p;p=p->pNext)printf("%s%d",p==list.pPetData?"":",",p->nUnitGUID);printf("],[");for(size_t i=0;i<removals.size();i++)printf("%s%d",i?",":"",removals[i]);printf("]]");
}printf("]\n");clearList();}
'''
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==12800
 for inputs,out,heads,ids,removed in cases:
  kind,guard,mask,count,rank,fallback=inputs;admit=guard not in range(1,9);start=guard not in (5,6,7,8);n=mask.bit_count() if admit else 0;player=kind in (0,1);substitute=kind in (1,4)
  assert out==[int(start),int(n>0),sum((2 if fallback or not mask&(1<<i) else 1) for i in range(3)) if admit else 0,n if player else 0,n,n*(4 if rank else 3),n if not player else 0,n],(inputs,out)
  expected=list(range(100,100+count));evicted=[]
  for i,head in enumerate(heads):
   made=admit and bool(mask&(1<<i));owner=2 if substitute else 1;typ=0 if player else 1
   if made:
    expiry=1000+(250 if rank else 0) if not(guard==9 and i==1) else -1
    assert head==[1,108+[-1,0,1][i],208+[-1,0,-1][i],owner,typ,3,expiry,rank,rank,13 if substitute else 7,17 if substitute else 11,139 if substitute else 77,23 if substitute else 17,0 if substitute else 1,350*256,1025],(inputs,i,head)
    if player:
     if len(expected)==18:evicted.append(expected.pop(0))
     expected.append(303+i)
   else:assert head[0]==0 and head[3:]==[0,-1,0,-1,0,0,0,0,0,0,0,100*256,-1],(inputs,i,head)
  assert ids==expected and removed==evicted,(inputs,ids,removed,expected,evicted)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True)
 log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text());tables={}
 for name in ('skills','monstats','pettype','itemstatcost'):
  q=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(q)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(q.relative_to(ROOT))]=sha(q);tables[name]=json.loads(q.read_text())
 row=next(r for r in tables['skills'].values() if r.get('skill')=='Hydra');assert row['*Id']==62 and row['srvstfunc']==14 and row['srvdofunc']==144 and row['Param1']==250 and row['Param2']==0 and row['Param3']==18
 assert row['petmax']=='par3' and row['summon']=='hydra1' and row['pettype']=='hydra' and row['summode']=='S2' and row['calc1']=='ln12'
 for i,(name,identity,calc) in enumerate((('HydraMissile',337,'lvl'),('Fire Bolt',36,"skill('Fire Bolt'.blvl)"),('Fire Ball',47,"skill('Fire Ball'.blvl)")),1):
  assert row['sumskill'+str(i)]==name and row['sumsk'+str(i)+'calc']==calc and next(r for r in tables['skills'].values() if r.get('skill')==name)['*Id']==identity
 for i in range(3):assert tables['monstats']['hydra'+str(i+1)]['*hcIdx']==351+i
 assert tables['pettype']['hydra'].get('group',0)==0 and tables['pettype']['hydra']['lineNumber']==19
 for i,key in enumerate(('passive_fire_mastery','passive_fire_pierce','item_pierce_fire_immunity'),1):
  assert row['passivestat'+str(i)]==key and row['passivecalc'+str(i)]==f"stat('{key}'.accr)" and tables['itemstatcost'][key]['*ID']==(329,333,189)[i-1] and not any(v for k,v in tables['itemstatcost'][key].items() if k.lower().startswith('itemevent'))
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=source_hashes,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable=pins,sanitizers='ASan/UBSan clean',
  established=['Complete start14/do144 compose with the retained full generic summon,pet registration/FIFO,assigned-skill refresh,passive helper and percentage body. Requests are independent;partial failures still permit later heads. Explicit coordinates/three consecutive classes,S2mode,revive/no-treasure flags,owner records,frame+25initial AI events and duration AI params are checked.',
  'Hydra pettype19/group0 has an18-head limit. Each successful player-owned head appends independently and evicts one oldest head only when full. A failed head does not evict anything. Hirelings resolve an available master;ordinary monster casters retain themselves even when they have a minion owner. Monster-owned heads do not enter the player pet list and inherit alignment.',
  'The full old passive helper captures three accrued fields and positive HydraMissile/current rank plus hard Fire Bolt/Fire Ball from the resolved owner. Four assigned-skill calls occur per positive-rank head including the base Hydra skill. The old shared calc1percentage also multiplies supplied base life by350percent;this is recorded without asserting modern untargetable-head HP semantics.'],
  declaredServices=['Older engine plus pinned current tables,not modern executable proof. Reused trap ABI/list services are extended;all nine canonical function bodies remain unchanged. Target/room/town,summon ID/row/formula,current accrued/hard-stat values,spawn results,stat storage,skill/event wrappers and client pet-list bridge are declared services.',
  'Spawn first-radius/fallback outcomes and missing AI data are controlled. No actual terrain search,head startup/animation/AI target acquisition,projectile emission/damage/HP/block/events,global RNG,modern scheduling/removal,charged/oskill/client or native persistence is established. Item equipment receives the old helper request only;no invented Hydra gear or incoming HP behavior. No C#/runtime/install/save change.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,p,binary,san,log,fixture)})
 target=ROOT/'tools/reference/hydra-creation-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('Hydra creation:12800complete start/do/summon/FIFO/passive/assigned-skill contexts;partial failures/per-head18cap/owner substitution;ASan/UBSan clean;runtime unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
