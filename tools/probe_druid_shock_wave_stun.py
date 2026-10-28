#!/usr/bin/env python3
"""Execute complete missile health/mitigation/stun events with explicit stat services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    parent=ROOT/'tools/reference/bear-smite-engine-evidence.json';prior=json.loads(parent.read_text());path=BASE/'bear-smite-source-probe.cpp'
    assert sha(path)==prior['fixture']['generatedSourceSha256']
    cpp=path.read_text().split('\nbool first=true;\n',1)[0];hashes={};routines={}
    for relative,names in [('source/D2Game/src/UNIT/SUnitDmg.cpp',['SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage','SUNITDMG_ExecuteEvents']),('source/D2Game/src/MONSTER/MonsterUnique.cpp',['MONSTERUNIQUE_CalculatePercentage'])]:
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path)
        for name in names:
            body=function(raw.decode(),name);assert function(cpp,name)==body;routines[name]=hashlib.sha256(body.encode()).hexdigest()
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old;cpp=cpp.replace(old,new,1)
    replace('int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}',
        'bool stunHire=false,stunUnique=false,stunBoss=false,stunExists=false;int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p->dwUnitId==29&&stunHire;}')
    replace('int ITEMS_RollLimitedRandomNumber(', 'int stunDraw=0;int ITEMS_RollLimitedRandomNumber(')
    replace('return high?bound-1:0;}','return bound==100?stunDraw:high?bound-1:0;}')
    replace('struct D2StatListStrc{};', 'struct D2StatListStrc{int expires=1234,state=0;}stunList;int posts=0,toggles=0,refreshes=0,healthAtStun=-1;')
    replace('bool MONSTERS_IsBoss(D2MonStatsTxt*,D2UnitStrc*){return false;}','bool MONSTERS_IsBoss(D2MonStatsTxt*,D2UnitStrc*){return stunBoss;}')
    replace('bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,unsigned){return false;}','bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,unsigned flag){assert(flag==8);return stunUnique;}')
    replace('D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}',
        'D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int s){return s==STATE_STUNNED&&stunExists?&stunList:nullptr;}')
    replace('D2StatListStrc*STATLIST_AllocStatList(void*,unsigned,int,int,int){__builtin_trap();}',
        'D2StatListStrc*STATLIST_AllocStatList(void*,unsigned flags,int end,int type,int id){assert(flags==2&&type==UNIT_PLAYER&&id==17);stunList.expires=end;healthAtStun=defender.stats[STAT_HITPOINTS];return &stunList;}')
    replace('void STATLIST_SetState(D2StatListStrc*,int){__builtin_trap();}','void STATLIST_SetState(D2StatListStrc* p,int s){assert(s==STATE_STUNNED);p->state=s;}')
    replace('void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){__builtin_trap();}',
        'void D2COMMON_10475_PostStatToStatList(D2UnitStrc* p,D2StatListStrc*,int f){assert(p==&defender&&f==1);posts++;}')
    replace('void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){__builtin_trap();}',
        'void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}')
    replace('void STATLIST_SetExpireFrame(D2StatListStrc*,int){__builtin_trap();}',
        'void STATLIST_SetExpireFrame(D2StatListStrc* p,int end){p->expires=end;refreshes++;healthAtStun=defender.stats[STAT_HITPOINTS];}')
    replace('void STATES_ToggleState(D2UnitStrc*,int,int){__builtin_trap();}',
        'void STATES_ToggleState(D2UnitStrc* p,int state,int enabled){assert(p==&defender&&state==STATE_STUNNED&&enabled==1);toggles++;}')
    replace('void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){__builtin_trap();}',
        'void EVENT_SetEvent(D2GameStrc*,D2UnitStrc* p,int kind,int end,int,int){assert(p==&defender&&kind==EVENTTYPE_REMOVESTATE&&end==stunList.expires);}')
    cpp+=r'''
bool first=true;
void emit(int raw,int resist,int frames,int unique,int boss,int velocity,int hire,int roll,int existing,int gate){
game={};attacker={};defender={};mon={};attacker.dwUnitType=UNIT_PLAYER;defender.dwUnitType=UNIT_MONSTER;attacker.dwUnitId=17;defender.dwUnitId=29;attacker.pGame=defender.pGame=&game;
attacker.pInventory=defender.pInventory=nullptr;attacker.stats[STAT_HITPOINTS]=25600;defender.stats[STAT_HITPOINTS]=gate==5?0:25600;defender.stats[STAT_DAMAGERESIST]=resist;
interact=gate!=1;hasRoom=gate!=2;town=gate==3;mon.dwMonStatsFlags=gate==4?0:0xffffffff;mon.nVelocity=velocity;
stunHire=hire;stunUnique=unique;stunBoss=boss;stunDraw=roll;stunExists=existing;stunList={};posts=toggles=refreshes=draws=events=0;healthAtStun=-1;trace.clear();bounds.clear();
D2DamageStrc d;d.dwPhysDamage=raw;d.dwStunLen=frames;d.wResultFlags=5;d.dwHitClass=0x60;
SUNITDMG_ExecuteEvents(&game,&attacker,&defender,1,&d);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",raw,resist,frames,unique,boss,velocity,hire,roll,existing,gate,defender.stats[STAT_HITPOINTS],d.dwDmgTotal,stunList.expires,posts,toggles,refreshes,draws,healthAtStun);first=false;
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
for(int raw:{0,2560,16640,25601})for(int resist:{-100,0,50,100,150})for(int frames:{0,1,40,250,251,320,3850})for(int unique:{0,1})for(int boss:{0,1})for(int velocity:{0,5})for(int hire:{0,1})for(int roll:{0,89,90,99})for(int existing:{0,1})emit(raw,resist,frames,unique,boss,velocity,hire,roll,existing,0);
for(int gate=1;gate<=5;gate++)emit(2560,0,40,0,0,5,0,0,0,gate);printf("]\n");}
'''
    generated=BASE/'druid-shock-wave-stun-source-probe.cpp';generated.write_text(cpp)
    compiler=BASE/'druid-shock-wave-stun-compiler.log';sanitizer=BASE/'druid-shock-wave-stun-sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(generated.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(generated.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    cases=json.loads(run.stdout);assert len(cases)==17925
    for raw,resist,frames,unique,boss,velocity,hire,roll,existing,gate,hp,total,deadline,posts,toggles,refreshes,draws,health_at_stun in cases:
        damage=raw*(100-max(-100,min(50 if hire else 100,resist)))//100 if gate==0 else 0
        expected_hp=(0 if 25600-damage<256 else 25600-damage) if gate==0 else 0 if gate==5 else 25600
        eligible=gate==0 and frames>0 and (not unique or roll>=90) and not boss and velocity!=0
        duration=13 if hire and frames>=13 else min(frames,250)
        assert (hp,total)==(expected_hp,damage)
        assert (deadline,posts,toggles,refreshes,health_at_stun)==((1000+duration,int(not existing),int(not existing),int(existing),expected_hp) if eligible else (1234,0,0,0,-1))
        assert draws==int(gate==0 and frames>0 and unique)
    fixture=generated.with_suffix('.json');fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,
        sourceHashes=hashes,executedCompleteRoutineHashes=routines,contexts=len(cases),inheritedScaffold=dict(evidence=str(parent.relative_to(ROOT)),sha256=sha(parent)),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [generated,compiler,sanitizer,fixture]},
        established=[
            'Complete missile ExecuteEvents calls complete total-damage/resistance helpers and commits fractional HP before creating/refreshing stun. Physical immunity can remove all damage without suppressing eligible stun. The less-than-one-life lethal boundary is preserved; this probe does not replace death-mode handling.',
            'Raw stun caps at250frames; hirelings cap at13. Unique recipients roll once against100and require90or higher; bosses and stationary monsters refuse new stun. Unique RNG occurs before boss/velocity exclusion,as in the unchanged condition. Both new-state creation and unconditional replacement expiration are recorded after health commitment.',
            'Hostility,missing-room,town,unkillable and already-dead controls return before new health/stun delivery. Existing stun is not erased by failed eligibility. Surviving ordinary/immune controls and lethal HP-field controls retain explicit source order.',
        ],services=[
            'Actor stats/types,monster boss/unique/velocity/hireling metadata,selected RNG,empty event subscribers,stat-list allocation/post/callback/expiration scheduling and health storage are declared services. Complete production function bodies are unchanged and checked against the pinned Git tree.',
            'The matrix executes17925contexts under ASan/UBSan. Allocation success is supplied; state expiry delivery,death transitions,FHR/animation and graphical behavior are separate native/event concerns. Player victims,foreign equipment and nonzero leech/typed channels are outside this physical source-zero matrix.',
        ],remaining=[
            'Connect the same health-before-stun order in native Shock Wave,with existing mitigation,hit recovery and persisted stun. Keep lethal/reentrant chronology and unsupported recipients explicit instead of borrowing the existing War Cry stun-before-health order silently.',
            'Then verify actual travelling fan waves,LastCollide/pre-selection cast-local windows,bear SC/FCR,7mana,input/cancellation,gear/resistance/owner and real reloads. Final effects/audio and talent/hotkey icons remain separate placeholders.',
        ])
    destination=ROOT/'tools/reference/druid-shock-wave-stun-engine-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==encoded
    else:destination.write_text(encoded)
    print('Shock Wave event health/stun:17925complete source contexts; zero damage,eligibility,RNG,caps,refresh and HP-before-stun pass.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
