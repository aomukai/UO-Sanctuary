#!/usr/bin/env python3
"""Execute complete source Bone Spear damage creation and generic hit routing."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'bone-spear-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(path):
        p=BASE/'d2moo-pinned-source'/path;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);return p,raw.decode()
    common,commonText=source('source/D2Common/src/Units/Missile.cpp');mode,modeText=source('source/D2Game/src/MISSILES/MissMode.cpp');skills,skillText=source('source/D2Common/src/D2Skills.cpp')
    launch,launchText=source('source/D2Game/src/SKILLS/Skills.cpp');launchBody=function(launchText,'sub_6FD11420')
    assert 'missileParams.dwFlags = 33;' in launchBody and '!bCheckItemQuantity' in launchBody
    refs=ROOT/'tools/reference/d2r-named-equipment/json';s=json.loads((refs/'skills.json').read_text())['84'];m=json.loads((refs/'missiles.json').read_text())['bonespear']
    assert s['srvmissile']=='bonespear' and s['EType']=='mag' and not s.get('SrcDam') and m['*ID']==192
    assert [m.get(k,0) for k in ['pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc','CollideKill','LastCollide','ToHit','NextHit','Pierce','Size','Range','Vel']]==[1,0,0,0,1,0,0,0,1,40,24]
    damageParent=BASE/'molten-boulder-damage-source-probe.cpp';damage=damageParent.read_text().split('void setup(')[0]
    for text,name in [(commonText,'MISSILE_CalculateDamageData'),(commonText,'MISSILE_SetDamageStats'),(skillText,'SKILLS_GetMinElemDamage'),(skillText,'SKILLS_GetMaxElemDamage')]:assert function(text,name) in damage
    damage=damage.replace('struct D2UnitStrc{','struct D2UnitStrc{int pSeed=0;')
    roll=function(modeText,'MISSMODE_RollDamageValue')
    damage+='\nint calls=0;int ITEMS_RollLimitedRandomNumber(int*,int bound){calls++;return bound>0?bound-1:0;}\nint MONSTERUNIQUE_CalculatePercentage(int a,int b,int c){return int(int64_t(a)*b/c);}\n'+roll
    damage+='\nvoid setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_MAGIC;row.nSkill=84;row.nSrcDamage=row.nSrcMissDmg=0;skill.dwELen=0;for(int&v:skill.dwELevLen)v=0;'
    for member,key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:damage+=f'skill.{member}={s[key]};'
    for i in range(5):
        for member,key in [('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:damage+=f'skill.{member}[{i}]={s.get(key+str(i+1),0)};'
    damage+='}\n'+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int points:{0,1,20,80})for(int item:{0,99999})for(int crit:{0,1}){
owner={};weapon={};missile={};missileData={};setup();missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=192;missileData.nSkill=84;missileData.nLevel=rank;hard=points;volcano=0;critical=crit;critCalls=0;
for(int&v:owner.stats)v=item;D2UnitStrc origin;for(int&v:origin.stats)v=99999;D2MissileDamageDataStrc packet;
MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);calls=0;
int rolled=MISSMODE_RollDamageValue(&missile,STAT_MAGICMINDAM,STAT_MAGICMAXDAM,-1);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,points,item,crit,packet.nMinDamage,packet.nMaxDamage,packet.nMagicMinDamage,packet.nMagicMaxDamage,packet.nFlags,critCalls,rolled,calls);first=false;
}printf("]\n");}
'''
    routeParent=BASE/'molten-boulder-routing-source-probe.cpp';route=routeParent.read_text().split('void Prepare(int kind)')[0];router=function(modeText,'MISSMODE_SrvDmgHitHandler');assert router in route
    route+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int victim:{0,1,2})for(int repeat:{0,1})for(int terminal:{0,1})for(int owned:{0,1})for(int friendly:{0,1})for(int firstId:{10,99}){
Reset(123);actor.pMissileData=&actor.md;actor.dwClassId=192;actor.dwMissileMode=8;target.dwUnitType=victim==2?UNIT_PLAYER:UNIT_MONSTER;target.dwUnitId=firstId;
row.wSrvHitFunc=row.wSrvDmgFunc=row.nCollideKill=row.nAlwaysExplode=0;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];hostile=!friendly;hasOwner=owned;justHit=false;resets=hitEvents=direct=0;directFlags.clear();
if(repeat){actor.md.nOwnerType=target.dwUnitType;actor.md.dwOwnerGUID=target.dwUnitId;}
int result=MISSMODE_SrvDmgHitHandler(&game,&actor,victim?&target:nullptr,terminal);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",victim,repeat,terminal,owned,friendly,firstId,result,fills,direct,hitEvents,resets,actor.md.dwOwnerGUID);first=false;
}printf("]\n");}
'''
    artifacts=[];outputs={}
    for name,cpp in [('damage',damage),('route',route)]:
        generated=STAGE/(name+'.cpp');generated.write_text(cpp);binary=STAGE/name
        run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],capture_output=True)
        log=STAGE/(name+'-compiler.log');log.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
        run=subprocess.run([str(binary)],capture_output=True);san=STAGE/(name+'-sanitizer.log');san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
        outputs[name]=json.loads(run.stdout);fixture=STAGE/(name+'-cases.json');fixture.write_text(json.dumps(outputs[name],separators=(',',':'))+'\n');artifacts+=[generated,log,san,fixture]
    assert len(outputs['damage'])==4080 and len(outputs['route'])==96
    for rank,hard,item,crit,physicalMin,physicalMax,lo,hi,flags,critCalls,rolled,calls in outputs['damage']:
        def endpoint(stem):
            value=s[stem];remaining=rank-1
            for i,limit in enumerate([7,8,6,6,999],1):n=min(limit,remaining);value+=n*s.get(stem+'Lev'+str(i),0);remaining-=n
            value<<=s['HitShift'];return value+value*hard*8//100
        assert (physicalMin,physicalMax,lo,hi,flags,critCalls,rolled,calls)==(0,0,endpoint('EMin'),endpoint('EMax'),0,0,endpoint('EMax')-1,1)
    for victim,repeat,terminal,owned,friendly,id,result,fills,direct,events,resets,last in outputs['route']:
        rejected=bool(victim and (repeat or owned and friendly));removed=bool(terminal and not rejected)
        assert (result,fills,direct,events,resets)==(2 if removed else 1,int(bool(victim) and not rejected),int(bool(victim) and not rejected),int(bool(victim or terminal) and not rejected),int(removed))
        assert last==(id if victim else -1) if not(repeat and not victim) else last==id
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],damageCases=4080,routeCases=96,
        completeRoutines={n:hashlib.sha256(function(text,n).encode()).hexdigest() for text,n in [(commonText,'MISSILE_CalculateDamageData'),(commonText,'MISSILE_SetDamageStats'),(skillText,'SKILLS_GetMinElemDamage'),(skillText,'SKILLS_GetMaxElemDamage'),(modeText,'MISSMODE_RollDamageValue'),(modeText,'MISSMODE_SrvDmgHitHandler')]},
        launchStaticReviewSha256=hashlib.sha256(launchBody.encode()).hexdigest(),inputs={str(p.relative_to(ROOT)):sha(p) for p in [common,mode,skills,launch,damageParent,routeParent,refs/'skills.json',refs/'missiles.json']},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=['Complete creator/skill getters/stat setter/impact roll use current skill84/missile192. SrcDam0 excludes supplied weapon/item/origin stats and critical;all255ranks/four synergy totals have exact FP256 magic endpoints and max-exclusive rolls.',
            'Complete generic hit router executes the no-hit/no-damage-function Bone Spear route. LastCollide rejects only the immediately recorded target;hostility rejection follows its write. Direct hits survive;terminal wall/expiry removes. Supplied event/Fill/SetDamageFlags services record their calls.',
            'Static generic launch helper uses flags33,current target or path point and no quantity use for this spell. Existing shared factory/vector/steps/do1 services remain separately verified primitives.'],
        remaining=['Full engine stat preparation,actor/world RNG allocation,current executable and native room/candidate/terrain equivalence are not certified. Magic item mastery and approved Sanctuary potion additions remain explicit native adapters.',
            'The source router packet/defense/HP/event services are declared boundaries. Native casts,mitigation,ownership,piercing,slow and actual saved flight/input require their own evidence.'])
    path=ROOT/'tools/reference/bone-spear-source-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print('Bone Spear:4080complete source damage and96generic-router cases;magic SrcDam0,latest collider and unlimited piercing verified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
