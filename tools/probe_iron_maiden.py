#!/usr/bin/env python3
"""Execute the complete Iron Maiden event, including its legacy Blood Golem tail.
Unit/stat/owner/formula and downstream damage services are explicitly supplied.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'iron-maiden-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def percentage(value,scale,divisor=100):
    if value<=1048576:
        if scale<=65536:return value*scale//divisor
        if divisor<=scale>>4:return value*(scale//divisor)
    elif divisor<=value>>4:return scale*(value//divisor)
    return value*scale//divisor

def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={}
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    nec=source('source/D2Game/src/SKILLS/SkillNec.cpp');unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    host=function(source('source/D2Game/src/UNIT/SUnitDmg.cpp'),'SUNITDMG_ExecuteEvents')
    anchors=['SUNITEVENT_Trigger(pGame, UNITEVENT_DOMELEEDMG',
             'SUNITEVENT_Trigger(pGame, UNITEVENT_DAMAGEDINMELEE',
             'pDamage->dwPhysDamage = std::min(pDamage->dwPhysDamage, nHp);']
    positions=[host.index(anchor) for anchor in anchors];assert positions==sorted(positions)
    review=ROOT/'tools/reference/weapon-event-order-review.json'
    assert hashlib.sha256(host.encode()).hexdigest()==json.loads(review.read_text())['functions']['SUNITDMG_ExecuteEvents']
    bodies={'D2GAME_EventFunc04_6FD0E840':function(nec,'D2GAME_EventFunc04_6FD0E840'),
            'MONSTERUNIQUE_CalculatePercentage':function(unique,'MONSTERUNIQUE_CalculatePercentage')}
    tables=ROOT/'tools/reference/d2r-named-equipment/json';skills=tables/'skills.json';states=tables/'states.json'
    row=json.loads(skills.read_text())['76'];state=json.loads(states.read_text())['ironmaiden']
    assert (row['srvdofunc'],row['aurafilter'],row['auraevent1'],row['auraeventfunc1'])==(30,3,'domeleedamage',4)
    assert [row['Param'+str(i)] for i in range(1,7)]==[7,0,300,60,200,25]
    assert [row['calc'+str(i)] for i in range(1,4)]==['ln56','ln56/4','ln56/4']
    assert row['ResultFlags']==16385 and row['HitClass']==141 and row.get('HitFlags',0)==0 and state['*ID']==55
    assert not any(row.get('aurastat'+str(i)) for i in range(1,7))
    cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONSTER_BLOODGOLEM=290,UNIT_ALIGNMENT_EVIL=0,STAT_HITPOINTS=6;
struct D2MonStatsTxt{uint8_t nDrain[3]={100,100,100};}monster;
struct MonsterData{D2MonStatsTxt*pMonstatsTxt=&monster;}mondata;
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,hire=0,hp=2003,max=12005,alignment=0,id=1;bool dead=false;MonsterData*pMonsterData=&mondata;}attacker,victim,lifeOwner,caster;
struct D2GameStrc{int nDifficulty=0;};
struct D2DamageStrc{int32_t dwPhysDamage=0;uint32_t wResultFlags=0,dwHitFlags=0;int dwHitClass=0;};
struct D2SkillsTxt{int wAuraTargetState=55,dwCalc[3]={0,1,2};uint32_t wResultFlags=16385,dwHitFlags=0;int dwHitClass=141;}skill;
struct D2StatListStrc{}list;
struct Tables{int nStatesTxtRecordCount=256;}tables;auto*sgptDataTables=&tables;
int mode=0,ownerMode=0,selected=-1,seenRank=-99,seenOwner=0,raw=0,after=0,flags=0,hitclass=0,trace=0,ownerOverlay=0,attackerOverlay=0;
std::vector<std::pair<int,int>>writes;
bool SUNIT_IsDead(D2UnitStrc*p){return !p||p->dead;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int){return mode==5?nullptr:&skill;}
bool STATES_CheckState(D2UnitStrc*,int){return mode!=8;}
D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return mode==9?nullptr:&list;}
int STATLIST_GetOwnerType(D2StatListStrc*){return 0;}int STATLIST_GetOwnerGUID(D2StatListStrc*){return 7;}
D2UnitStrc*SUNIT_GetServerUnit(D2GameStrc*,int,int){return mode==10?nullptr:&caster;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*p){return p->hire;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*p,int calc,int id,int rank){assert(id==76&&calc>=0&&calc<3);selected=calc;seenRank=rank;seenOwner=p!=nullptr;int value=200+25*(rank-1);return calc?value/4:value;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*p,D2UnitStrc*t,int event,D2DamageStrc*d){assert(p==&victim&&t==&attacker&&event==1);trace=trace*10+1;raw=d->dwPhysDamage;flags=d->wResultFlags;hitclass=d->dwHitClass;assert(d->dwHitFlags==0);if(mode==11)d->dwPhysDamage/=2;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*p,D2UnitStrc*t,D2DamageStrc*d){assert(p==&victim&&t==&attacker);trace=trace*10+2;after=d->dwPhysDamage;}
int STATLIST_GetUnitAlignment(D2UnitStrc*p){return p->alignment;}
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*p){assert(p==&attacker);return ownerMode?&lifeOwner:nullptr;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*p){return p->max;}
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int layer){assert(stat==6&&layer==0);return p->hp;}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int value,int layer){assert(stat==6&&layer==0);p->hp=value;writes.emplace_back(p->id,value);}
void UNITS_SetOverlay(D2UnitStrc*p,int overlay,int layer){assert(overlay==151&&layer==0);if(p==&attacker)attackerOverlay++;else{assert(p==&lifeOwner);ownerOverlay++;}}
'''
    cpp+=bodies['MONSTERUNIQUE_CalculatePercentage']+'\n'+bodies['D2GAME_EventFunc04_6FD0E840']+r'''
bool first=true;
void emit(int tag,int type,int hire,int victimType,int physical,int rank,int guard,int drain,int alignment,int owner,int injury,int data){
attacker={};victim={};lifeOwner={};caster={};attacker.dwUnitType=type;attacker.hire=hire;attacker.dead=guard==3;attacker.id=1;
victim.dwUnitType=victimType;victim.dwClassId=tag?MONSTER_BLOODGOLEM:1;victim.alignment=alignment;victim.pMonsterData=data?&mondata:nullptr;mondata.pMonstatsTxt=data==2?&monster:nullptr;
for(int i=0;i<3;i++)monster.nDrain[i]=drain;lifeOwner.id=0;lifeOwner.max=8001;lifeOwner.hp=injury==0||injury==2?8001:1001;lifeOwner.dead=owner==2;attacker.hp=injury==0||injury==3?12005:2003;
mode=guard;ownerMode=owner;selected=-1;seenRank=-99;seenOwner=raw=after=flags=hitclass=trace=ownerOverlay=attackerOverlay=0;writes.clear();skill.wAuraTargetState=guard==6?-1:guard==7?256:55;
D2GameStrc game;D2DamageStrc damage;damage.dwPhysDamage=physical;
int result=D2GAME_EventFunc04_6FD0E840(&game,17,guard==2?nullptr:&attacker,guard==1?nullptr:&victim,guard==4?nullptr:&damage,76,rank);
printf("%s[%d,[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[",first?"":",",tag,type,hire,victimType,physical,rank,guard,drain,alignment,owner,injury,data,result,selected,seenRank,seenOwner,raw,after,flags,hitclass,trace,lifeOwner.hp,attacker.hp,ownerOverlay,attackerOverlay);
for(size_t i=0;i<writes.size();i++)printf("%s[%d,%d]",i?",":"",writes[i].first,writes[i].second);printf("]]");first=false;
}
int main(){printf("[");
for(int type:{0,1,2})for(int hire:{0,1})for(int victim:{0,1,2})for(int physical:{0,1,255,256,257,511,10000,1048577})for(int rank:{-1,0,1,20,255})for(int guard=0;guard<12;guard++)emit(0,type,hire,victim,physical,rank,guard,100,0,1,1,2);
for(int type:{0,1})for(int hire:{0,1})for(int physical:{257,25600,1048577})for(int rank:{1,20,255})for(int drain:{0,50,100})for(int alignment:{0,1})for(int owner:{0,1,2})for(int injury:{0,1,2,3})for(int data:{0,1,2})for(int guard:{0,11})emit(1,type,hire,1,physical,rank,guard,drain,alignment,owner,injury,data);
printf("]\n");}
'''
    STAGE.mkdir(parents=True,exist_ok=True);src=STAGE/'probe.cpp';src.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(src),'-o',str(STAGE/'probe')],capture_output=True)
    (STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    rows=json.loads(run.stdout);counts=[0,0]
    for tag,values,actual,writes in rows:
        typ,hire,victim,physical,rank,guard,drain,alignment,owner,injury,data=values
        owner_hp=8001 if injury in [0,2] else 1001;attacker_hp=12005 if injury in [0,3] else 2003
        accept=typ in [0,1] and physical>0 and guard not in range(1,10)
        expected_writes=[];owner_overlay=attacker_overlay=0
        if accept:
            normalized=max(1,rank) if rank<0 else rank
            calc=(1 if victim==1 else 2) if typ==0 or hire else 0
            percent=200+25*(normalized-1);percent=percent//4 if calc else percent
            raw=percentage(physical,percent);after=raw//2 if guard==11 else raw
            if tag and data==2 and drain and alignment==0 and owner!=2:
                heal=percentage(percentage(after,drain) if drain!=100 else after,20)
                if heal>0:
                    if owner:
                        share=percentage(heal,50)
                        if share>0:
                            next_owner=min(8001,owner_hp+share);share=next_owner-owner_hp;owner_hp=next_owner;expected_writes.append([0,owner_hp])
                        heal-=share
                    used=0
                    if heal>0:
                        next_attacker=min(12005,attacker_hp+heal);used=next_attacker-attacker_hp;attacker_hp=next_attacker;expected_writes.append([1,attacker_hp])
                    overflow=heal-used
                    if owner and overflow>0:owner_hp=min(8001,owner_hp+overflow);expected_writes.append([0,owner_hp])
                    attacker_overlay=1;owner_overlay=int(bool(owner))
            expected=[1,calc,normalized,int(guard!=10),raw,after,16417,141,12,owner_hp,attacker_hp,owner_overlay,attacker_overlay]
        else:expected=[0,-1,-99,0,0,0,0,0,0,owner_hp,attacker_hp,0,0]
        assert actual==expected,(tag,values,actual,expected)
        assert writes==expected_writes,(tag,values,writes,expected_writes)
        counts[tag]+=1
    assert counts==[8640,15552],counts
    cases=STAGE/'cases.json';cases.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    inputs.update({str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__),skills,states,review,ROOT/'tools/review_weapon_event_order.py']})
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=dict(event=counts[0],legacyBloodGolemTail=counts[1]),
                routines={name:hashlib.sha256(body.encode()).hexdigest() for name,body in bodies.items()},inputs=inputs,
                hostStaticReview=dict(function='SUNITDMG_ExecuteEvents',bodySha256=hashlib.sha256(host.encode()).hexdigest(),orderedAnchors=anchors,executed=False),
                current=dict(skillId=76,stateId=55,event='domeleedamage',callback=4,sourceRadius=7,mana=5,duration='300+60*(rank-1)',
                             fullPercent='200+25*(rank-1)',playerOrHirelingPercent='fullPercent/4',resultFlags=16385,emittedResultFlags=16417,hitClass=141),
                artifacts={str(p.relative_to(ROOT)):sha(p) for p in [src,cases,STAGE/'compiler.log',STAGE/'sanitizer.log']},
                established=[
                    'Complete unchanged event4 and percentage bodies execute fractional physical values with source guards,statlist/creator resolution,negative-rank normalization,full versus quarter formula selection,explicit flags/hit class and event1-before-missile service order.',
                    'Complete hash-reviewed host dispatches attacker domeleedamage before defender damagedinmelee/Life Tap and before the physical life clip. Iron Maiden therefore consumes the pre-clip fractional component,not actual final life loss. The host itself is not executed by this probe. The callback is driven by the pinned domeleedamage event;it has no independent ranged test. Source formula and downstream events/mitigation are declared services,including a synthetic half-damage mutation control. Supplied source percentages retain the complete helper large-value early-division behavior.',
                    'Complete legacy enemy-BloodGolem tail is retained and executed:missing monster data,row,drain,evil alignment or live minion owner gates,20percent recovery,owner-first half share,pet/overflow caps and overlays match bounded independent expectations. This does not enable a new current native healing rule.'],
                remaining=[
                    'No Sanctuary C# change,learned casting,native event dispatch,save/client acceptance or class completion is claimed. Existing whole-HP after-hit Iron Maiden compatibility path still needs fractional ordered-event integration.',
                    'Formula evaluation,stat/unit/state lookup,ownership,downstream damage and HP writes are declared services. This is not the complete engine,formula VM or current3.3.93847 executable. Legacy enemy-BloodGolem healing needs current-build corroboration or an explicit unsupported adapter.',
                    'Event host filtering,actual native pre-clip physical ordering,melee/missile exceptions,caster removal,PvP/hireling behavior and complete current damage/HP/death/event chronology remain separate work.'])
    dest=ROOT/'tools/reference/iron-maiden-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Iron Maiden:8640 complete event contexts and15552 legacy Blood Golem-tail contexts passed ASan/UBSan;native integration pending.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
