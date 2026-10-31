#!/usr/bin/env python3
"""Execute complete barrier start/do and shared summon callbacks with declared world services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'bone-barrier-cast-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    nec=source('source/D2Game/src/SKILLS/SkillNec.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');spawn=source('source/D2Game/src/MONSTER/MonsterSpawn.cpp');units=source('source/D2Game/src/UNIT/SUnit.cpp')
    for text,name in [(skills,'D2GAME_SummonPet_6FD14430'),(nec,'SKILLS_SrvSt19_BonePrison'),(nec,'SKILLS_SrvDo060_BoneWall'),(nec,'SKILLS_SrvDo062_BonePrison')]:bodies[name]=function(text,name)
    reviewed={name:function(text,name) for text,name in [(skills,'D2GAME_SKILLS_Handler_6FD12BA0'),(skills,'sub_6FD12950'),(skills,'sub_6FD12FD0'),(skills,'D2GAME_SKILLMANA_Consume_6FD10A50'),(units,'SUNIT_CanAllyBeTargetedBySkill'),(spawn,'D2GAME_SpawnMonster_6FC69F10'),(spawn,'D2GAME_SpawnNormalMonster_6FC68E30')]}
    assert reviewed['D2GAME_SpawnNormalMonster_6FC68E30'].split('//')[0].strip().endswith('return nullptr;')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';manifest=json.loads((refs.parent/'sources.json').read_text())['files'];tables={}
    for name in ['skills','missiles']:
        path=refs/(name+'.json');assert sha(path)==manifest['json/'+name+'.json']['sha256'];inputs[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
    wall=tables['skills']['78'];prison=tables['skills']['88'];maker=next(r for r in tables['missiles'].values() if r['Missile']=='bonewallmaker')
    assert wall['srvdofunc']==60 and not wall.get('srvstfunc') and wall['summode']=='S1' and wall['mana']==17 and wall['srvmissilea']=='bonewallmaker' and wall['Param3']==8 and wall.get('Param4',0)==0
    assert prison['srvdofunc']==62 and prison['srvstfunc']==19 and prison['summode']=='NU' and prison['mana']==27 and prison['lvlmana']==-1 and prison['SearchEnemyNear']==1
    assert maker['*ID']==207 and maker['pSrvDoFunc']==13
    for row in [wall,prison]:assert not any(v for k,v in row.items() if k.lower() in ['targetableonly','targetally','targetpet','usemanaondo'])
    cpp=r'''#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define D2_ASSERT assert
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_ISREVIVE=1,UNITFLAG_NOTC=2,UNITFLAG_SKSRVDOFUNC=4,AISPECIALSTATE_NONE=0,STATE_UNINTERRUPTABLE=1,EVENTTYPE_AITHINK=2,MONUMOD_KILLMINIONSDEATH=15;
struct D2ActiveRoomStrc{}room;
struct D2UnitStrc{int dwUnitType=0,dwUnitId=1,dwFlags=0,x=100,y=100;void*pDynamicPath=nullptr;}owner,target;
std::array<D2UnitStrc,32> pets;
struct D2GameStrc{int dwGameFrame=1000;}game;
struct D2SkillsTxt{int nPetType=0,dwPetMax=13,dwCalc[2]={11,12},wSrvMissileA=207;}skill;
struct Tables{int nPetTypeTxtRecordCount=20,nMissilesTxtRecordCount=500;}tables;auto*sgptDataTables=&tables;
struct D2SummonArgStrc{int dwFlags=0,nPetType=0,nMonMode=0,nHcIdx=344,nPetMax=0,nAiSpecialState=0;struct{int nX=0,nY=0;}pPosition;D2UnitStrc*pOwner=nullptr;};
struct D2MissileStrc{int nX=0,nY=0,dwFlags=0,nSkill=0,nSkillLevel=0,nMissile=0,nTargetX=0,nTargetY=0;D2UnitStrc*pOwner=nullptr;};
int targetPresent=1,roomOk=1,town=0,failCount=0,spawnCalls=0,created=0,passives=0,missiles=0,rootOwner=-1;bool coordinateOk=true;
std::vector<int> parents;
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){assert(id==78||id==88);return &skill;}
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return targetPresent?&target:nullptr;}
D2ActiveRoomStrc*UNITS_GetRoom(D2UnitStrc*){return &room;}
int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
D2ActiveRoomStrc*D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*,int,int){return roomOk?&room:nullptr;}
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int*x,int*y){*x=108;*y=100;return coordinateOk;}
int CLIENTS_GetUnitX(D2UnitStrc*p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc*p){return p->y;}
int D2COMMON_10175_PathGetFirstPointX(void*){assert(false);return 0;}int D2COMMON_10176_PathGetFirstPointY(void*){assert(false);return 0;}
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*,int,int id,int,int*mode,int,int){*mode=id==78?8:1;return 344;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int,int){return f==12?8:0;}
D2UnitStrc*D2GAME_SpawnMonster_6FC69F10(D2GameStrc*,D2ActiveRoomStrc*,int x,int y,int id,int,int radius,int flags){assert(id==344&&flags==66&&(radius==-1||radius==4));spawnCalls++;if(failCount<0||spawnCalls<=failCount)return nullptr;auto*p=&pets[created++];*p={};p->dwUnitType=UNIT_MONSTER;p->dwUnitId=100+created;p->x=x;p->y=y;return p;}
void sub_6FD146D0(D2GameStrc*,D2UnitStrc*){assert(false);}void D2GAME_SetNecropetFlag_6FCBD760(D2UnitStrc*,int){assert(false);}
void sub_6FC7D7A0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){}
void AIGENERAL_SetOwnerData(D2GameStrc*,D2UnitStrc*p,int id,int,int,int){if(p->dwUnitId==101)rootOwner=id;}
void AIGENERAL_AllocMinionList(D2GameStrc*,D2UnitStrc*root,D2UnitStrc*){parents.push_back(root->dwUnitId);}
int STATES_CheckState(D2UnitStrc*,int){return 0;}void*AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){return nullptr;}
void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,void*,int){}void MONSTER_UpdateAiCallbackEvent(D2GameStrc*,D2UnitStrc*){}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){}void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}
void D2GAME_BOSSES_AssignUMod_6FC6FF10(D2GameStrc*,D2UnitStrc*,int mod,int){assert(mod==15);}
int D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int,int){passives++;return 1;}
void sub_6FC40280(D2GameStrc*,D2UnitStrc*,int,int){}
D2UnitStrc*MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*){missiles++;return &target;}
void MISSILE_SetTargetX(D2UnitStrc*,int){}void MISSILE_SetTargetY(D2UnitStrc*,int){}void UNITS_StoreOwner(D2UnitStrc*,D2UnitStrc*){}
int PATH_ComputeDirection(int,int,int,int){return 0;}void PATH_SetDirection(void*,int){}
'''
    cpp+='\n'.join(bodies.values())
    cpp+=r'''int main(){bool first=true;printf("[");for(int id:{78,88})for(int present:{0,1})for(int roomValue:{0,1})for(int townValue:{0,1})for(int fail:{-1,0,1,2,5,24})for(int coordinates:{0,1}){
owner={};target={};target.x=108;target.y=100;targetPresent=present;roomOk=roomValue;town=townValue;failCount=fail;coordinateOk=coordinates;spawnCalls=created=passives=missiles=0;rootOwner=-1;parents.clear();
int start=id==88?SKILLS_SrvSt19_BonePrison(&game,&owner,id,1):1;
int result=id==78?SKILLS_SrvDo060_BoneWall(&game,&owner,id,1):SKILLS_SrvDo062_BonePrison(&game,&owner,id,1);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",id,present,roomValue,townValue,fail,coordinates,start,result,spawnCalls,created,passives,missiles,rootOwner);first=false;
for(int i=0;i<created;i++)printf("%s[%d,%d]",i?",":"",pets[i].x-108,pets[i].y-100);printf("]] ");
}printf("]\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';r=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True);(STAGE/'compiler.log').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stderr.decode()[-3000:]
    r=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode()[-3000:]
    rows=json.loads(r.stdout);assert len(rows)==192;(STAGE/'cases.json').write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(rows),inputs={**inputs,str(Path(__file__).relative_to(ROOT)):sha(Path(__file__))},executed={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},reviewedNotExecuted={n:hashlib.sha256(b.encode()).hexdigest() for n,b in reviewed.items()},artifacts={str(x.relative_to(ROOT)):sha(x) for x in [p,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Complete do60/do62/start19 and shared summon callbacks execute192 bounded world-service contexts. Wall fails without a root; Prison returns1 even with zero allocations. Exact creation retries parameter4; first successful Prison actor owns later siblings in original source order.',
            'Start19 requires a selected unit outside town, not hostile alignment. Global TARGETABLEONLY/TARGETALLY/target flags still require separate mapping. The global handler charges successful do skills without a start callback at effect; Prison start19 can charge earlier. Native shared release settlement and modern rank1 mana-table value remain separate from the legacy consume quirk.'],
        remaining=['The pinned low-level SpawnNormalMonster is an executable null stub with commented decompile, not recovered search code. Native fallback reuses the existing Druid placement projection; exact D2 room/footprint/RNG/candidate ordering remains uncertain.',
            'Declared allocation,room,target,pet registration,birth,missile and direction services are not the complete engine. Source start/handler/payment bodies are reviewed; only four listed callbacks execute. Current executable, charged/Blood Mana, invalid targetable/item/corpse mapping, empty-ground SearchEnemyNear and windup cancellation remain separate.'])
    out=ROOT/'tools/reference/bone-barrier-cast-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==render
    else:out.write_text(render)
    print('Barrier casting:192 complete callback contexts; low-level spawn stub and payment-phase/current-engine gaps explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
