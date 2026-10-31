#!/usr/bin/env python3
"""Execute complete pinned missile getters/roll against current Skeletal Mage tables."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/mage-missiles-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={};bodies={}
    def source(path):
        raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
    common=source('source/D2Common/src/Units/Missile.cpp');skills=source('source/D2Common/src/D2Skills.cpp');mode=source('source/D2Game/src/MISSILES/MissMode.cpp')
    for t,n in [(skills,'SKILLS_CalculateDamageBonusByLevel'),(common,'MISSILE_GetMinElemDamage'),(common,'MISSILE_GetMaxElemDamage'),(common,'MISSILE_GetElementalLength'),(mode,'MISSMODE_RollDamageValue')]:bodies[n]=function(t,n)
    launch=function(source('source/D2Game/src/SKILLS/SkillMonst.cpp'),'SKILLS_SrvDo149_NecromageMissile')
    critical=function(source('source/D2Game/src/MONSTER/MonsterMode.cpp'),'D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70')
    assert 'nMissileId += pUnit->pMonsterData->nComponent[11];' in launch and 'SUNIT_GetTargetUnit(pGame, pUnit)' in launch
    assert 'pDamage->dwPoisDamage *= 2;' in critical and 'pDamage->dwColdDamage *= 2;' in critical
    path=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';rows=json.loads(path.read_text());array=[]
    for i in range(1,5):
        r=rows['necromage'+str(i)];assert r['*ID']==207+i
        array.append('{'+','.join([str(r.get('EMin',0)),str(r.get('EMax',0)),'{'+','.join(str(r.get('MinELev'+str(n),0)) for n in range(1,6))+'}','{'+','.join(str(r.get('MaxELev'+str(n),0)) for n in range(1,6))+'}',str(r.get('ELen',0)),'{'+','.join(str(r.get('ELevLen'+str(n),0)) for n in range(1,4))+'}',str(r.get('HitShift',0)),'-1'])+'}')
    cpp=r'''#include <cstdint>
#include <cstdio>
#include <cassert>
#define __fastcall
#define __stdcall
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int UNIT_MISSILE=3;
struct Data{int nLevel;};struct D2UnitStrc{int dwUnitType=3,dwClassId=0;Data* pMissileData=nullptr;int pSeed=0;int stats[3]={};};
struct D2MissilesTxt{int dwElemMin,dwElemMax,dwMinElemLev[5],dwMaxElemLev[5],dwElemLen,dwElemLevLen[3],nHitShift,dwElemDmgSymPerCalc;};
D2MissilesTxt rows[4]={'''+','.join(array)+r'''};
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int id){assert(id>=0&&id<4);return &rows[id];}
int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){assert(false);return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int id,int){return u->stats[id];}
int MONSTERUNIQUE_CalculatePercentage(int v,int n,int d){return int(int64_t(v)*n/d);}
int calls=0;int ITEMS_RollLimitedRandomNumber(int*,int bound){calls++;assert(bound>=0);return bound>0?bound-1:0;}
'''+ '\n'.join(bodies.values())+r'''
int main(){printf("[");bool first=true;
for(int element=0;element<4;element++)for(int level=1;level<=381;level++){
 D2UnitStrc missile;int minimum=MISSILE_GetMinElemDamage(&missile,nullptr,element,level),maximum=MISSILE_GetMaxElemDamage(&missile,nullptr,element,level),frames=MISSILE_GetElementalLength(0,&missile,element,level);
 missile.stats[0]=minimum;missile.stats[1]=maximum;calls=0;int rolled=MISSMODE_RollDamageValue(&missile,0,1,-1);
 printf("%s[%d,%d,%d,%d,%d,%d,%d]",first?"":",",element,level,minimum,maximum,frames,rolled,calls);first=false;
}
D2UnitStrc poison;poison.stats[0]=0;poison.stats[1]=1280;calls=0;assert(MISSMODE_RollDamageValue(&poison,0,1,-1)==0&&calls==0);printf("]\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True)
    (STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()[-3500:]
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3500:]
    cases=json.loads(run.stdout);assert len(cases)==1524;(STAGE/'cases.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    proof=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,functions={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
        launchFunctionSha256=hashlib.sha256(launch.encode()).hexdigest(),criticalFunctionSha256=hashlib.sha256(critical.encode()).hexdigest(),cases=1524,
        currentMissileTable=dict(path=str(path.relative_to(ROOT)),sha256=sha(path)),
        established=['Four component offsets select contiguous missile IDs208..211 from NecromageMissile149;target presence is required. Component selection/AI/animation clocks are separately reviewed adapters.',
            'Complete unchanged elemental getters,length getter,level-tier helper and impact roll execute for all four elements and derived levels1..381 using current tables. Poison absent HitShift means0;other elements use8.',
            'Physical min0 disables the poison row\'s unusual physical max1280 without a random draw. Positive elemental endpoints roll max-exclusive,including a draw service at equal endpoints.',
            'The complete intrinsic monster callback doubles elemental damage and poison rate,not duration. Its current necromage Crit5 is consumed through the shared critical predicate with a separately saved actor stream.'],
        limits=['The numerical functions execute with supplied table/stat/RNG services;the launch and critical bodies are hashed/static-reviewed,not complete live engine execution.',
            'Modern executable,source allocation/AI seed chronology,retired source lifetime and native geometry/room/candidate order remain unverified. Existing 1000ms mage attack cycle/half-cycle action timing is not source AI acceptance.',
            'Shared native incoming/death callbacks,initial poison-rate bridge/current applicability and PvP/global difficulty projections retain their earlier scope.'],
        artifacts={str(x.relative_to(ROOT)):sha(x) for x in [p,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']})
    output=ROOT/'tools/reference/mage-missile-source-evidence.json';text=json.dumps(proof,indent=2)+'\n'
    if check:assert output.read_text()==text
    else:output.write_text(text)
    print('Skeletal Mage:1524complete unchanged getter/length/impact-roll contexts;clean sanitizers;launch/critical source review retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
