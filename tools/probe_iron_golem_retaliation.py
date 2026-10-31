#!/usr/bin/env python3
"""Execute complete older flat-return and same-state replacement routines with declared services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={};routines={};bodies={}
    for relative,names in [
        ('source/D2Game/src/SKILLS/SkillItem.cpp',['SKILLITEM_EventFunc06_AttackerTakesDamage']),
        ('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10EC0']),
        ('source/D2Common/src/D2StatList.cpp',['D2Common_GetStateFromStatListEx_6FDB8190','STATLIST_GetStatListFromUnitAndState'])]:
        raw=(BASE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest()
        for name in names:
            body=function(raw.decode(),name);bodies[name]=body;routines[name]=hashlib.sha256(body.encode()).hexdigest()
    reference=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((reference/'sources.json').read_text());tables={};pins={}
    for name in ['skills','states','itemstatcost']:
        p=reference/'json'/(name+'.json');assert sha(p)==manifest['files']['json/'+name+'.json']['sha256'];pins[name]=sha(p);tables[name]=json.loads(p.read_text())
    iron=tables['skills']['90'];thorns=tables['skills']['103'];stat=tables['itemstatcost']['item_attackertakesdamage']
    assert iron['aurastat1']==thorns['aurastat1']=='item_attackertakesdamage' and iron['aurastate']==thorns['auratargetstate']=='thorns'
    assert iron['aurastatcalc1']=='edmn' and iron['HitShift']==8 and [iron['EMin']]+[iron['EMinLev'+str(i)] for i in range(1,6)]==[15,6,12,18,24,30]
    assert not iron.get('passivestate') and not any(iron.get('sumskill'+str(i)) for i in range(1,6))
    assert stat['*ID']==78 and stat['itemevent1']=='attackedinmelee' and stat['itemeventfunc1']==6
    assert tables['states']['thorns']['*ID']==36
    cpp=r'''
#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_CANBEATTACKED=1,STAT_CURSE_RESISTANCE=109,STATE_ATTRACT=57;
constexpr int MONSTATSFLAGINDEX_NPC=0,MONSTATS2FLAGINDEX_ISATT=0,EVENTTYPE_REMOVESTATE=0;
constexpr uint32_t gdwBitMasks[]={1,2};
struct D2GameStrc{int dwGameFrame=123;}game;
struct D2StatListStrc{uint32_t Flags=0;int dwStateNo=0;D2StatListStrc*pPrevLink=nullptr;int Skill=0,Rank=0,Expire=0;};
struct D2StatListExStrc:D2StatListStrc{D2StatListStrc*pMyLastList=nullptr,*pMyStats=nullptr;}root;
struct D2UnitStrc{int dwUnitType=1,dwFlags=1,dwClassId=291,dwUnitId=17;void*pMemoryPool=nullptr;D2StatListExStrc*pStatListEx=&root;int flat=0;}source,target;
using Remove=void(*)(D2UnitStrc*,int32_t,D2StatListStrc*);
struct D2CurseStrc{D2UnitStrc*pUnit,*pTarget;int nSkill,nSkillLevel,nDuration,nStat,nStatValue,nState;Remove pStateRemoveCallback=nullptr;};
struct D2StatesTxt{int nStateFlags[1]={0};}state;
struct D2MonStatsTxt{int dwMonStatsFlags=0;}monster;
struct D2MonStats2Txt{int dwFlags=1;}model;
struct D2DamageStrc{uint16_t wResultFlags=0;int dwPhysDamage=0,dwHitClass=0;};
struct Call{int kind,a=0,b=0,c=0;};std::vector<Call>calls;
D2StatListStrc original,allocated;int frees,allocations,eventFrame,getterStat,getterLayer;
bool STATLIST_IsExtended(D2StatListStrc*p){return(p->Flags&0x80000000u)!=0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int stat,uint16_t layer){getterStat=stat;getterLayer=layer;return p->flat;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int mode,D2DamageStrc*p){calls.push_back({0,mode,p->dwPhysDamage,p->wResultFlags});}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*p){calls.push_back({1,p->dwHitClass,p->dwPhysDamage,p->wResultFlags});}
D2GameStrc*SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
D2StatesTxt*SKILLS_GetStatesTxtRecord(int id){return id==36?&state:nullptr;}
D2MonStatsTxt*MONSTERMODE_GetMonStatsTxtRecord(int){return &monster;}
D2MonStats2Txt*MONSTERREGION_GetMonStats2TxtRecord(int){return &model;}
int STATES_CheckStateMaskCurseByStateId(int){return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}
int MONSTERUNIQUE_CalculatePercentage(int a,int b,int c){return(int)((int64_t)a*b/c);}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
D2StatListStrc*STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc*,int){return nullptr;}
int STATLIST_GetState(D2StatListStrc*p){return p->dwStateNo;}
int STATLIST_GetSkillId(D2StatListStrc*p){return p->Skill;}
int STATLIST_GetSkillLevel(D2StatListStrc*p){return p->Rank;}
void STATLIST_SetExpireFrame(D2StatListStrc*p,int frame){p->Expire=frame;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int frame,int,int){eventFrame=frame;}
void D2Common_10474(D2UnitStrc*,D2StatListStrc*p){if(root.pMyStats==p)root.pMyStats=p->pPrevLink;if(root.pMyLastList==p)root.pMyLastList=p->pPrevLink;}
void STATLIST_FreeStatList(D2StatListStrc*){frees++;}
void UNITROOM_RefreshUnit(D2UnitStrc*){}
void STATES_ToggleState(D2UnitStrc*,int,int){}
bool STATES_CheckStateMaskExpByStateId(int){return false;}
D2StatListStrc*STATLIST_AllocStatList(void*,uint32_t flags,int expire,int,int){allocations++;allocated={};allocated.Flags=flags;allocated.Expire=expire;return &allocated;}
void STATLIST_SetState(D2StatListStrc*p,int value){p->dwStateNo=value;}
void STATLIST_SetSkillId(D2StatListStrc*p,int value){p->Skill=value;}
void STATLIST_SetSkillLevel(D2StatListStrc*p,int value){p->Rank=value;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int,int,int){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*p,int){p->pPrevLink=root.pMyStats;root.pMyStats=p;}
void sub_6FD10E50(D2UnitStrc*,int32_t,D2StatListStrc*){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,Remove){}
'''
    for name in ['D2Common_GetStateFromStatListEx_6FDB8190','STATLIST_GetStatListFromUnitAndState','sub_6FD10EC0','SKILLITEM_EventFunc06_AttackerTakesDamage']:cpp+='\n'+bodies[name]+'\n'
    cpp+=r'''
int main(){printf("{\"events\":[");bool first=true;
for(int a:{0,1})for(int b:{0,1})for(int attackable:{0,1})for(int flat:{-1,0,15,225,7215})for(int layer:{0,65535}){
source.flat=flat;target.dwFlags=attackable;calls.clear();getterStat=getterLayer=-1;int packed=(78<<16)|layer;
int result=SKILLITEM_EventFunc06_AttackerTakesDamage(&game,0,a?&source:nullptr,b?&target:nullptr,nullptr,packed,0);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",a,b,attackable,flat,layer,result,getterStat,getterLayer);first=false;
for(unsigned i=0;i<calls.size();i++){auto c=calls[i];printf("%s[%d,%d,%d,%d]",i?",":"",c.kind,c.a,c.b,c.c);}printf("]]");}
printf("],\"replacement\":[");first=true;
for(int oldSkill:{0,103})for(int rank:{1,20,21})for(int duration:{0,6}){
root={};root.Flags=0x80000000u;original={};original.dwStateNo=36;original.Skill=oldSkill;original.Rank=oldSkill?20:0;original.Expire=oldSkill?130:0;root.pMyStats=&original;
frees=allocations=eventFrame=0;D2CurseStrc c{&source,&target,103,rank,duration,-1,0,36};auto*list=sub_6FD10EC0(&c);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",oldSkill,rank,duration,list?1:0,frees,allocations,list?list->Skill:0,list?list->Rank:0,list?list->Expire:0,eventFrame);first=false;}
printf("]}\n");}
'''
    generated=BASE/'iron-golem-retaliation-source-probe.cpp';generated.write_text(cpp)
    binary=generated.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],check=True)
    process=subprocess.run([str(binary)],capture_output=True);(BASE/'iron-golem-retaliation-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode==0 and not process.stderr,process.stderr.decode()[-2000:]
    rows=json.loads(process.stdout);assert len(rows['events'])==80 and len(rows['replacement'])==12
    for a,b,attackable,flat,layer,result,stat_id,actual_layer,calls in rows['events']:
        queried=a and b and attackable;emitted=queried and flat>0
        assert result==int(emitted) and stat_id==(78 if queried else -1) and actual_layer==(layer if queried else -1)
        assert calls==([[0,1,flat*256,0x4021],[1,0x8d,flat*256,0x4021]] if emitted else [])
    for old,rank,duration,result,frees,alloc,skill,level,expire,event in rows['replacement']:
        if old==103 and rank<20:assert [result,frees,alloc,skill,level,expire,event]==[0,0,0,0,0,0,0]
        elif old==103 and rank==20:assert [result,frees,alloc,skill,level,expire,event]==[1,0,0,103,20,129 if duration else 130,129 if duration else 0]
        else:assert [result,frees,alloc,skill,level,expire,event]==[1,1,1,103,rank,129 if duration else 0,129 if duration else 0]
    rows['ironFlat']=[[rank,15+sum(max(0,min(rank,end)-start)*step for start,end,step in [(1,8,6),(8,16,12),(16,22,18),(22,28,24),(28,255,30)])] for rank in range(1,256)]
    fixture=BASE/'iron-golem-retaliation-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    reviews={}
    for relative,names in [('source/D2Game/src/SKILLS/SkillNec.cpp',['D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/SKILLS/SkillPal.cpp',['SKILLS_AuraCallback_BasicAura']),
        ('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10E50']),
        ('source/D2Common/src/D2Skills.cpp',['SKILLS_RefreshPassiveSkills'])]:
        raw=(BASE/'d2moo-pinned-source'/relative).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest()
        for name in names:reviews[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest()
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,unchangedExecutedRoutineHashes=routines,staticReviewedRoutineHashes=reviews,
        currentTableHashes=pins,generatedSourceSha256=sha(generated),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture),eventCases=80,replacementCases=12,formulaControls=255),
        sanitizers='ASan+UBSan exit0 with empty runtime stderr',
        established=['Complete event6 tests nullable units,target CANBEATTACKED,signed total stat78,and uint16layer narrowing. Positive totals request ExecuteEvents(mode1)before ExecuteMissileDamage with physical FP256,flags0x4021/hitclass0x8D. Dispatch bodies/mitigation are supplied services.',
            'Complete same-state getter/replacement helper replaces an untagged state36list when skill103arrives,regardless of rank. Same skill20rejects lower level,refreshes equal level,and replaces higher level. Birth helper statically creates the untagged aura list;BasicAura callback statically writes its selected values/skill metadata.',
            'Current Iron row supplies flat15at rank1/225at20/7215at255,not percentage reflection. Formula controls evaluate pinned tier inputs outside the compiled source helper;they are not compiled damage-formula proof. Item stat78and distinct Barbs state147remain separate inputs.'],
        remaining=['No complete stat getter,allocation/event engine,mitigation,damage/death/return ordering or actual modern executable is executed. Current-build same-state Iron/Paladin coexistence remains a reference question;the native candidate follows the recorded older replacement rule.',
            'Expiry/removal and passive refresh are static reviews,not executed here. The ordinary callback refreshes passive-state skills;current Iron Golem has no passive-state entry or supplied innate skill on its pet. Native saved replacement history,lifetime/resource invariance,actual melee/ranged/spell events and source packet/HP/death chronology require separate acceptance.'])
    output=ROOT/'tools/reference/iron-golem-retaliation-source-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print('Iron Golem retaliation:80complete event6/12same-state source contexts pass ASan+UBSan;255table-tier controls,not full formula/event-engine proof.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
