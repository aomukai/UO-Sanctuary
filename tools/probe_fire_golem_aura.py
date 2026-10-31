#!/usr/bin/env python3
"""Execute complete Do66 and its recipient callback with declared world services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'fire-golem-aura-source'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative='source/D2Game/src/SKILLS/SkillPal.cpp';path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    reference=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((reference/'sources.json').read_text())
    skills=reference/'json/skills.json';assert sha(skills)==manifest['files']['json/skills.json']['sha256']
    row=json.loads(skills.read_text())['102']
    assert [row[k] for k in ['srvdofunc','perdelay','HitShift','Param5','mana']]==[66,50,7,6,0]
    assert row['passivestat1']=='firemindam' and row['passivestat2']=='firemaxdam'
    assert row['passivecalc1']=='enms*par5/256' and row['passivecalc2']=='exms*par5/256'
    cpp=r'''#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,STAT_MANA=8,STATE_NOMANAREGEN=1;
struct D2GameStrc{int dwGameFrame=100;}game;
struct D2UnitStrc{int dwUnitType=1;}actor,targets[2];
struct D2SkillsTxt{int nAuraState=1,nPassiveStat[5]={48,49,-1,-1,-1},dwPassiveCalc[5]={10,11,-1,-1,-1},wAuraTargetState=-1,wAuraStat[6]={-1,-1,-1,-1,-1,-1},dwAuraStatCalc[6]={},dwHitClass=0,wResultFlags=0,dwHitFlags=0,dwAuraRangeCalc=20,dwAuraFilter=0;}skill;
struct Tables{int nStatesTxtRecordCount=600;}tables;auto*sgptDataTables=&tables;
struct D2DamageStrc{int dwFireDamage=0,dwHitClass=0,wResultFlags=0,dwHitFlags=0;};
struct D2BasicAuraParamStrc{int nSkillId=0,nSkillLevel=0,nDuration=0,nAuraState=0,nAuraStat[6]={},nAuraStatCalcValue[6]={};};
struct D2DamageAuraParamStrc{int nSkillId=0,nSkillLevel=0,nDuration=0,nAuraState=0,nAuraStat[6]={},nAuraStatCalcValue[6]={},nPassiveState=0;D2DamageStrc*pDamage=nullptr;int field_40=0;};
struct D2AuraCallbackStrc{D2GameStrc*pGame;D2UnitStrc*pOwner;void*pArgs;};
int guard=0,mana=0,cost=0,town=0,hasDamage=0,order=0,selfCalls=0,selfMin=0,selfMax=0,duration=0,rolls=0,finds=0,eventCalls=0,damageCalls=0,firstDamage=0,secondDamage=0,flags=0;
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){assert(id==102);return guard==1?nullptr:&skill;}
int SKILLS_GetManaCosts(int,int){return cost;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){assert(stat==STAT_MANA);return mana;}
int SKILL_ComputePeriodicRate(D2GameStrc*g,D2UnitStrc*,D2SkillsTxt*,int,int){return g->dwGameFrame+50;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int,int){return f==10?123:f==11?456:f==20?7:0;}
int SKILLS_AuraCallback_BasicAura(D2AuraCallbackStrc*cb,D2UnitStrc*){auto*a=(D2BasicAuraParamStrc*)cb->pArgs;selfCalls++;selfMin=a->nAuraStatCalcValue[0];selfMax=a->nAuraStatCalcValue[1];duration=a->nDuration;order=order*10+1;return 1;}
void sub_6FD0FE50(D2GameStrc*g,D2UnitStrc*u,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),D2BasicAuraParamStrc*a){D2AuraCallbackStrc cb{g,u,a};callback(&cb,u);}
void*UNITS_GetRoom(D2UnitStrc*){return &game;}
int DUNGEON_IsRoomInTown(void*){return town;}
int D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*d,int,int){rolls++;order=order*10+2;d->dwFireDamage=257;return hasDamage;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int event,D2DamageStrc*d){assert(event==1);eventCalls++;order=order*10+4;d->dwFireDamage++;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*d){damageCalls++;order=order*10+5;(damageCalls==1?firstDamage:secondDamage)=d->dwFireDamage;flags=d->wResultFlags;}
void sub_6FD0FE80(D2GameStrc*g,D2UnitStrc*u,int,int,int radius,int,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),D2DamageAuraParamStrc*a,int,const char*,int){assert(radius==7);finds++;order=order*10+3;D2AuraCallbackStrc cb{g,u,a};for(auto&t:targets)callback(&cb,&t);}
void STATES_ToggleState(D2UnitStrc*,int,int){}
void D2GAME_SKILLMANA_AuraConsume_6FD10C90(D2UnitStrc*,int){assert(false);}
'''
    names=['SKILLS_AuraCallback_HolyFire_HolyShock_Sanctuary_Conviction','SKILLS_SrvDo066_HolyFire_HolyShock_Sanctuary_Conviction']
    bodies={name:function(raw.decode(),name) for name in names}
    for body in bodies.values():cpp+=body+'\n'
    cpp+=r'''int main(){printf("[");bool first=true;
for(int kind:{0,1})for(int t:{0,1})for(int m:{0,10})for(int c:{0,5})for(int h:{0,1})for(int r:{1,10,30})for(int g:{0,1,2,3}){
actor.dwUnitType=kind;town=t;mana=m;cost=c;hasDamage=h;guard=g;skill.nAuraState=g==2?-1:g==3?600:1;
order=selfCalls=selfMin=selfMax=duration=rolls=finds=eventCalls=damageCalls=firstDamage=secondDamage=flags=0;
int result=SKILLS_SrvDo066_HolyFire_HolyShock_Sanctuary_Conviction(&game,&actor,102,r);
printf("%s[[%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",kind,t,m,c,h,r,g,result,selfCalls,selfMin,selfMax,duration,rolls,finds,eventCalls,damageCalls,firstDamage,secondDamage,flags,order);first=false;
}printf("]\n");}
'''
    source=STAGE/'probe.cpp';source.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],capture_output=True)
    (STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    cases=json.loads(run.stdout);assert len(cases)==384
    for (kind,town,mana,cost,hit,rank,guard),result in cases:
        if guard:expected=[0]*13
        else:
            allowed=kind!=0 or mana>=cost;damages=0 if town else 2*hit
            order=1 if town else 1234545 if hit else 123
            expected=[1,1,123 if allowed else 0,456 if allowed else 0,51,0 if town else 1,0 if town else 1,damages,damages,258 if damages else 0,258 if damages else 0,32 if damages else 0,order]
        assert result==expected,(kind,town,mana,cost,hit,rank,guard,result,expected)
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),ordinaryCases=96,guardCases=288,
        probeSha256=sha(Path(__file__)),sourcePath=relative,sourceSha256=sha(path),skillTableSha256=sha(skills),
        unchangedRoutineHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in bodies.items()},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [source,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Complete unchanged Do66 and recipient callback execute384contexts with declared skill/formula/periodic-rate/world/stat/event services. Nonplayer self weapon values bypass the player mana gate and are applied before town return;50frame periodic service yields51frame self lifetime.',
            'Outside town one elemental packet is rolled before recipient enumeration. Each recipient receives a separate copy and event1then missile damage;controlled event mutation does not leak to the next recipient. Town performs self application without rolling or enumerating damage.'],
        remaining=['Numeric elemental/weapon formulas,mastery and actual state-list replacement/expiry are declared services here,not executed source bodies. Verify their native composition separately. No current executable parity claim.',
            'Modern proximity scaling,filter/room/geometry/order,initial aura scheduling,AI/global frame/seed and notifications/client remain separate. These old bodies do not implement the later2.4proximity rule.'])
    destination=ROOT/'tools/reference/fire-golem-aura-source-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==rendered
    else:destination.write_text(rendered)
    print('Fire Holy Fire source:384complete Do66/callback contexts pass ASan+UBSan;state/world services remain explicit.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
