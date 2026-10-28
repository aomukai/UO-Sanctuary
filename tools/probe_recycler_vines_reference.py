#!/usr/bin/env python3
"""Execute complete older vine corpse reservation and delayed resource callbacks.

Formula evaluation, lookups, allocation and collision are declared services.
The Solar current-table zero-formula result is retained as a conflict control.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight_path = ROOT / 'tools/reference/recycler-vines-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == preflight['legacyCommit']
    hashes, routines = {}, {}
    def extract(relative, name):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        body = function(raw.decode(), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        if name in preflight['completeRoutineHashes']:
            assert routines[name] == preflight['completeRoutineHashes'][name]
        return body + '\n'
    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#define __fastcall
constexpr int UNIT_MONSTER=1, STATE_CORPSE_NOSELECT=1, STAT_HITPOINTS=0, STAT_MANA=1;
struct D2GameStrc{} game;
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,life=0,mana=0,maxLife=100*256+211,maxMana=100*256+211;bool dead=false;}vine,owner,corpse,missile;
struct D2SkillsTxt{int wSrvMissileA=1,dwCalc[2]={1,0};}skill;
struct D2MissilesTxt{int dwParam[1]={45},wProgOverlay=1;}row;
struct Tables{int nMissilesTxtRecordCount=2,nOverlayTxtRecordCount=2;}tables;
Tables* sgptDataTables=&tables;
struct D2MissileStrc{int nMissile=0,nSkill=0,nSkillLevel=0;D2UnitStrc*pOwner=nullptr,*pOrigin=nullptr;}created;
bool skillRecord=true,missileRecord=true,hasOwner=true,hasTarget=true,reserved=false,allocation=true;
int refreshed=0,creates=0,messages=0,collision=0,overlays=0,rank=1,skillId=307,remaining=45;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return skillRecord?&skill:nullptr;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return missileRecord?&row:nullptr;}
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc*){return hasOwner?&owner:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&corpse:nullptr;}
bool STATES_CheckState(D2UnitStrc*p,int state){assert(p==&corpse&&state==STATE_CORPSE_NOSELECT);return reserved;}
void STATES_ToggleState(D2UnitStrc*p,int state,int on){assert(p==&corpse&&state==STATE_CORPSE_NOSELECT&&on==1);reserved=true;}
void UNITROOM_RefreshUnit(D2UnitStrc*p){assert(p==&corpse);refreshed++;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*p){creates++;created=*p;return allocation?&missile:nullptr;}
void sub_6FCC6300(D2UnitStrc*p,D2UnitStrc*q,int id,int level,int x,int y,int last){assert(p==&vine&&q==&corpse&&id==skillId&&level==1&&x==0&&y==0&&last==0);messages++;}
int MISSILE_GetSkill(D2UnitStrc*){return skillId;}
int MISSILE_GetLevel(D2UnitStrc*){return rank;}
int MISSILE_GetRemainingFrames(D2UnitStrc*){return remaining;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
bool SUNIT_IsDead(D2UnitStrc*p){return p->dead;}
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int){return stat==STAT_MANA?p->mana:p->life;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*p){return p->maxLife;}
int STATLIST_GetMaxManaFromUnit(D2UnitStrc*p){return p->maxMana;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int level){assert(id==skillId&&level==rank);return formula==1?3+level:0;}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int value,int){if(stat==STAT_MANA)p->mana=value;else p->life=value;}
void UNITS_SetOverlay(D2UnitStrc*p,int overlay,int zero){assert(p==&owner&&overlay==1&&zero==0);overlays++;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collision++;return 7;}
'''
    cpp += extract('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CalculatePercentage')
    cpp += extract('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvSt63_Corpse_VineCycler')
    for name in ['MISSMODE_SrvDo29_RecyclerDelay', 'MISSMODE_SrvDo33_VineRecyclerDelay']:
        cpp += extract('source/D2Game/src/MISSILES/MissMode.cpp', name)
    cpp += r'''
bool first=true;
void reset(){vine={};owner={};corpse={};missile={};skill={};row={};created={};skillRecord=missileRecord=hasOwner=hasTarget=allocation=true;reserved=false;refreshed=creates=messages=collision=overlays=0;}
void delay(int kind,int level,int frames,int amount,int ownerState,int invalid){
reset();skillId=kind?325:307;rank=level;remaining=frames;owner.life=owner.mana=amount;hasOwner=ownerState!=0;owner.dead=ownerState==2;
skill.dwCalc[0]=kind?0:1;skill.dwCalc[1]=kind?1:0;
missileRecord=invalid!=2;skillRecord=invalid!=3;
int result=kind?MISSMODE_SrvDo33_VineRecyclerDelay(&game,invalid==1?nullptr:&missile):MISSMODE_SrvDo29_RecyclerDelay(&game,invalid==1?nullptr:&missile);
printf("%s[1,[%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d]]",first?"":",",kind,level,frames,amount,ownerState,invalid,result,owner.life,owner.mana,collision,overlays);first=false;
}
int main(){printf("[");
for(int unit:{0,1,2})for(int record:{0,1})for(int miss:{-1,0,1,2})for(int own:{0,1})for(int target:{0,1,2})for(int allocate:{0,1}){
reset();skillId=307;rank=21;vine.dwUnitType=unit==2?0:1;skillRecord=record;skill.wSrvMissileA=miss;hasOwner=own;hasTarget=target!=0;reserved=target==2;allocation=allocate;
int result=SKILLS_SrvSt63_Corpse_VineCycler(&game,unit==0?nullptr:&vine,skillId,rank);
printf("%s[0,[%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",unit,record,miss,own,target,allocate,result,reserved,refreshed,creates,messages,created.nMissile,created.nSkill,created.nSkillLevel,created.pOwner==&owner&&created.pOrigin==&corpse);first=false;
}
for(int kind:{0,1})for(int level:{1,3,20,28,255})for(int frames:{44,45,46})for(int amount:{0,1,9*256+239,99*256+255,100*256+1,100*256+211,105*256+11})for(int own:{0,1,2})for(int invalid:{0,1,2,3})delay(kind,level,frames,amount,own,invalid);
for(int kind:{0,1})for(int level=1;level<=255;level++)delay(kind,level,45,9*256+239,1,0);
printf("]\n");}
'''
    generated = BASE / 'recycler-vines-source-probe.cpp'
    generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    sanitizer = BASE / 'recycler-vines-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    counts = [0, 0]
    for family, context, actual in cases:
        counts[family] += 1
        if family == 0:
            unit, record, missile_id, owner, target, allocation = context
            valid = unit == 1 and record == 1 and missile_id == 1 and owner == 1 and target == 1
            expected = [int(valid), int(target == 2 or valid), int(valid), int(valid), int(valid), 1 if valid else 0, 307 if valid else 0, 21 if valid else 0, int(valid)]
        else:
            kind, level, frames, amount, owner, invalid = context
            life = mana = amount
            called = invalid == 0
            changes = called and owner == 1 and frames == 45 and amount // 256 < 100
            if changes:
                # Independent bounded integer oracle; percentage source is also
                # compiled above. All contexts stay within its exact-small lane.
                value = min(100, amount // 256 + (0 if kind else 3 + level)) * 256
                if kind:
                    mana = value
                else:
                    life = value
            expected = [7 if called else 2, life, mana, int(called), int(changes)]
        assert actual == expected, (family, context, actual, expected)
    assert counts == [288, 3030], counts
    output = BASE / 'recycler-vines-source-probe.json'
    output.write_bytes(process.stdout)
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], nativeAccepted=False, handlerEnabled=False,
        currentExecutableParity=False, compiledRoutineHashes=routines, sourceHashes=hashes,
        preflightSha256=sha(preflight_path), contexts=len(cases), corpseReservationContexts=counts[0], delayedResourceContexts=counts[1],
        sanitizerClean=True, allRanks=list(range(1, 256)),
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in [Path(__file__).resolve(), generated, output, sanitizer]},
        declaredServices=['Current-table ln12 and blank-formula evaluation, not the full formula interpreter.', 'Owner/target/skill/row lookups, resource stat storage and overlay capture.', 'Missile allocation captures original-player ownership and corpse origin; allocation failure is independent.', 'Ordinary collision returns a distinct sentinel to verify callback delegation; factory, travel, queue and actual expiry are not executed.'],
        established=[
            'Complete start63 rejects missing/nonmonster caster, invalid row/missile, missing owner/target and reserved corpse. Success reserves/refreshes before creation and sends the skill packet even when allocation fails; origin is corpse and owner is the original player.',
            'Complete do29/do33 only attempt resource recovery at exactly45remaining frames with living owner, valid rows and whole-point resource below whole-point maximum. Other frames delegate ordinary collision without credit. Invalid callback arguments return2. Fractional resources can be truncated even by zero gain; full whole-point resources are left unchanged.',
            'Across all255ranks, Carrion current ln12 supplies4..258percent of maximum before clamping. Solar with current calc1 blank supplies zero to the unmodified older do33 and can discard the fractional current mana part. This is a reproduced source/table conflict, not an accepted Solar implementation.',
        ], remaining=[
            'AI111, corpse search/consumption/packet side effects, generic missile factory and per-frame remaining-count chronology, native actor and delayed save/reload remain separate. This probe does not establish wall-clock recovery delay or current underground exposure.',
            'Modern Solar formula slot and fractional-resource behavior are unresolved. Do not change the original source body to read calc2 in this proof or certify an inferred correction as current executable parity. Final effects/audio and icons remain separately inventoried.'],
    )
    path = ROOT / 'tools/reference/recycler-vines-source-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Recycler source: {len(cases)} contexts, four complete routines, ASan/UBSan clean; Solar conflict reproduced, no learned/native acceptance.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
