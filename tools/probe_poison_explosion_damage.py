#!/usr/bin/env python3
"""Execute Poison Explosion's raw poison-rate packet and rank-scaled poison length."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'poison-explosion-damage'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    parent=BASE/'bone-spear-source/damage.cpp';cpp=parent.read_text().split('void setup(){')[0];routines={};sources={}
    def verify(path,name):
        p=BASE/'d2moo-pinned-source'/path;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        body=function(raw.decode(),name);assert body in cpp;routines[name]=hashlib.sha256(body.encode()).hexdigest();sources[path]=sha(p)
    for name in ['MISSILE_CalculateDamageData','MISSILE_SetDamageStats']:verify('source/D2Common/src/Units/Missile.cpp',name)
    for name in ['SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength','SKILLS_CalculateMasteryBonus']:verify('source/D2Common/src/D2Skills.cpp',name)
    verify('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_RollDamageValue')
    cpp=cpp.replace('hard*8','hard*15')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';skill=json.loads((refs/'skills.json').read_text())['83'];missile=json.loads((refs/'missiles.json').read_text())['poisonexplosioncloud'];assert skill['EType']=='pois' and not skill.get('SrcDam') and skill['HitShift']==4 and skill['ELen']==50 and missile['*ID']==217
    cpp+='\nvoid setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_POIS;row.nSkill=83;row.nSrcDamage=row.nSrcMissDmg=0;skill.dwELen=50;skill.dwELenSymPerCalc=-1;for(int&v:skill.dwELevLen)v=10;'
    for member,key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:cpp+=f'skill.{member}={skill[key]};'
    for i in range(5):
        for member,key in [('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:cpp+=f'skill.{member}[{i}]={skill.get(key+str(i+1),0)};'
    cpp+='}\n'+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int points:{0,1,20,40})for(int item:{0,99999})for(int mastery:{0,25,100})for(int crit:{0,1}){
owner={};weapon={};missile={};missileData={};setup();missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=217;missileData.nSkill=83;missileData.nLevel=rank;hard=points;volcano=0;critical=crit;critCalls=0;
for(int&v:owner.stats)v=item;owner.stats[STAT_PASSIVE_POIS_MASTERY]=mastery;D2UnitStrc origin;for(int&v:origin.stats)v=99999;D2MissileDamageDataStrc packet;
MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);calls=0;
int rolled=MISSMODE_RollDamageValue(&missile,STAT_POISONMINDAM,STAT_POISONMAXDAM,STAT_PASSIVE_POIS_MASTERY);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,points,item,mastery,crit,packet.nMinDamage,packet.nMaxDamage,packet.nPoisonMinDamage,packet.nPoisonMaxDamage,packet.nPoisonLength,packet.nPoisonCount,packet.nFlags,critCalls,rolled,calls);first=false;
}printf("]\n");}
'''
    p=STAGE/'damage.cpp';p.write_text(cpp);binary=STAGE/'damage';run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True);compiler=STAGE/'compiler.log';compiler.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode();cases=json.loads(run.stdout);assert len(cases)==12240
    for rank,hard,item,mastery,crit,physicalMin,physicalMax,lo,hi,length,count,flags,critCalls,rolled,calls in cases:
        def endpoint(stem):
            value=skill[stem];remaining=rank-1
            for i,limit in enumerate([7,8,6,6,999],1):n=min(limit,remaining);value+=n*skill.get(stem+'Lev'+str(i),0);remaining-=n
            value<<=skill['HitShift'];value+=value*hard*15//100;return value+value*mastery//100
        assert (physicalMin,physicalMax,lo,hi,length,count,flags,critCalls,rolled,calls)==(0,0,endpoint('EMin'),endpoint('EMax'),50+10*(rank-1),0,0,0,endpoint('EMax')-1,1)
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerAccepted=False,currentExecutableParity=False,cases=12240,sourceHashes=sources,completeRoutineHashes=routines,
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [parent,refs/'skills.json',refs/'missiles.json']},artifacts={str(p.relative_to(ROOT)):sha(p) for p in [p,compiler,san,fixture]},
        established=['Complete source creator/getters/length/mastery/writer/impact-roll execute255ranks,four hard-synergy totals,three poison-masteries,supplied weapon/item/origin values and critical controls. Current HitShift4 stores a poison rate,not total damage. Length is50+10*(rank-1)frames,retaining all three current length tiers.',
        'Poison mastery applies after the15percent hard synergies to each endpoint with integer truncation. SrcDam0 excludes supplied weapon/origin poison/physical stats and critical. The skill packet has poison count0;its rank-scaled length must not be divided by an invented source count. Impact rolls are max-exclusive.'],
        remaining=['No actual poison application/resistance/duration scaling,initial-rate health subtraction,periodic ordering,credit/death,NextHit or world child lifecycle is executed here. The separate eight-child emitter/factory proof must be composed with the shared poison consumers before replacing the provisional field.',
        'Formula/stat lookup,positive percentage arithmetic and maximal random services are explicit. Full current executable behavior,actual item activation and native captured mastery/slow/pierce/input/save remain integration work. No runtime/data/class completion or installation is claimed.'])
    path=ROOT/'tools/reference/poison-explosion-damage-source-evidence.json';encoded=json.dumps(out,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Poison Explosion packet:12240complete rate/length/mastery/writer/roll contexts pass;native poison integration pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
