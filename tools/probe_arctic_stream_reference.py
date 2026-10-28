#!/usr/bin/env python3
"""Execute unchanged cold getters/creator/writer and Arctic flags0x8020 factory."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'arctic-stream'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes,routines={},{}
    def body(relative,name):
        p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(p);text=function(raw.decode(),name)
        routines[name]=hashlib.sha256(text.encode()).hexdigest();return text
    parent=json.loads((ROOT/'tools/reference/molten-boulder-damage-source-evidence.json').read_text())
    cpp=(STAGE/'damage-abi-base.cpp').read_text().split('void setup(',1)[0]
    common='source/D2Common/src/D2Skills.cpp';missile='source/D2Common/src/Units/Missile.cpp'
    for relative,names in [(common,['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage']),
        (missile,['MISSILE_CalculateDamageData','MISSILE_AddStatsToDamage','MISSILE_CalculateFinalDamage','MISSILE_SetDamageStats','MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage'])]:
        for name in names: assert body(relative,name) in cpp,name
    before='return formula==2?volcano*12:hard*8;';assert cpp.count(before)==1
    cpp=cpp.replace(before,'return formula==2?0:hard*15;',1)
    directory=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((directory/'sources.json').read_text());tables={};pins={}
    for name in ['skills','missiles']:
        p=directory/f'json/{name}.json';pins[name]=sha(p);assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(p.read_text())
    s=tables['skills']['230'];m=tables['missiles']['arcticblast1']
    assert s['EDmgSymPerCalc']=="skill('Cyclone Armor'.blvl)*par8" and s['Param8']==15 and s['EType']=='cold'
    assert m['Skill']=='Arctic Blast' and m['DamageRate']==41 and not m.get('CanSlow') and not m.get('SrcDamage')
    cpp+='void setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_COLD;'
    for dest,key in [('dwMinDam','MinDam'),('dwMaxDam','MaxDam'),('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift'),('dwELen','ELen')]:cpp+=f'skill.{dest}={s.get(key,0)};'
    for i in range(5):
        for dest,key in [('dwMinLvlDam','MinLevDam'),('dwMaxLvlDam','MaxLevDam'),('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:cpp+=f'skill.{dest}[{i}]={s.get(key+str(i+1),0)};'
    for i in range(3):cpp+=f'skill.dwELevLen[{i}]={s.get("ELevLen"+str(i+1),0)};'
    cpp+='row.nSkill=230;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.dwElemDmgSymPerCalc=-1;}\n'
    cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int points:{0,1,20})for(int mastery:{0,7,50})for(int noise:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup();weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=459;missileData.nSkill=230;missileData.nLevel=rank;hard=points;critical=true;critCalls=0;
 for(int& stat:owner.stats)stat=noise;owner.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;
 D2UnitStrc origin;for(int& stat:origin.stats)stat=noise;D2MissileDamageDataStrc p;MISSILE_CalculateDamageData(&p,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&p,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,points,mastery,noise,p.nColdMinDamage,p.nColdMaxDamage,p.nColdLength,p.nMinDamage,p.nMaxDamage,p.nFireMaxDamage,p.nFlags,critCalls,missile.stats[STAT_COLDLENGTH]);first=false;
}printf("]\n");}'''
    def execute(cpp,name):
        p=STAGE/(name+'.cpp');p.write_text(cpp);compiler=p.with_suffix('.compiler.log');sanitizer=p.with_suffix('.sanitizer.log')
        with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
        run=subprocess.run([str(p.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2500:]
        data=json.loads(run.stdout);output=BASE/(name+'-source-probe.json');output.write_bytes(run.stdout)
        artifacts.extend([p,compiler,sanitizer,output]);return data
    artifacts=[Path(__file__).resolve(),STAGE/'damage-abi-base.cpp',STAGE/'factory-abi-base.cpp',ROOT/'tools/reference/arctic-blast-preflight.json']
    damage=execute(cpp,'arctic-stream-damage');assert len(damage)==4590
    def endpoint(maximum,rank):
        key='EMax' if maximum else 'EMin';value=s[key];left=rank-1
        for i,length in enumerate([7,8,6,6,2**30],1):
            count=min(left,length);value+=count*s[key+'Lev'+str(i)];left-=count
        return value<<s['HitShift']
    for rank,hard,mastery,noise,lo,hi,frames,pmin,pmax,fire,flags,crits,stored in damage:
        ends=[]
        for maximum in [False,True]:
            value=endpoint(maximum,rank);value+=value*hard*15//100;value+=value*mastery//100;ends.append(value)
        assert [lo,hi,frames,pmin,pmax,fire,flags,crits,stored]==[*ends,100+15*(rank-1),0,0,0,0,0,100+15*(rank-1)]
    factory=(STAGE/'factory-abi-base.cpp').read_text().split('int main()',1)[0]
    name='MISSILES_CreateMissileFromParams';assert body('source/D2Game/src/MISSILES/Missiles.cpp',name) in factory
    factory=factory.replace('void MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc*){}','D2UnitStrc* lastCollider=nullptr;void MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc* p){lastCollider=p;}')
    factory+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int dx:{-100,-99,-1,0,1,99,100})for(int dy:{0,1})for(int slowed:{0,1})for(int allocation:{0,1}){
 owner={};owner.pDynamicPath=&owner.path;owner.x=400;owner.y=700;owner.dwUnitType=UNIT_PLAYER;created={};created.pDynamicPath=&created.path;lastCollider=nullptr;
 row={};row.nVel=row.nMaxVel=12;row.nVelLev=0;row.wRange=30;row.nActivate=0;row.nCollideType=3;row.dwDamageRate=41;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|gdwBitMasks[MISSILESFLAGINDEX_GETHIT];
 hasRoom=hasRecord=true;allocated=allocation;slow=slowed;creates=draws=repaths=0;
 D2MissileStrc p;p.pOwner=p.pOrigin=&owner;p.dwFlags=0x8020;p.nMissile=459;p.nSkill=230;p.nSkillLevel=rank;p.nTargetX=400+dx;p.nTargetY=700+dy;p.nRange=(35+2*(rank-1))/2;
 auto u=MISSILES_CreateMissileFromParams(&game,&p);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",rank,dx,dy,slowed,allocation,u!=nullptr,creates,draws);first=false;
 if(u)printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",u->x,u->y,u->path.x,u->path.y,u->path.velocity,u->total,u->remaining,u->activate,u->skill,u->rank,lastCollider==&owner,u->stats[STAT_DAMAGE_FRAMERATE]);printf("]]");
}printf("]\n");}'''
    factoryCases=execute(factory,'arctic-stream-factory');assert len(factoryCases)==14280
    for rank,dx,dy,slow,allocation,made,attempts,draws,shot in factoryCases:
        valid=abs(dx)<100 and abs(dy)<100
        assert [made,attempts,draws]==[int(valid and allocation),int(valid),0]
        if made:
            if dx==dy==0:dx=dy=1
            frames=(35+2*(rank-1))//2
            assert shot==[400,700,400+dx,700+dy,2304,frames,frames,frames,230,rank,1,41]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],learnedHandlerEnabled=False,currentExecutableParity=False,
        sourceHashes=hashes,completeRoutineHashes=routines,pinnedInputHashes=pins,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        verified=dict(damageContexts=len(damage),factoryContexts=len(factoryCases),sanitizers='ASan/UBSan exit0; empty runtime stderr'),
        established=['Complete source getters/creator/writer use all255current Arctic ranks,independent0/1/20hard Cyclone Armor,0/7/50cold item mastery and99999unrelated owner/origin stat controls. Cold synergy follows HitShift3; mastery truncates once after synergy. Length100+15per rank; physical/fire/critical inheritance remains zero.',
            'Complete factory with playerdo19flags0x8020 overrides tableRange30using floor((35+2*(rank-1))/2),sets actual last-collider owner,skill230/rank and DamageRate41. Vel12becomes2304FP8. CanSlow0ignores the supplied50percent Slow Missiles velocity; self aim nudges(+1,+1),absolute deltas>=100fail before allocation.'],
        services=['Explicit ABI/stat/formula/ratio/weapon services; unselected weapon branches are retained. Factory room/allocation/path/owner/damage services are supplied,not current executable simulation. The complete cold damage producer and complete factory execute separately; source do19-to-factory flags were already checked in player-inferno-channel.',
            'Public table hashes and licensed214-field preflight identify3.3.93847. Current input/FCR/callback cadence,owner lifecycle,room order/terrain and native damage commitment need their own checks. The rounded12–24tooltip is not used to calibrate an emission rate.'])
    p=ROOT/'tools/reference/arctic-stream-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print(f'Arctic cold producer: {len(damage)} contexts; complete factory: {len(factoryCases)} contexts; sanitizers clean.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
