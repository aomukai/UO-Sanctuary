#!/usr/bin/env python3
"""Compose shared missile flags, full damage/stun events and mode dispatch.

The modern stun duration is an explicit packet input. This does not pretend
that the older damage9 fallback evaluates the modern skill formula.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_molten_boulder_impact_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/molten-boulder-impact-source-evidence.json').read_text())
    base = (STAGE / 'molten-boulder-impact-source-probe.cpp').read_text().split('int main()', 1)[0]

    def replace(before, after):
        nonlocal base
        assert base.count(before) == 1, before
        base = base.replace(before, after, 1)

    replace('struct D2StatListStrc{};', 'struct D2StatListStrc{}stunState;bool hasStun=false,unique=false,boss=false,hire=false;int stunUntil=0,allocations=0,stateEvents=0;')
    replace('int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}bool MONSTERS_IsBoss(void*,D2UnitStrc*){return false;}',
            'int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p==&target&&hire;}bool MONSTERS_IsBoss(void*,D2UnitStrc* p){return p==&target&&boss;}')
    replace('D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}',
            'D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int state){return p==&target&&state==STATE_STUNNED&&hasStun?&stunState:nullptr;}')
    replace('bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,unsigned){assert(false);return false;}',
            'bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* p,unsigned flag){assert(p==&target&&flag==8);return unique;}')
    replace('D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){assert(false);return nullptr;}',
            'D2StatListStrc* STATLIST_AllocStatList(void*,unsigned flags,int until,int type,int id){assert(flags==2&&type==UNIT_PLAYER&&id==77);allocations++;stunUntil=until;return &stunState;}')
    for name in ['STATES_ToggleState', 'EVENT_SetEvent', 'STATLIST_SetExpireFrame', 'STATLIST_SetState', 'STATLIST_SetStatRemoveCallback', 'D2COMMON_10475_PostStatToStatList']:
        replace('UNUSED_VOID(' + name + ')', '')
    # These are observable state services; the complete event body owns the
    # decision, duration/cap, refresh/allocation order and application guards.
    base += r'''
void STATES_ToggleState(D2UnitStrc* p,int state,int on){assert(p==&target&&state==STATE_STUNNED&&on==1);hasStun=true;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc* p,int event,int until,int,int){assert(p==&target&&event==EVENTTYPE_REMOVESTATE);stateEvents++;stunUntil=until;}
void STATLIST_SetExpireFrame(D2StatListStrc* p,int until){assert(p==&stunState);stunUntil=until;}
void STATLIST_SetState(D2StatListStrc* p,int state){assert(p==&stunState&&state==STATE_STUNNED);}
void STATLIST_SetStatRemoveCallback(D2StatListStrc* p,void(*)(D2UnitStrc*,int,D2StatListStrc*)){assert(p==&stunState);}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* p,D2StatListStrc* state,int one){assert(p==&target&&state==&stunState&&one==1);hasStun=true;}
'''
    # Declarations must precede the inherited full callers, without replacing
    # any source body. Signatures match the explicit recording services above.
    declarations = r'''
void STATES_ToggleState(D2UnitStrc*,int,int);void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int);
void STATLIST_SetExpireFrame(D2StatListStrc*,int);void STATLIST_SetState(D2StatListStrc*,int);
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*));
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int);
'''
    replace('int32_t __fastcall SUNIT_IsDead(', declarations + '\nint32_t __fastcall SUNIT_IsDead(')
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int duration:{0,10,50,300})for(int grade:{0,1,2,3,4})for(bool existing:{false,true})for(bool guard:{false,true})
for(int defense:{0,1,2,8,16})for(int resistance:{0,100})for(bool kb:{false,true})for(uint32_t low:{0u,1u,19u,89u,123u,410u,0xFFFFFFFFu}){
 game={};actor={};target={};missile={};row={};mon2={};missileRow={};actor.dwUnitType=UNIT_PLAYER;actor.dwUnitId=77;actor.group=1;target.group=2;target.dwUnitId=99;actor.pGame=target.pGame=missile.pGame=&game;
 game.pUnitList[0][77]=&actor;missile.dwUnitType=UNIT_MISSILE;missile.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;missile.dwOwnerType=0;missile.dwOwnerGUID=77;
 actor.pSeed.nLowSeed=missile.pSeed.nLowSeed=low;actor.pSeed.nHighSeed=missile.pSeed.nHighSeed=666;
 row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];row.nVelocity=grade==3?0:6;
 target.stats[STAT_HITPOINTS]=25600;target.stats[STAT_DAMAGERESIST]=resistance;
 mon2.dwModeFlags=gdwBitMasks[MONMODE_BLOCK]|(kb?gdwBitMasks[MONMODE_KNOCKBACK]:0);
 missileRow.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_GETHIT];missileRow.nKnockBack=1;
 unique=grade==1;boss=grade==2;hire=grade==4;hasStun=existing;stunUntil=existing?1999:0;allocations=stateEvents=callbackCount=kills=modes=0;requested=-1;blocked=defense;uninterruptible=guard;
 D2DamageStrc damage{};damage.dwPhysDamage=1536;damage.dwStunLen=duration;damage.dwHitClass=0x60;
 MISSMODE_SetDamageFlags(&game,&missile,&target,&damage);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%llu,%llu,%d,%d,%d,%d,%d]",first?"":",",duration,grade,existing,guard,defense,resistance,kb,low,target.stats[STAT_HITPOINTS],requested,(unsigned long long)missile.pSeed.lSeed,(unsigned long long)actor.pSeed.lSeed,damage.wResultFlags,hasStun,stunUntil,allocations,stateEvents);first=false;
}printf("]\n");}
'''
    path = STAGE / 'twister-impact-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'twister-impact-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    rows = json.loads(run.stdout)
    assert len(rows) == 11200, len(rows)
    fixture = STAGE / 'twister-impact-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
                    legacyCommit=parent['legacyCommit'], reusedComposition='tools/reference/molten-boulder-impact-source-evidence.json',
                    sourceHashes=parent['sourceHashes'], routineHashes=parent['routineHashes'],
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(rows)),
                    sanitizers='ASan/UBSan exit0 with empty runtime stderr',
                    established=['Complete shared missile flags, typed resistance/health event body, stun allocation/refresh/cap and final mode dispatcher compose with player ownership.',
                                 'Twister GetHit1/KnockBack1 consumes its independent missile RNG draw unless uninterruptible. Defense suppresses hit events and stun; an existing knockback flag can still request movement on a blocked hit.',
                                 'Normal, unique10percent, boss, stationary and hireling13-frame cases record separate attacker/missile RNG, new versus existing stun state, physical immunity, uninterruptible health/event versus mode behavior.'],
                    limits=['The modern stun length is an explicit input packet. The earlier raw-Param2 callback is not passed off as current formula execution; modern duration formula has separate current-data/actual-content evidence.',
                            'State allocation/storage/events, declared monster flags/modes, empty event subscribers, damage stats and mode/kill calls are recording services; no complete modern executable, native skill or ordinary-client certificate follows.'])
    output = ROOT / 'tools/reference/twister-impact-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == text
    else:
        output.write_text(text)
    print('Twister:11200 complete shared flag/health/stun/mode cases; modern duration supplied explicitly; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
