#!/usr/bin/env python3
"""Compile unchanged pinned Holy Freeze callback gates/order and state removal with declared stubs."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
COMMIT='0c0bbd1e6bef9181a3da1c046523af622a925416'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
assert tree['sha']==COMMIT and not tree['truncated']
blobs={r['path']:r['sha'] for r in tree['tree']}
paths={k:STAGE/'d2moo-pinned-source'/v for k,v in {
    'pal':'source/D2Game/src/SKILLS/SkillPal.cpp',
    'damage':'source/D2Game/src/UNIT/SUnitDmg.cpp',
    'aura':'source/D2Game/src/SKILLS/Skills.cpp',
}.items()}
for path in paths.values():
    b=path.read_bytes();rel=path.relative_to(STAGE/'d2moo-pinned-source').as_posix()
    assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==blobs[rel]
def routine(key,name):
    s=paths[key].read_text();at=s.index('__fastcall '+name+'(');a=s.rfind('\n',0,at)+1;b=s.index('{',at)+1;depth=1
    while depth:
        depth+=(s[b]=='{')-(s[b]=='}');b+=1
    return s[a:b]
cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#define __fastcall
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int UNIT_MONSTER=1,STATE_SHATTER=2;
struct D2GameStrc{int nDifficulty=0;} game;
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,pSeed=0;bool dead=false;};
struct D2MonStatsTxt{int nColdEffect[3]={-50,-40,-25};} row;
struct D2StatListStrc{};
struct D2DamageStrc{int value=7;};
struct D2BasicAuraParamStrc{int nPassiveState=0,nAuraState=1,nSkillId=114,nSkillLevel=1,nDuration=51,nAuraStat[6]={},nAuraStatCalcValue[6]={};void* pStateRemoveCallback=nullptr;};
struct D2AuraCallbackStrc{void* pArgs=nullptr;D2UnitStrc* pOwner=nullptr;D2GameStrc* pGame=nullptr;};
bool hasRow=true,stayDeath=false;int applied=0,damage=0,events=0,state=-1,updates=0,order=0;
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return hasRow?&row:nullptr;}
int SUNIT_IsDead(D2UnitStrc* p){return p->dead;}
int STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc*,int){return stayDeath;}
void STATES_ToggleState(D2UnitStrc*,int id,int value){if(id==STATE_SHATTER)state=value;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){updates++;}
int SKILLS_AuraCallback_BasicAura(D2AuraCallbackStrc*,D2UnitStrc*){applied++;order=order*10+1;return 1;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc*){events++;order=order*10+2;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){damage++;order=order*10+3;}
uint32_t ITEMS_RollRandomNumber(int* p){order=order*10+4;return *p;}
'''
cpp+=routine('damage','SUNITDMG_GetColdEffect')+'\n'
cpp+=routine('pal','SKILLS_CurseStateCallback_HolyFreeze')+'\n'
cpp+=routine('pal','SKILLS_AuraCallback_HolyFreeze')+'\n'
cpp+=r'''int main(){printf("{\"callbackCases\":[");bool first=true;
for(int kind: {0,1})for(int present: {0,1})for(int cold: {-50,-25,0,25})for(int passive: {0,1})for(int hasDamage: {0,1})for(int seed: {0,19,20,99}){
 D2UnitStrc actor;actor.dwUnitType=kind;actor.pSeed=seed;hasRow=present;row.nColdEffect[0]=cold;
 D2DamageStrc packet;D2BasicAuraParamStrc args;args.nPassiveState=passive;args.pStateRemoveCallback=hasDamage?&packet:nullptr;
 D2AuraCallbackStrc cb{&args,&actor,&game};applied=damage=events=order=0;state=-1;
 int result=SKILLS_AuraCallback_HolyFreeze(&cb,&actor);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,present,cold,passive,hasDamage,seed,result,applied,damage,events,state,order);first=false;
}
printf("],\"removalCases\":[");first=true;
for(int dead: {0,1})for(int stays: {0,1}){D2UnitStrc actor;actor.dead=dead;stayDeath=stays;state=-1;updates=0;
 SKILLS_CurseStateCallback_HolyFreeze(&actor,1,nullptr);
 printf("%s[%d,%d,%d,%d]",first?"":",",dead,stays,state,updates);first=false;
}
printf("]}\n");}
'''
source=STAGE/'holy-freeze-source-probe.cpp';binary=STAGE/'holy-freeze-source-probe';source.write_text(cpp)
subprocess.run(['c++','-std=c++20','-O2',str(source),'-o',str(binary)],check=True)
result=json.loads(subprocess.check_output([str(binary)],text=True))
for kind,present,cold,passive,has_damage,seed,success,applied,damage,events,state,order in result['callbackCases']:
    eligible=kind!=1 or present and cold<0
    assert success==int(eligible)
    assert applied==int(eligible and passive) and damage==int(eligible and has_damage) and events==damage
    assert state==(int(seed<20) if eligible else -1)
    expected=int(('1' if passive else '')+('23' if has_damage else '')+'4') if eligible else 0
    assert order==expected
for dead,stays,state,updates in result['removalCases']:
    assert state==(0 if not dead else -1) and updates==int(not dead or not stays)
aura=paths['aura'].read_text();pal=paths['pal'].read_text()
assert 'nAuraFilter & 0x40000 && MONSTERS_IsPrimeEvil(pTarget)' in aura
assert 'if (nBonus < nColdEffect)' in pal and 'STAT_OTHER_ANIMRATE, nBonus' in pal
assert 'args1.pStateRemoveCallback = SKILLS_CurseStateCallback_HolyFreeze;' in pal
assert 'pStateRemoveCallback' not in routine('aura','sub_6FD10E50')
assert 'STATE_SHATTER' not in routine('aura','sub_6FD10E50')
out=dict(referenceVersion='3.3.93847',sourceCommit=COMMIT,gitBlobVerification=True,
    sourceHashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths.values()},
    callbackCases=len(result['callbackCases']),removalCases=result['removalCases'],
    verified=['Unchanged callback ColdEffect/nonmonster/missing-row gates, optional state/damage event order and exclusive20percent shatter threshold.','Unchanged special state removal retains shatter on dead actors and clears it on living actors; this callback is installed on the caster, not the hostile target.','Prime-Evil source filter and per-stat ColdEffect cap/other-animation assignment inspected, not compiled in this bounded probe.','SrvDo081 installs special removal on caster args1; hostile callback constructs fresh BasicAura args without it. Default sub_6FD10E50 removes the target slow state without clearing independent SHATTER. This routing is source-inspected, not a modern timing certificate.'],
    limits=['World, damage, BasicAura recipient application, state bitmaps, unit seed stream and animation updates are explicit stubs.','No source death/shatter-corpse consumer or modern lethal-pulse/death ordering is certified; native retained-loot unusable-corpse mapping is separately tested.','Modern source eligibility, mode composition, item/native interactions, proximity geometry and client acceptance remain open.'])
(ROOT/'tools/reference/holy-freeze-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n')
print(f"Holy Freeze source callback cases:{len(result['callbackCases'])}; state-removal cases:{len(result['removalCases'])}; passed.")
