#!/usr/bin/env python3
"""Execute complete Fire Claws/element/allocator routines with declared combat services."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def build(check=False):
    pinpath=ROOT/'tools/reference/druid-fire-claws-preflight.json';pin=json.loads(pinpath.read_text())
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==pin['legacyCommit']
    hashes,bodies,routines,enums={},{},{},{}
    def source(relative):
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path);return raw.decode()
    def routine(relative,name):
        body=function(source(relative),name);bodies[name]=body;routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    def enum(relative,name):
        text=source(relative);match=re.search(r'\benum\s+'+name+r'\s*\{.*?\};',text,re.S);assert match,name
        body=match.group(0);enums[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    row=pin['currentRows']['skills']['Fire Claws'];assert row['SrcDam']=='128' and row['EType']=='fire' and row['EDmgSymPerCalc']=="(skill('Firestorm'.blvl)+skill('Molten Boulder'.blvl))*par8"
    cpp='''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_MAYBE_UNUSED(x) (void)(x)
using BOOL=int;
'''
    cpp+=enum('source/D2Common/include/D2Combat.h','D2C_ElemTypes')
    cpp+=enum('source/D2Common/include/D2StatList.h','D2C_ItemStats')
    cpp+=enum('source/D2Game/include/UNIT/SUnitDmg.h','D2DamageResultFlags')
    cpp+=enum('source/D2Game/include/UNIT/SUnitDmg.h','D2DamageHitFlags')
    cpp+='''constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_SKSRVDOFUNC=64,EVENTTYPE_REMOVESTATE=10;
struct Seed{};
struct D2GameStrc{int dwGameFrame=100;void* pMemoryPool=nullptr;} game;
struct D2DamageStrc{uint32_t wResultFlags=0,dwHitFlags=0,dwHitClass=0;int dwEnDmgPct=0,dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwMagDamage=0,dwColdDamage=0,dwPoisDamage=0,dwLifeLeech=0,dwManaLeech=0,dwStamLeech=0,dwColdLen=0,dwPoisLen=0,dwStunLen=0,dwBurnLen=0,dwBurnDamage=0,dwFrzLen=0,dwDmgTotal=0;};
struct D2CombatStrc{D2GameStrc* pGame=nullptr;int dwAttackerType=0,dwAttackerId=0,dwDefenderType=0,dwDefenderId=0;D2DamageStrc tDamage;D2CombatStrc* pNext=nullptr;};
struct D2UnitStrc{int dwUnitType=0,dwUnitId=1,dwFlags=0,hp=20000000,physicalResist=0,fireResist=0;void* pMemoryPool=nullptr;D2CombatStrc* pCombat=nullptr;Seed pSeed;} owner,target;
struct D2SkillsTxt{int dwEMin=0,dwEMax=0,dwEMinLev[5]={},dwEMaxLev[5]={},nToHitShift=8,dwEDmgSymPerCalc=1002,nEType=ELEMTYPE_FIRE,dwELen=0,dwELevLen[3]={},dwELenSymPerCalc=-1;uint32_t dwToHitCalc=uint32_t(-1);int dwToHit=50,dwLevToHit=15,wMana=4,wLevelMana=0,nManaShift=8;uint32_t wResultFlags=0,dwHitFlags=0,dwHitClass=0,dwCalc[4]={1001,0,0,0};uint8_t nSrcDam=128;int wSrvOverlay=37,wAuraTargetState=-1,nAuraState=0,dwAuraLenCalc=1003;} row,prototype;
struct D2StatListStrc{};
struct Tables{int nOverlayTxtRecordCount=100,nStatesTxtRecordCount=232;} tables;auto* sgptDataTables=&tables;
bool record=true,selected=true,exposeCombat=true,critical=false;int hardA=0,hardB=0,mastery=0,rollChoice=0,resultFlags=1,fills=0,draws=0,calculations=0,allocations=0,drains=0,overlays=0,explicitAr=-1,rngBound=0,rngValue=0,rawFire=0,rawPhysical=0;std::vector<int> trace;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==239);return record?&row:nullptr;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){return SKILLS_GetSkillsTxtRecord(id);}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int){assert(id==239);if(formula==1001)return 0;if(formula==1002)return (hardA+hardB)*22;std::abort();}
int STATLIST_UnitGetStatValue(D2UnitStrc* unit,int stat,int){if(unit==&target&&stat==STAT_HITPOINTS)return target.hp;assert(unit==&owner&&stat==STAT_PASSIVE_FIRE_MASTERY);return mastery;}
int DATATBLS_ApplyRatio(int amount,int percentage,int divisor){return int(int64_t(amount)*percentage/divisor);}
int ITEMS_RollLimitedRandomNumber(Seed*,int bound){assert(bound>0);trace.push_back(4);draws++;rngBound=bound;rngValue=rollChoice?bound-1:0;return rngValue;}
int ITEMS_RollRandomNumber(Seed*){std::abort();}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){explicitAr=ar;trace.push_back(1);return resultFlags;}
void SUNITDMG_FillDamageValues(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* damage,int,uint8_t src){assert(src==128);trace.push_back(2);fills++;trace.push_back(3);damage->dwPhysDamage=(40*256+37)*(critical?2:1);damage->dwFireDamage=11*256+13;damage->dwHitFlags|=DAMAGEHITFLAG_32;if(critical)damage->wResultFlags|=DAMAGERESULTFLAG_CRITICALSTRIKE;}
void SUNITDMG_CalculateTotalDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc* defender,D2DamageStrc* damage){calculations++;trace.push_back(5);rawFire=damage->dwFireDamage;rawPhysical=damage->dwPhysDamage;damage->dwPhysDamage=damage->dwPhysDamage*(100-defender->physicalResist)/100;damage->dwFireDamage=damage->dwFireDamage*(100-defender->fireResist)/100;damage->dwDmgTotal=damage->dwPhysDamage+damage->dwFireDamage;}
template<class T>T* allocate(void*){allocations++;trace.push_back(6);return new T;}
#define D2_ALLOC_STRC_POOL(pool,T) allocate<T>(pool)
D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc* attacker,D2UnitStrc*){return exposeCombat&&attacker->pCombat?&attacker->pCombat->tDamage:nullptr;}
void UNITS_SetOverlay(D2UnitStrc*,int overlay,int){assert(overlay==37);overlays++;trace.push_back(7);}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int value){assert(value==0);drains++;trace.push_back(8);}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){std::abort();}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){std::abort();}
void STATLIST_SetState(D2StatListStrc*,int){std::abort();}void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){std::abort();}void sub_6FD10E50(){}void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){std::abort();}void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){std::abort();}void STATES_ToggleState(D2UnitStrc*,int,int){std::abort();}void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){std::abort();}
'''
    common='source/D2Common/src/D2Skills.cpp';skills='source/D2Game/src/SKILLS/Skills.cpp';druid='source/D2Game/src/SKILLS/SkillDruid.cpp';damage='source/D2Game/src/UNIT/SUnitDmg.cpp'
    for name in ['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength','SKILLS_GetToHitFactor','SKILLS_GetManaCosts']:cpp+=routine(common,name)
    for name in ['sub_6FD155E0','sub_6FD11E40','D2GAME_RollElementalDamage_6FD14DD0']:cpp+=routine(skills,name)
    cpp+=routine(damage,'SUNITDMG_AllocCombat')
    cpp+=routine(druid,'SKILLS_SrvSt58_FireClaws')
    cpp+=routine(skills,'SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws')
    cpp+='int main(){\n'
    for field,source_key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:cpp+=f'prototype.{field}={int(row[source_key])};\n'
    for field,key in [('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:
        for i in range(5):cpp+=f'prototype.{field}[{i}]={int(row[key+str(i+1)])};\n'
    cpp+='''bool first=true;std::printf("[");for(int rank:{1,2,8,9,20,28,60})for(int a:{0,1,20})for(int b:{0,1,20})for(int master:{0,25,100})for(int crit:{0,1})for(int roll:{0,1})for(int profile=0;profile<5;profile++)for(int scene=0;scene<12;scene++){
row=prototype;owner={};target={};target.dwUnitType=UNIT_MONSTER;target.dwUnitId=2;target.physicalResist=profile==1?50:profile==2||profile==4?100:0;target.fireResist=profile==1?25:profile>=3?100:0;
record=scene!=2;selected=scene!=3;exposeCombat=true;critical=crit;hardA=a;hardB=b;mastery=master;rollChoice=roll;resultFlags=scene==1||scene==7||scene==11?0:scene==5?DAMAGERESULTFLAG_DODGE:DAMAGERESULTFLAG_SUCCESSFULHIT;
if(scene==6)resultFlags|=DAMAGERESULTFLAG_WEAPONBLOCK;if(scene==7)row.wResultFlags=DAMAGERESULTFLAG_SUCCESSFULHIT;if(scene==9)target.dwUnitType=2;if(scene==10){row.dwHitClass=0x70;row.dwHitFlags=DAMAGEHITFLAG_4096;target.hp=256;}if(scene==11)row.wSrvOverlay=0;
fills=draws=calculations=allocations=drains=overlays=0;explicitAr=-1;rngBound=rngValue=rawFire=rawPhysical=0;trace.clear();
int lo=SKILLS_GetMinElemDamage(&owner,239,rank,1),hi=SKILLS_GetMaxElemDamage(&owner,239,rank,1),mana=SKILLS_GetManaCosts(239,rank);
int start=SKILLS_SrvSt58_FireClaws(&game,&owner,239,rank);if(scene==4)selected=false;if(scene==8)exposeCombat=false;
int effect=SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws(&game,&owner,239,rank);
D2DamageStrc saved=owner.pCombat?owner.pCombat->tDamage:D2DamageStrc{};
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%u,%d,[",rank,a,b,master,crit,roll,profile,scene,start,effect,fills,draws,calculations,allocations,drains,overlays,explicitAr,mana,lo,hi,rngBound,rngValue,rawPhysical,rawFire,saved.dwPhysDamage,saved.dwFireDamage,saved.wResultFlags,saved.dwHitFlags,saved.dwHitClass,target.hp);
for(size_t i=0;i<trace.size();i++){if(i)std::printf(",");std::printf("%d",trace[i]);}std::printf("]]");delete owner.pCombat;
}std::printf("]\\n");}
'''
    generated=BASE/'druid-fire-claws-source-probe.cpp';generated.write_text(cpp);binary=BASE/'druid-fire-claws-source-probe'
    compiler=BASE/'druid-fire-claws-compiler.log';sanitizer=BASE/'druid-fire-claws-sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
    with sanitizer.open('w') as log:raw=subprocess.check_output([str(binary)],stderr=log)
    cases=json.loads(raw);assert len(cases)==45360
    def tier(base,rank,key):
        remaining=rank-1;value=base
        for i,size in enumerate([7,8,6,6,10000],1):
            take=min(size,remaining);value+=take*int(row[key+str(i)]);remaining-=take
        return value
    for case in cases:
        rank,a,b,master,crit,roll,profile,scene,start,effect,fills,draws,calc,alloc,drains,overlays,ar,mana,lo,hi,bound,value,phys_raw,fire_raw,phys,fire,flags,hitflags,hitclass,hp,trace=case
        present=scene!=2;selected=scene!=3;success=scene not in [1,5,11];contact=present and selected and success
        low=(tier(int(row['EMin']),rank,'EMinLev')<<8);low+=low*((a+b)*22)//100;low+=low*master//100
        high=(tier(int(row['EMax']),rank,'EMaxLev')<<8);high+=high*((a+b)*22)//100;high+=high*master//100
        assert (lo,hi,mana)==((low,high,1024) if present else (0,0,0))
        assert (start,fills,draws)==(int(present and selected),int(contact),int(contact))
        allocated=contact and scene!=9;calculated=contact and scene!=6
        assert (alloc,calc)==(int(allocated),int(calculated))
        assert ar==(50+(rank-1)*15 if present and selected and scene!=7 else -1)
        if contact:assert bound==hi-lo and value==(bound-1 if roll else 0)
        if allocated:
            p=(40*256+37)*(2 if crit else 1);f=11*256+13+lo+value
            if calculated:
                assert (phys_raw,fire_raw)==(p,f)
                p=p*(100-(50 if profile==1 else 100 if profile in [2,4] else 0))//100
                f=f*(100-(25 if profile==1 else 100 if profile>=3 else 0))//100
            assert (phys,fire,hitclass)==(p,f,0x20)
            expected_flags=1|(8192 if crit else 0)|(32768 if scene==6 else 0)|(2 if calculated and ((p+f)&0xFFFFFF00)>(hp&0xFFFFFF00) else 0)
            assert flags==expected_flags and hitflags==2|32|(4096 if scene==10 else 0)
        else:assert (phys,fire,flags,hitflags,hitclass)==(0,0,0,0,0)
        has_release_target=present and selected and scene!=4
        packet_exposed=allocated and scene!=8
        expected_overlay=has_release_target and row.get('srvoverlay') and scene!=11 and packet_exposed
        expected_drain=has_release_target and (packet_exposed or scene==11)
        assert (effect,overlays,drains)==(int(has_release_target),int(bool(expected_overlay)),int(bool(expected_drain)))
        expected_trace=[]
        if present and selected and scene!=7:expected_trace += [1]
        if contact:expected_trace += [2,3,4]
        if calculated:expected_trace += [5]
        if allocated:expected_trace += [6]
        if expected_overlay:expected_trace += [7]
        if expected_drain:expected_trace += [8]
        assert trace==expected_trace,(case,expected_trace)
    fixture=BASE/'druid-fire-claws-source-probe.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    assert sanitizer.read_text()==''
    result=dict(referenceVersion='3.3.93847',sourcePreparation=True,nativeAccepted=False,handlerEnabled=False,legacyCommit=tree['sha'],preflightSha256=sha(pinpath),
        contexts=len(cases),completeRoutineHashes=routines,pinnedEnumHashes=enums,sourceHashes=hashes,generatedSourceSha256=sha(generated),compilerLogSha256=sha(compiler),sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),
        columns=['rank','hardFirestorm','hardBoulder','mastery','declaredCritical','upperRoll','mitigationProfile','scene','start','do','weaponFills','skillDraws','calculations','allocations','drains','overlays','attackPercent','mana256','minFire256','maxFire256','rngBound','rngValue','rawPhysical256','rawFire256','physical256','fire256','resultFlags','hitFlags','hitClass','targetLife256','orderedServices'],
        established=[
            'Thirteen complete unchanged older routines execute45360bounded contexts: start58/result helper,mastered integer elemental endpoints/length/mana/AR,full elemental writer,AllocCombat and do2. Header enums use their pinned original values. Current hard-only Firestorm/Boulder synergy supplies22percent per point through an explicitly injected formula service.',
            'Complete start58 sets fill-once hitflag2,requests weapon fill,then rolls/adds skill fire,then allocates. Complete AllocCombat skips a second fill. Its calculation precedes copying the packet. do2requests the successful fire-hit overlay before combat/durability drain. Source skill fire is additive to the declared weapon fire and is not multiplied by the declared physical critical. Minimum/maximum mastery truncates before exclusive-span RNG.',
            'Missing records/targets,miss,dodge,target loss and missing combat packets have explicit results. With current positive overlay,do2returns early if no packet exists,without invoking drain. Synthetic force-hit/inconsistent weaponblock/object/hitclass/no-overlay controls test bounded branches only; they are not claims about reachable modern Fire Claws contacts.'
        ],declaredServices=[
            'Weapon fill,critical choice,mitigation,total damage,combat allocation memory and durability/health execution remain declared services. Their arithmetic is controlled test input,not complete source combat/HP/leech/proc/block/dodge/seed acceptance. Complete routines compose around them unchanged; no whole legacy or current executable is compiled.',
            'Formula IDs and minimal struct layouts are declared surrogate services,not Fog/ABI execution. Current row strings/values remain byte-pinned. RNG chooses low/high legal values; the game seed algorithm is not reproduced. fire_hit overlay uses synthetic positive ID37 solely for the branch; actual overlay lookup/assets are not certified. Non-fire elemental writer branches and aura list callbacks are guarded unexecuted.',
            'Ranks1,2,8,9,20,28,60,hard0/1/20 and mastery0/25/100 avoid old signed intermediate overflow under ASan/UBSan. Higher-rank overflow behavior remains unsupported by this probe; no implicit wrap repair or current-build parity is claimed.'
        ],remaining=[
            'Connect a native Fire Claws route only after the frozen Feral full regression is terminal and accepted. Reuse shared form A1,prepared weapon packet/physical-only critical,fire endpoint/mastery,source stun/leech and cancellation. Preserve weapon/critical-before-skill-fire ordering and no do120charge gain/consumption. Verify hard-versus-soft synergies,both forms,gear/4mana/target/immune/miss/block/owner/input/save behavior and current executable gaps.',
            'No new handler,class completion,installed build/save change or final art is established by this source preparation. Fire-hit/body/contact/cast/expiry audio and talent/left/right/quick icons remain separate placeholders.'
        ])
    out=ROOT/'tools/reference/druid-fire-claws-engine-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print(f'Fire Claws:{len(cases)}complete-source contexts/{len(routines)}routines,ASan/UBSan clean; native handler not claimed.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();build(args.check)
