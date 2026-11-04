#!/usr/bin/env python3
"""Original copied-area router confirms Tail flags9 -> successful KB/GH flags13."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'dragon-tail-burst-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};bodies={}
    for rel,names in {'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD10140'],'source/D2Game/src/SKILLS/SkillAss.cpp':['sub_6FCF5DE0']}.items():
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p)
        for name in names:bodies[name]=function(raw.decode(),name)
    header=BASE/'d2moo-pinned-source/source/D2Game/include/UNIT/SUnitDmg.h';text=header.read_text()
    assert 'DAMAGERESULTFLAG_KNOCKBACK = 0x00000008'in text and 'DAMAGERESULTFLAG_GETHIT = 0x00000004'in text;inputs[str(header.relative_to(ROOT))]=sha(header)
    cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,DAMAGERESULTFLAG_SUCCESSFULHIT=1,STATE_UNINTERRUPTABLE=42;
struct D2GameStrc{};struct D2UnitStrc{int dwUnitType=UNIT_PLAYER;};
struct D2DamageStrc{uint16_t wResultFlags=0;int dwPhysDamage=0,dwFireDamage=0;};
struct D2AuraCallbackStrc{D2GameStrc*pGame;D2UnitStrc*pOwner;void*pArgs;};
int uninterruptible=0,defense=0,critical=0,events=0,commits=0,lastFlags=0,lastFire=0;
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){assert(false);return nullptr;}
int SUNITDMG_ApplyBlockOrDodge(D2GameStrc*,D2UnitStrc*a,D2UnitStrc*t,int avoid,int block){assert(a->dwUnitType==0&&t->dwUnitType==1&&avoid==1&&block==0);defense++;return 0;}
bool STATES_CheckState(D2UnitStrc*,int state){assert(state==STATE_UNINTERRUPTABLE);return uninterruptible;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc*a,D2UnitStrc*,D2DamageStrc*){assert(a->dwUnitType==0);critical++;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int context,D2DamageStrc*){assert(context==1);events++;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*d){commits++;lastFlags=d->wResultFlags;lastFire=d->dwFireDamage;d->dwFireDamage=-1;}
'''
    cpp+='\n'+bodies['sub_6FD10140']+'\n'+bodies['sub_6FCF5DE0']+r'''
int main(){D2GameStrc game;D2UnitStrc owner,target;target.dwUnitType=UNIT_MONSTER;bool comma=false;printf("[");
for(int flags:{0,9,13})for(int unt:{0,1})for(int fire:{0,1,256,12345}){
 uninterruptible=unt;defense=critical=events=commits=lastFlags=lastFire=0;D2DamageStrc packet;packet.wResultFlags=flags;packet.dwFireDamage=fire;D2AuraCallbackStrc callback{&game,&owner,&packet};
 sub_6FCF5DE0(&callback,&target);sub_6FCF5DE0(&callback,&target);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,unt,fire,defense,critical,events,commits,lastFlags,lastFire,packet.wResultFlags,packet.dwFireDamage);
}printf("]\n");}
'''
    cpp=cpp.replace('#include <cstring>','#include <cstring>\n#include <initializer_list>')
    p=STAGE/'source.cpp';p.write_text(cpp)
    with(STAGE/'compiler.log').open('w')as output:subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(STAGE/'probe')],stdout=output,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
    rows=json.loads(run.stdout);assert len(rows)==24
    for flags,unt,fire,defense,critical,events,commits,finalFlags,finalFire,originalFlags,originalFire in rows:
        assert (defense,critical,events,commits,finalFlags,finalFire,originalFlags,originalFire)==(2,2,2,2,flags|(0 if unt else 4),fire,flags,fire)if flags else (defense,critical,events,commits,finalFlags,finalFire,originalFlags,originalFire)==(0,0,0,0,0,0,flags,fire)
    cases=STAGE/'cases.json';cases.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,inputs=inputs,compiledUnchangedRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},
        cases=24,dispatchesPerCase=2,resultFlags=dict(success=1,getHit=4,knockback=8,do50=9,admittedNpc=13),
        artifacts={str(q.relative_to(ROOT)):sha(q)for q in (p,STAGE/'compiler.log',STAGE/'sanitizer.log',cases)},
        established=['Tail do50flags9 request knockback. Copied router adds GetHit4 for an interruptible successful NPC,retaining KB8; flags13 enter the existing source return controller even when fire is zero.','Complete callback clones each packet. Mutating the first delivery cannot alter the second recipient or original fire/flags. Zero result flags dispatch nothing.'],
        services=['NPC defense is supplied0 for context avoid1/block0; no player/PvP branch is claimed. Player-caster monster-critical call is a declared no-op,not a compiled monster-critical oracle.','Target state,context1events and damage sink are explicit boundaries. Native typed HP/return/motion use the existing ordered spell and knockback services and require separate actual-world tests.'])
    out=ROOT/'tools/reference/dragon-tail-burst-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Tail burst source evidence stale'
    else:out.write_text(encoded)
    print('Dragon Tail burst:24 complete copied-router controls/48dispatches pass ASan+UBSan; flags9/13 preserve knockback and per-recipient copies.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
