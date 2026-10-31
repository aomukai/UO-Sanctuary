#!/usr/bin/env python3
"""Run Bone Spirit's complete legacy guidance/factory and source-zero packet.

Current tables are pinned; supplied world/path/damage services are explicit.
"""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'bone-spirit-source'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={};bodies={}
    def routine(path,name):
        p=BASE/'d2moo-pinned-source'/path;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        hashes[path]=sha(p);body=function(raw.decode(),name);bodies[name]=hashlib.sha256(body.encode()).hexdigest();return body
    refs=ROOT/'tools/reference/d2r-named-equipment/json';skill=json.loads((refs/'skills.json').read_text())['93'];missile=json.loads((refs/'missiles.json').read_text())['bonespirit']
    assert skill['srvdofunc']==10 and skill['calc1']==0 and skill['srvmissilea']==skill['srvmissileb']=='bonespirit' and not skill.get('SrcDam') and skill['EType']=='mag'
    assert missile['*ID']==193 and [missile.get(k,0) for k in ['pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc','Vel','Range','CollideType','CollideKill','LastCollide','NextHit','AlwaysExplode','Param1','Param2']]==[7,10,0,12,128,3,1,0,0,1,5,15]
    parent=BASE/'guided-arrow-source-probe.cpp';cpp=parent.read_text();shared=cpp.split('void reset(){')[0]
    sharedNames=[('source/D2Game/src/SKILLS/SkillAma.cpp',n) for n in ['SKILLS_AddDamagePercentBonus','SKILLS_SrvDo010_GuidedArrow_BoneSpirit']]
    sharedNames += [('source/D2Game/src/MISSILES/Missiles.cpp','MISSILES_CreateMissileFromParams')]
    sharedNames += [('source/D2Game/src/MISSILES/MissMode.cpp',n) for n in ['MISSMODE_SrvDo07_GuidedArrow_BoneSpirit','MISSMODE_SrvHit10_GuidedArrow_BoneSpirit']]
    sharedNames += [('source/D2Game/src/SKILLS/Skills.cpp',n) for n in ['sub_6FD10880','sub_6FD107F0']]
    sharedNames += [('source/D2Common/src/Units/Units.cpp',n) for n in ['UNITS_GetDistanceToCoordinates','UNITS_GetDistanceToOtherUnit']]
    for path,name in sharedNames:assert routine(path,name) in shared
    # Only declared schema defaults and supplied test inputs change. All complete
    # factory/launch/turn/hit/selector/distance bodies remain byte-for-byte equal.
    shared=shared.replace('nVel=24','nVel=12').replace('nMaxVel=24','nMaxVel=12').replace('wSrvMissileA=86,wSrvMissileB=86','wSrvMissileA=193,wSrvMissileB=193')
    main=cpp[cpp.index('void reset(){'):]
    main=main.replace('for(int bonus:{0,12,145})for(int rank:{1,20})','for(int bonus:{0})for(int rank:{1,20,255})for(int slowed:{0,1})')
    main=main.replace('selected=targeted?&target:nullptr;calc=bonus;','selected=targeted?&target:nullptr;calc=bonus;slow=slowed;')
    main=main.replace('(&game,&owner,22,rank)','(&game,&owner,93,rank)')
    old='[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",targeted,dx,dy,bonus,rank,cross,result,created.mode,created.packed,created.remaining,created.total,created.path.velocity,creationPercent,draws'
    new='[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",targeted,dx,dy,bonus,rank,cross,slowed,result,created.mode,created.packed,created.remaining,created.total,created.path.velocity,creationPercent,draws'
    assert old in main;main=main.replace(old,new)
    guidance=shared+main
    damageParent=BASE/'bone-spear-source/damage.cpp';damage=damageParent.read_text().split('void setup(){')[0]
    for path,name in [('source/D2Common/src/Units/Missile.cpp',n) for n in ['MISSILE_CalculateDamageData','MISSILE_SetDamageStats']]+[('source/D2Common/src/D2Skills.cpp',n) for n in ['SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage']]+[('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_RollDamageValue')]:assert routine(path,name) in damage
    damage+='\nvoid setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_MAGIC;row.nSkill=93;row.nSrcDamage=row.nSrcMissDmg=0;skill.dwELen=0;for(int&v:skill.dwELevLen)v=0;'
    for member,key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:damage+=f'skill.{member}={skill[key]};'
    for i in range(5):
        for member,key in [('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:damage+=f'skill.{member}[{i}]={skill.get(key+str(i+1),0)};'
    damage+='}\n'+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int points:{0,1,20,80})for(int item:{0,99999})for(int crit:{0,1}){
owner={};weapon={};missile={};missileData={};setup();missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=193;missileData.nSkill=93;missileData.nLevel=rank;hard=points;volcano=0;critical=crit;critCalls=0;
for(int&v:owner.stats)v=item;D2UnitStrc origin;for(int&v:origin.stats)v=99999;D2MissileDamageDataStrc packet;
MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);calls=0;
int rolled=MISSMODE_RollDamageValue(&missile,STAT_MAGICMINDAM,STAT_MAGICMAXDAM,-1);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,points,item,crit,packet.nMinDamage,packet.nMaxDamage,packet.nMagicMinDamage,packet.nMagicMaxDamage,packet.nFlags,critCalls,rolled,calls);first=false;
}printf("]\n");}
'''
    outputs={};artifacts=[]
    for name,text in [('guidance',guidance),('damage',damage)]:
        p=STAGE/(name+'.cpp');p.write_text(text);binary=STAGE/name
        run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True);log=STAGE/(name+'-compiler.log');log.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
        run=subprocess.run([str(binary)],capture_output=True);san=STAGE/(name+'-sanitizer.log');san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
        outputs[name]=json.loads(run.stdout);fixture=STAGE/(name+'-cases.json');fixture.write_text(json.dumps(outputs[name],separators=(',',':'))+'\n');artifacts.extend([p,log,san,fixture])
    data=outputs['guidance'];assert {k:len(v) for k,v in data.items()}==dict(launch=360,steer=2016,hit=288,select=40)
    for targeted,dx,dy,bonus,rank,cross,slow,result,mode,packed,remaining,total,velocity,percent,draws in data['launch']:
        speed=1152 if slow else 2304;distance=max(abs(dx),abs(dy))+min(abs(dx),abs(dy))//2
        assert (result,mode,remaining,total,velocity,percent,draws)==(1,1 if targeted else 2,128 if targeted else (distance<<16)//(16*speed),128,speed,0,0)
        assert packed&65535==dx&65535 and (packed>>16)&65535==dy&65535
    for flags,remaining,cadence,kind,dist,result,repaths,validations,collisions in data['steer']:
        expected=int(bool(flags&1) and kind==1 and remaining%(cadence if cadence>0 else 5)==0 and 3<dist<25)
        assert (result,repaths,validations,collisions)==(1,expected,int(bool(flags&1)),1)
    for flags,remaining,victim,wall,found,result,newMode,newRemaining,repaths,searches,targetChanged,x,y,filter in data['hit']:
        expected=1 if victim==0 and (flags&4 or wall&4) else (4 if victim==2 else 3) if flags&1 else 3 if not flags&2 else 4-int(bool(flags&4)) if remaining>0 else 1 if flags&4 else 4
        seeking=bool(flags==2 and remaining<=0 and not(victim==0 and wall&4))
        assert (result,searches,repaths,newMode,newRemaining)==(expected,int(seeking),int(seeking),(5 if found else 6) if seeking else flags,128 if seeking else remaining)
        if seeking:assert filter==0xA783 and targetChanged==found and ((x,y)==(30,77) if not found else True)
    for count,reversed,previous,result,seen,filter in data['select']:
        ids=[(i+1)*10 for i in range(count)];greater=[x for x in ids if x>previous] if previous>=0 else ids;expected=min(greater) if greater else min(ids) if ids else -1
        assert result==expected and filter==0xA783
    assert len(outputs['damage'])==4080
    for rank,hard,item,crit,physicalMin,physicalMax,lo,hi,flags,critCalls,rolled,calls in outputs['damage']:
        def endpoint(stem):
            value=skill[stem];remaining=rank-1
            for i,limit in enumerate([7,8,6,6,999],1):n=min(limit,remaining);value+=n*skill.get(stem+'Lev'+str(i),0);remaining-=n
            value<<=skill['HitShift'];return value+value*hard*8//100
        assert (physicalMin,physicalMax,lo,hi,flags,critCalls,rolled,calls)==(0,0,endpoint('EMin'),endpoint('EMax'),0,0,endpoint('EMax')-1,1)
    out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerAccepted=False,currentExecutableParity=False,
        cases=dict(launch=360,steer=2016,hit=288,select=40,damage=4080),sourceHashes=hashes,completeRoutineHashes=bodies,
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [parent,damageParent,refs/'skills.json',refs/'missiles.json',ROOT/'tools/reference/guided-arrow-engine-evidence.json']},artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=['Complete do10/factory use current Bone Spirit skill93/missile193,calc1zero and velocity12. Selected launch has mode1/current128frames;ground launch has mode2/current distance-derived frames while total remains128. Normal/50percent slow give2304/1152FP8. Packed signed original aim is retained. No weapon damage-percent initializer or pierce draw occurs.',
        'Complete do7 validates locked target and only repaths on remaining-frame cadence5 when size-adjusted distance is strictly3<d<25. Dead or hostile-invalid locked targets stop repathing;do7 does not search for replacements. A declared generic collision service still runs.',
        'Complete hit10 ignores other units for modes1/5. Ground mode2 ignores early units,then at its first expiry searches within15source subtiles,chooses the lowest candidate ID with previous=-1 and resets range128. Acquired mode5 or straight-extension mode6 do not reacquire on later expiry. Barrier/retarget/path callbacks remain declared services.',
        'Complete creator/getters/writer/roll use HitShift8 and8percent hard synergies across4080contexts. SrcDam0 excludes supplied weapon/item/origin/critical payloads;magic rolls are max-exclusive.'],
        remaining=['Generic collision/actual hit-result routing,movement/path flags and source steering/vector recomputation are not executed together by this probe. Declared world/room/allocation/target/filter/packet/stat services retain their bounds;the full modern executable and graphical client are not certified.',
        'Before replacing the provisional native loop,compose saved modes1/2/5/6,initial remaining versus total frames,ground extension,locked-target loss,turn cadence/distance,collision/terrain,actual spell defenses/resources/lifecycle/inputs and real reloads. Reuse GuidedArrows primitives where exact;do not copy its weapon packet or hard128remaining validation to slowed ground Spirit.'])
    path=ROOT/'tools/reference/bone-spirit-source-evidence.json';encoded=json.dumps(out,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Bone Spirit:360launch/factory+2016turn+288hit+40selector+4080damage contexts;ASan/UBSan clean. Native handler acceptance pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
