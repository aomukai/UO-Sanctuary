#!/usr/bin/env python3
"""Execute the complete pinned Clay Golem incoming-slow callback with declared stat/state services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'clay-golem-slow-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);return p,raw.decode()
    p,raw=source('source/D2Game/src/SKILLS/SkillNec.cpp');body=function(raw,'D2GAME_EventFunc27_6FD0F7A0')
    q,helper=source('source/D2Game/src/SKILLS/Skills.cpp');gateway=function(helper,'sub_6FD10EC0')
    r,item=source('source/D2Game/src/SKILLS/SkillItem.cpp');itembody=function(item,'SKILLITEM_EventFunc19_Slow')
    d,damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');dispatch=function(damage,'SUNITDMG_ExecuteEvents')
    u,units=source('source/D2Common/src/Units/Units.cpp');rates=function(units,'UNITS_UpdateAnimRateAndVelocity')
    m,monster=source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    flag_bodies={n:function(monster,n) for n in ['MONSTERUNIQUE_CheckMonTypeFlag','sub_6FC6E940','D2GAME_SpawnMonster_6FC6F220','D2GAME_SpawnSuperUnique_6FC6F690']}
    assert 'nTypeFlag |= 4u' in flag_bodies['sub_6FC6E940'] and 'nTypeFlag |= 8u' in flag_bodies['D2GAME_SpawnMonster_6FC6F220']
    assert 'D2GAME_SpawnMonster_6FC6F220' in flag_bodies['D2GAME_SpawnSuperUnique_6FC6F690'] and 'nTypeFlag |= 2u' in flag_bodies['D2GAME_SpawnSuperUnique_6FC6F690']
    states=ROOT/'data/Sanctuary/Balance/class-states.json';slowed=next(x for x in json.loads(states.read_text()) if x['state']=='slowed');assert not slowed['curse'] and not slowed['curable']
    cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdio>
#include <cstdint>
#define __fastcall
constexpr int UNIT_MONSTER=1,STAT_ITEM_SLOW=150,STAT_VELOCITYPERCENT=67,STAT_ATTACKRATE=68,STAT_OTHER_ANIMRATE=69,STATE_SLOWED=1;
struct D2GameStrc{};
struct D2UnitStrc{int dwUnitType=1,slow=0,flags=0;};
struct D2DamageStrc{};struct D2StatListStrc{}list;
struct D2CurseStrc{D2UnitStrc*pUnit=nullptr,*pTarget=nullptr;int nSkill=0,nSkillLevel=0,nDuration=0,nStat=0,nStatValue=0,nState=0;}captured;
bool accepted=true;int attack=0,other=0,updates=0,calls=0;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int id,int layer){assert(id==STAT_ITEM_SLOW&&layer==0);return p->slow;}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*p,uint16_t mask){assert(mask==12);return (p->flags&mask)!=0;}
D2StatListStrc*sub_6FD10EC0(D2CurseStrc*p){calls++;captured=*p;return accepted?&list:nullptr;}
void STATLIST_SetStat(D2StatListStrc*,int id,int value,int layer){assert(layer==0);if(id==STAT_ATTACKRATE)attack=value;else{assert(id==STAT_OTHER_ANIMRATE);other=value;}}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){updates++;}
'''+body+r'''
int main(){bool first=true;printf("[");D2GameStrc game;D2UnitStrc owner,target;
for(int hasOwner:{0,1})for(int hasTarget:{0,1})for(int kind:{0,1,2,6})for(int amount:{0,1,11,50,51,75,90,127})for(int flags:{0,2,4,8,12})for(int gate:{0,1}){
owner.slow=amount;target.dwUnitType=kind;target.flags=flags;accepted=gate;captured={};attack=other=updates=calls=0;
int result=D2GAME_EventFunc27_6FD0F7A0(&game,1,hasOwner?&owner:nullptr,hasTarget?&target:nullptr,nullptr,75,1);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",hasOwner,hasTarget,kind,amount,flags,gate,result,captured.nStatValue,captured.nDuration,attack,other,updates,calls);first=false;
if(calls){assert(captured.pUnit==&owner&&captured.pTarget==&target&&captured.nSkill==0&&captured.nSkillLevel==1&&captured.nStat==STAT_VELOCITYPERCENT&&captured.nState==STATE_SLOWED);}
}printf("]\n");}
'''
    # The constants are declared stat services,not an ABI layout. Resolve their
    # identities from the pinned table before compiling the unchanged callback.
    ref=ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json';stats=json.loads(ref.read_text())
    manifest=ref.parent.parent/'sources.json';assert sha(ref)==json.loads(manifest.read_text())['files']['json/itemstatcost.json']['sha256']
    for symbol,name in [('STAT_ITEM_SLOW','item_slow'),('STAT_VELOCITYPERCENT','velocitypercent'),('STAT_ATTACKRATE','attackrate'),('STAT_OTHER_ANIMRATE','other_animrate')]:
        import re
        cpp=re.sub(r'\b'+symbol+r'=\d+',symbol+'='+str(stats[name]['*ID']),cpp)
    sourcepath=STAGE/'probe.cpp';sourcepath.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(sourcepath),'-o',str(STAGE/'probe')],capture_output=True)
    compiler=STAGE/'compiler.log';compiler.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()[-3000:]
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    rows=json.loads(run.stdout);assert len(rows)==1280;cases=STAGE/'cases.json';cases.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(rows),callbackSha256=hashlib.sha256(body.encode()).hexdigest(),stateGatewayReviewedOnlySha256=hashlib.sha256(gateway.encode()).hexdigest(),outgoingItemCallbackReviewedOnlySha256=hashlib.sha256(itembody.encode()).hexdigest(),dispatchReviewedOnlySha256=hashlib.sha256(dispatch.encode()).hexdigest(),animationDispatchReviewedOnlySha256=hashlib.sha256(rates.encode()).hexdigest(),monsterFlagReviewedOnlyHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in flag_bodies.items()},
        inputs={str(x.relative_to(ROOT)):sha(x) for x in [Path(__file__),p,q,r,d,u,m,ref,states,manifest]},artifacts={str(x.relative_to(ROOT)):sha(x) for x in [sourcepath,cases,compiler,san]},
        established=['Complete incoming callback27executes1280presence/type/slow/monster-flag/allocation contexts. Zero slow and nonplayer/nonmonster targets refuse;players and flag12monsters cap50,other monsters90. A successful750frame slowed state writes attackrate/other_animrate and updates animation/velocity.','The callback does not inspect damage magnitude. ExecuteEvents dispatches successful melee damage events before health/leech,including fully mitigated contacts;miss/block/ranged events are distinct.','Reviewed state gateway refreshes skill0/rank1same-state expiry without rewriting its primary velocity stat;callback27then rewrites attackrate/other_animrate. Slow state is noncurse;Cleansing/curse resistance do not apply.'],
        remaining=['State allocator,monster flags,stat graph and modern executable/native event/classification are declared services. Full gateway execution/current frame/room/death/event ordering still needs acceptance.','The old outgoing item19body has an inverted nonzero-value guard. Modern applicability is unresolved;do not silently replace the existing outgoing slow path with that apparent no-op.'])
    out=ROOT/'tools/reference/clay-golem-slow-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Clay incoming slow:1280complete callback27contexts;state reuse and outgoing item19are separately reviewed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
