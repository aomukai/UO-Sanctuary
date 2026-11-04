#!/usr/bin/env python3
"""Compose complete Lightning Strike missile damage creation and source getters."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'lightning-strike-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    parent=BASE/'molten-boulder-damage-source-probe.cpp';cpp=parent.read_text().split('void setup(')[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    routines={};paths=[Path(__file__),parent]
    for rel,names in [('source/D2Common/src/Units/Missile.cpp',['MISSILE_CalculateDamageData','MISSILE_SetDamageStats']),('source/D2Common/src/D2Skills.cpp',['SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage'])]:
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
        paths.append(p)
        for name in names:
            body=function(raw.decode(),name);assert body in cpp;routines[name]=hashlib.sha256(body.encode()).hexdigest()
    formula='int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?volcano*12:hard*8;}'
    assert cpp.count(formula)==1;cpp=cpp.replace(formula,formula.replace('hard*8','hard*11'))
    mode=BASE/'d2moo-pinned-source/source/D2Game/src/MISSILES/MissMode.cpp';raw=mode.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Game/src/MISSILES/MissMode.cpp')
    roll=function(raw.decode(),'MISSMODE_RollDamageValue');routines['MISSMODE_RollDamageValue']=hashlib.sha256(roll.encode()).hexdigest();paths.append(mode)
    cpp=cpp.replace('struct D2UnitStrc{','struct D2UnitStrc{int pSeed=0;')
    cpp+='\nint draws=0;int ITEMS_RollLimitedRandomNumber(int*,int bound){draws++;return bound>0?bound-1:0;}\nint MONSTERUNIQUE_CalculatePercentage(int a,int b,int c){return int(int64_t(a)*b/c);}\n'+roll
    refs=ROOT/'tools/reference/d2r-named-equipment/json';skill=json.loads((refs/'skills.json').read_text())['34'];missile=json.loads((refs/'missiles.json').read_text())['lightningstrike']
    assert skill['skill']=='Lightning Strike'and not skill.get('SrcDam')and missile['Skill']=='Lightning Strike'and missile['EMin']==8
    cpp+='\nvoid setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_LTNG;row.nSkill=34;row.nElemType=ELEMTYPE_LTNG;row.nHitShift=8;skill.dwELen=0;for(int&v:skill.dwELevLen)v=0;'
    for member,key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:cpp+=f'skill.{member}={skill[key]};'
    for i in range(5):
        for member,key in [('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:cpp+=f'skill.{member}[{i}]={skill.get(key+str(i+1),0)};'
    cpp+='}\n'+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");
for(int rank=1;rank<=255;rank++)for(int points:{0,1,20,60})for(int mastery:{0,25,40})for(int item:{0,99999})for(int criticalInput:{0,1})for(int local:{0,8,999}){
owner={};weapon={};missile={};missileData={};setup();row.dwElemMin=local;row.dwElemMax=local+1;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=232;missileData.nSkill=34;missileData.nLevel=rank;hard=points;volcano=0;critical=criticalInput;critCalls=0;
for(int stat:{STAT_MINDAMAGE,STAT_MAXDAMAGE,STAT_FIREMINDAM,STAT_FIREMAXDAM,STAT_COLDMINDAM,STAT_COLDMAXDAM,STAT_LIGHTMINDAM,STAT_LIGHTMAXDAM,STAT_MAGICMINDAM,STAT_MAGICMAXDAM,STAT_POISONMINDAM,STAT_POISONMAXDAM,STAT_DAMAGEPERCENT,STAT_ITEM_DEADLYSTRIKE,STAT_LIFEDRAINMINDAM,STAT_MANADRAINMINDAM})owner.stats[stat]=weapon.stats[stat]=item;
owner.stats[STAT_PASSIVE_LTNG_MASTERY]=mastery;D2UnitStrc origin;for(int&v:origin.stats)v=99999;
D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);draws=0;
int rolled=MISSMODE_RollDamageValue(&missile,STAT_LIGHTMINDAM,STAT_LIGHTMAXDAM,-1);
int other=packet.nFireMinDamage+packet.nFireMaxDamage+packet.nColdMinDamage+packet.nColdMaxDamage+packet.nMagicMinDamage+packet.nMagicMaxDamage+packet.nPoisonMinDamage+packet.nPoisonMaxDamage;
int drain=packet.nLifeDrainMinDamage+packet.nLifeDrainMaxDamage+packet.nManaDrainMinDamage+packet.nManaDrainMaxDamage;
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",comma?",":"",rank,points,mastery,item,criticalInput,local,packet.nMinDamage,packet.nMaxDamage,packet.nLightMinDamage,packet.nLightMaxDamage,other,drain,critCalls,packet.nFlags,rolled,draws,missile.stats[STAT_LIGHTMINDAM],missile.stats[STAT_LIGHTMAXDAM]);comma=true;
}printf("]\n");}
'''
    STAGE.mkdir(exist_ok=True);source=STAGE/'probe.cpp';source.write_text(cpp)
    compiled=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],capture_output=True)
    (STAGE/'compiler.log').write_bytes(compiled.stdout+compiled.stderr);assert compiled.returncode==0,compiled.stderr.decode()[-3000:]
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    rows=json.loads(run.stdout);assert len(rows)==36720
    for r in rows:
        rank,points,mastery=r[:3];maximum=skill['EMax'];left=rank-1
        for i,limit in enumerate([7,8,6,6,999],1):n=min(left,limit);maximum+=n*skill.get('EMaxLev'+str(i),0);left-=n
        maximum*=256;maximum+=maximum*points*11//100;maximum+=maximum*mastery//100;minimum=256+256*mastery//100
        assert r[6:]==[0,0,minimum,maximum,0,0,0,0,maximum-1,1,minimum,maximum]
    cases=STAGE/'cases.json';cases.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    paths +=[refs/'skills.json',refs/'missiles.json',source,cases,STAGE/'compiler.log',STAGE/'sanitizer.log']
    result=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',legacyCommit=tree['sha'],cases=len(rows),completeRoutines=routines,
        evidence={str(p.relative_to(ROOT)):sha(p)for p in paths},
        established=['Complete creator/skill-getter/stat-setter/impact-roll composition uses current skill34/missile232 for255ranks,four hard-synergy totals,three masteries and independent item/critical/local-missile controls.',
            'Skill lightning endpoints take precedence over missile-local EMin8. Skill SrcDam0 excludes weapon/origin physical,added elements,drain and critical. Minimum1 is not synergized;hard ChargedStrike/LightningBolt/PowerStrike11percent totals raise only maximum. Mastery follows synergy and maximum-exclusive roll follows stat installation.'],
        limits=['Formula/stat/equipment/RNG leaves remain declared services. This does not prove modern initial-bolt coordinates,first collision/primary re-hit/solo damage or a current executable damage-order certificate.',
            'No learned handler is enabled by this source composition. Shared-chain native integration and the first-bolt design/reference decision remain separate.'])
    target=ROOT/'tools/reference/lightning-strike-damage-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Lightning Strike missile damage:36720 complete creator/getter/setter/roll contexts;ASan/UBSan clean;initial aim remains separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
