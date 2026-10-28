#!/usr/bin/env python3
"""Execute Hunger's complete older do122 composition with declared weapon services."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    pinpath = ROOT / 'tools/reference/druid-hunger-preflight.json'
    pin = json.loads(pinpath.read_text())
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit']
    hashes, routines, enums = {}, {}, {}

    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        return raw.decode()

    def routine(relative, name):
        body = function(source(relative), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def enum(relative, name):
        match = re.search(r'\benum\s+' + name + r'\s*\{.*?\};', source(relative), re.S)
        assert match
        body = match.group()
        enums[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    row = pin['currentRow']
    assert row['Param5'] == '-75' and row['SrcDam'] == '128' and not row.get('EType')
    cpp = '''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
'''
    cpp += enum('source/D2Common/include/D2Combat.h', 'D2C_ElemTypes')
    cpp += enum('source/D2Common/include/D2StatList.h', 'D2C_ItemStats')
    cpp += enum('source/D2Game/include/UNIT/SUnitDmg.h', 'D2DamageResultFlags')
    cpp += enum('source/D2Game/include/UNIT/SUnitDmg.h', 'D2DamageHitFlags')
    cpp += r'''
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_SKSRVDOFUNC=64;
struct Seed{};
struct D2GameStrc{void* pMemoryPool=nullptr;} game;
struct D2DamageStrc{uint32_t wResultFlags=0,dwHitFlags=0,dwHitClass=0;int dwEnDmgPct=0,dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwMagDamage=0,dwColdDamage=0,dwPoisDamage=0,dwLifeLeech=0,dwManaLeech=0,dwStamLeech=0,dwColdLen=0,dwPoisLen=0,dwStunLen=0,dwBurnLen=0,dwBurnDamage=0,dwFrzLen=0,dwDmgTotal=0;};
struct D2CombatStrc{D2GameStrc* pGame=nullptr;int dwAttackerType=0,dwAttackerId=0,dwDefenderType=0,dwDefenderId=0;D2DamageStrc tDamage;D2CombatStrc* pNext=nullptr;};
struct D2UnitStrc{int dwUnitType=0,dwUnitId=1,dwFlags=0,hp=20000000,physicalResist=0,fireResist=0;void* pMemoryPool=nullptr;D2CombatStrc* pCombat=nullptr;Seed pSeed;} owner,target;
struct D2SkillsTxt{uint32_t dwToHitCalc=uint32_t(-1);int dwToHit=50,dwLevToHit=10,wMana=3,wLevelMana=0,nManaShift=8,nEType=ELEMTYPE_NONE;uint32_t wResultFlags=0,dwHitFlags=0,dwHitClass=0,dwCalc[4]={1001,1002,1003,0};uint8_t nSrcDam=128;} row;
bool record=true,selected=true;int resultFlags=0,physical=0,weaponDrain=0,critical=0,fills=0,draws=0,calculations=0,allocations=0,drains=0,explicitAr=-1,rawPhysical=0,rawFire=0;
std::vector<int> trace;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==242);return record?&row:nullptr;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){return SKILLS_GetSkillsTxtRecord(id);}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,uint32_t formula,int id,int rank){assert(id==242);trace.push_back(int(formula-1000)+10);if(formula==1001)return -75;if(formula==1002||formula==1003)return 50+150*std::min(100,110*rank/(rank+6))/100;std::abort();}
int STATLIST_UnitGetStatValue(D2UnitStrc* unit,int stat,int){assert(stat==STAT_HITPOINTS);return unit->hp;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){explicitAr=ar;trace.push_back(1);return resultFlags;}
void SUNITDMG_FillDamageValues(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* damage,int,uint8_t src){assert(src==128);trace.push_back(2);fills++;damage->dwPhysDamage=physical*(critical?2:1);damage->dwFireDamage=11*256+13;damage->dwColdDamage=7*256+19;damage->dwLtngDamage=5*256+23;damage->dwMagDamage=3*256+29;damage->dwPoisDamage=2*256+31;damage->dwLifeLeech=weaponDrain;damage->dwManaLeech=weaponDrain+3;damage->dwHitFlags|=DAMAGEHITFLAG_32;if(critical)damage->wResultFlags|=DAMAGERESULTFLAG_CRITICALSTRIKE;}
int SKILLS_GetMinElemDamage(D2UnitStrc*,int id,int,int mode){assert(id==242&&mode==1);return 0;}
int SKILLS_GetMaxElemDamage(D2UnitStrc*,int id,int,int mode){assert(id==242&&mode==1);return 0;}
int SKILLS_GetElementalLength(D2UnitStrc*,int id,int,int mode){assert(id==242&&mode==1);return 0;}
int ITEMS_RollLimitedRandomNumber(Seed*,int bound){assert(bound==0);trace.push_back(3);draws++;return 0;}
int ITEMS_RollRandomNumber(Seed*){std::abort();} // ETypeNone never reaches random-element selection.
void SUNITDMG_CalculateTotalDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc* defender,D2DamageStrc* damage){trace.push_back(4);calculations++;rawPhysical=damage->dwPhysDamage;rawFire=damage->dwFireDamage;damage->dwPhysDamage=damage->dwPhysDamage*(100-defender->physicalResist)/100;damage->dwFireDamage=damage->dwFireDamage*(100-defender->fireResist)/100;damage->dwDmgTotal=damage->dwPhysDamage+damage->dwFireDamage+damage->dwColdDamage+damage->dwLtngDamage+damage->dwMagDamage+damage->dwPoisDamage;}
template<class T>T* allocate(void*){trace.push_back(5);allocations++;return new T;}
#define D2_ALLOC_STRC_POOL(pool,T) allocate<T>(pool)
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int value){assert(value==0);trace.push_back(6);drains++;}
'''
    common = 'source/D2Common/src/D2Skills.cpp'
    skills = 'source/D2Game/src/SKILLS/Skills.cpp'
    for name in ['SKILLS_GetToHitFactor', 'SKILLS_GetManaCosts']:
        cpp += routine(common, name)
    for name in ['sub_6FD155E0', 'sub_6FD11E40', 'D2GAME_RollElementalDamage_6FD14DD0']:
        cpp += routine(skills, name)
    cpp += routine('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CalculatePercentage')
    cpp += routine('source/D2Game/src/UNIT/SUnitDmg.cpp', 'SUNITDMG_AllocCombat')
    cpp += routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo122_Hunger')
    cpp += r'''
int main(){
 bool first=true;std::printf("[");
 for(int rank:{1,2,3,8,20,28,60,255})for(int input:{0,1,2,3,4,255,256,257,10003,524287,524288,524289,1048575,1048576,1048577,2097153})
 for(int crit:{0,1})for(int drain:{0,7,100})for(int profile=0;profile<5;profile++)for(int scene=0;scene<9;scene++){
 row={};owner={};target={};target.dwUnitType=UNIT_MONSTER;target.dwUnitId=2;
 target.physicalResist=profile==1?50:profile==2||profile==4?100:0;target.fireResist=profile==1?25:profile>=3?100:0;
 record=scene!=1;selected=scene!=2;resultFlags=scene==3?0:scene==4?DAMAGERESULTFLAG_DODGE:DAMAGERESULTFLAG_SUCCESSFULHIT;
 if(scene==5)resultFlags|=DAMAGERESULTFLAG_WEAPONBLOCK;if(scene==6)row.wResultFlags=DAMAGERESULTFLAG_SUCCESSFULHIT;
 if(scene==7)target.dwUnitType=2;if(scene==8){row.dwHitClass=0x70;row.dwHitFlags=DAMAGEHITFLAG_4096;target.hp=256;}
 physical=input;weaponDrain=drain;critical=crit;fills=draws=calculations=allocations=drains=0;explicitAr=-1;rawPhysical=rawFire=0;trace.clear();
 int mana=SKILLS_GetManaCosts(242,rank);int effect=SKILLS_SrvDo122_Hunger(&game,&owner,242,rank);
 D2DamageStrc saved=owner.pCombat?owner.pCombat->tDamage:D2DamageStrc{};
 std::printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%u,%d,[",first?"":",",rank,input,crit,drain,profile,scene,effect,fills,draws,calculations,allocations,drains,explicitAr,mana,rawPhysical,rawFire,saved.dwPhysDamage,saved.dwFireDamage,saved.dwColdDamage,saved.dwLtngDamage,saved.dwMagDamage,saved.dwPoisDamage,saved.dwLifeLeech,saved.dwManaLeech,saved.wResultFlags,saved.dwHitFlags,saved.dwHitClass,owner.dwFlags);first=false;
 for(size_t i=0;i<trace.size();i++)std::printf("%s%d",i?",":"",trace[i]);std::printf("]]");delete owner.pCombat;
 }std::printf("]\n");
}
'''
    generated = BASE / 'druid-hunger-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-hunger-compiler.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=log, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-hunger-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 34560
    for case in cases:
        rank, physical, critical, drain, profile, scene, effect, fills, draws, calc, alloc, drains, ar, mana, raw, rawfire, damage, fire, cold, lightning, magic, poison, life, mana_leech, flags, hitflags, hitclass, ownerflags, trace = case
        present = scene != 1
        selected = scene != 2
        active = present and selected
        success = active and scene not in [3, 4]
        calculated = success and scene != 5
        allocated = success and scene != 7
        assert (effect, fills, draws, calc, alloc, drains) == (int(active), int(success), int(success), int(calculated), int(allocated), int(success))
        assert mana == (768 if present else 0)
        assert ar == (50 + 10 * (rank - 1) if active and scene != 6 else -1)
        assert ownerflags == (64 if active else 0)
        original = physical * (2 if critical else 1)
        # Signed percentage is added back. For large values early division
        # occurs before the negative multiplication, preserving a remainder.
        reduced = original - (original * 75 // 100 if original <= 1048576 else original // 100 * 75)
        if calculated:
            assert (raw, rawfire) == (reduced, 11 * 256 + 13)
        else:
            assert (raw, rawfire) == (0, 0)
        if allocated:
            expected_physical = reduced * (100 - (50 if profile == 1 else 100 if profile in [2, 4] else 0)) // 100 if calculated else reduced
            expected_fire = (11 * 256 + 13) * (100 - (25 if profile == 1 else 100 if profile >= 3 else 0)) // 100 if calculated else 11 * 256 + 13
            assert (damage, fire, cold, lightning, magic, poison) == (expected_physical, expected_fire, 7 * 256 + 19, 5 * 256 + 23, 3 * 256 + 29, 2 * 256 + 31)
            extra = 50 + 150 * min(100, 110 * rank // (rank + 6)) // 100
            assert (life, mana_leech) == (drain + extra, drain + 3 + extra)
            expected_flags = 1 | (8192 if critical else 0) | (32768 if scene == 5 else 0) | (2 if calculated and scene == 8 else 0)
            assert (flags, hitflags, hitclass) == (expected_flags, 2 | 32 | (4096 if scene == 8 else 0), 0x70 if scene == 8 else 0)
        else:
            assert (damage, fire, cold, lightning, magic, poison, life, mana_leech, flags, hitflags, hitclass) == (0,) * 11
        expected_trace = ([1] if active and scene != 6 else []) + ([2, 3, 11, 12, 13] if success else []) + ([4] if calculated else []) + ([5] if allocated else []) + ([6] if success else [])
        assert trace == expected_trace, (case, expected_trace)
    fixture = BASE / 'druid-hunger-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        legacyCommit=tree['sha'], preflightSha256=sha(pinpath), contexts=len(cases), completeRoutineHashes=routines, pinnedEnumHashes=enums, sourceHashes=hashes,
        generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        columns=['rank', 'declaredPhysical256', 'declaredCritical', 'declaredGearDrain', 'mitigationProfile', 'scene', 'do', 'weaponFills', 'zeroElementRollCalls', 'calculations', 'allocations', 'drains', 'attackPercent', 'mana256', 'rawPhysical256', 'rawFire256', 'physical256', 'fire256', 'cold256', 'lightning256', 'magic256', 'poison256', 'lifeDrain', 'manaDrain', 'resultFlags', 'hitFlags', 'hitClass', 'ownerFlags', 'orderedServices'],
        established=[
            'Eight complete older routines compose34,560bounded contexts under ASan/UBSan, with four original header enums. Hunger do122rolls result at release, fills weapon/critical once, calls a zero-bound elemental roll for current ETypeNone, applies physical -75percent, adds both drain formulas, allocates/mitigates and drains combat/durability. Hitflag2prevents a second fill.',
            'Post-critical physical reduction preserves signed truncation and early-division boundaries, including original physical0..2,097,153and both sides of1,048,576after critical. Every declared nonphysical weapon component remains unchanged by Hunger. Existing captured drain is additive to each rank formula; ranks1..255cover diminishing life/mana percent inputs.',
            'Missing row/target,miss,dodge,force-hit and synthetic inconsistent weaponblock/object/explicit hitclass controls have explicit ordered outcomes. Synthetic successful weaponblock skips total calculation but still allocates; source object control calculates before rejecting allocation. These branches are controlled source inputs, not claims of reachable modern contacts.',
        ], declaredServices=[
            'Weapon fill/critical choice,typed mitigation,total calculation,HP/drain/durability,event delivery and memory allocation are declared services,not execution of complete combat/HP/leech. Packet field ordering and signed percentage execute unchanged around them. Current zero elemental endpoints/length and diminishing formula handles are explicit services; Fog is not executed.',
            'The zero-bound RNG call is counted but returns0without a real seed. Game seed advancement is unverified. Minimal structures and synthetic flags are not an ABI or current executable. No native learned Hunger handler, installation or final asset is claimed.',
        ], remaining=[
            'Connect release-time target/result/weapon capture without copying start58preparation. Reuse the verified S3scheduler, SourcePercentage.Calculate, physical-only critical, normal elemental/weapon/leech consumers and existing charge/form lifecycle. Test native gear,miss/block,immunities/drain/difficulty,action cancellation,right/quick and saved fractional resources.',
            'Exact modern event/seed/geometry/reentry/target-loss/low-mana and unsupported recipients remain explicit. Final bite/body/contact/leech/audio and separate talent/left/right/quick icons remain pending.',
        ])
    path = ROOT / 'tools/reference/druid-hunger-engine-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Hunger:34,560complete-source contexts/8routines; post-critical physical reduction and additive dual drain prepared, no native handler.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
