#!/usr/bin/env python3
"""Execute Teeth's complete source packet, roll, finder and generic hit router.

Modern delay identity remains distinct from the supplied historical JUSTHIT state.
"""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'teeth-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(path):
        p=BASE/'d2moo-pinned-source'/path;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);return p,raw.decode()
    common,ct=source('source/D2Common/src/Units/Missile.cpp');skills,st=source('source/D2Common/src/D2Skills.cpp');mode,mt=source('source/D2Game/src/MISSILES/MissMode.cpp')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';s=json.loads((refs/'skills.json').read_text())['67'];m=json.loads((refs/'missiles.json').read_text())['teeth']
    assert s['srvdofunc']==8 and s['srvmissilea']=='teeth' and s['EType']=='mag' and not s.get('SrcDam') and m['*ID']==114
    assert [m.get(k,0) for k in ['pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc','CollideKill','LastCollide','ToHit','NextHit','NextDelay','Pierce','Size','Range','Vel']]==[1,0,0,1,1,0,1,4,0,1,50,16]
    damageParent=BASE/'bone-spear-source/damage.cpp';damage=damageParent.read_text().split('void setup(){')[0]
    for text,name in [(ct,'MISSILE_CalculateDamageData'),(ct,'MISSILE_SetDamageStats'),(st,'SKILLS_GetMinElemDamage'),(st,'SKILLS_GetMaxElemDamage'),(mt,'MISSMODE_RollDamageValue')]:assert function(text,name) in damage
    # Parent's declared formula evaluator supplied 8% synergy. Teeth supplies 15%.
    assert 'hard*8' in damage;damage=damage.replace('hard*8','hard*15')
    damage+='\nvoid setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_MAGIC;row.nSkill=67;row.nSrcDamage=row.nSrcMissDmg=0;skill.dwELen=0;for(int&v:skill.dwELevLen)v=0;'
    for member,key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:damage+=f'skill.{member}={s[key]};'
    for i in range(5):
        for member,key in [('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:damage+=f'skill.{member}[{i}]={s.get(key+str(i+1),0)};'
    damage+='}\n'+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int points:{0,1,20,80})for(int item:{0,99999})for(int crit:{0,1}){
owner={};weapon={};missile={};missileData={};setup();missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=114;missileData.nSkill=67;missileData.nLevel=rank;hard=points;volcano=0;critical=crit;critCalls=0;
for(int&v:owner.stats)v=item;D2UnitStrc origin;for(int&v:origin.stats)v=99999;D2MissileDamageDataStrc packet;
MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);calls=0;
int rolled=MISSMODE_RollDamageValue(&missile,STAT_MAGICMINDAM,STAT_MAGICMAXDAM,-1);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,points,item,crit,packet.nMinDamage,packet.nMaxDamage,packet.nMagicMinDamage,packet.nMagicMaxDamage,packet.nFlags,critCalls,rolled,calls);first=false;
}printf("]\n");}
'''
    routeParent=BASE/'bone-spear-source/route.cpp';route=routeParent.read_text().split('int main(){')[0]
    assert function(mt,'MISSMODE_SrvDmgHitHandler') in route
    route=route.replace('struct D2UnitStrc{','struct D2UnitStrc{unsigned dwFlags=3;')
    route=route.replace('D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){return nullptr;}', 'bool allocate=true;int allocations=0,delay=-1;D2StatListStrc lease;D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int when,int,int){allocations++;delay=when;return allocate?&lease:nullptr;}')
    route=route.replace('void STATES_ToggleState(D2UnitStrc*,int,int){}','void STATES_ToggleState(D2UnitStrc*,int,int){justHit=true;}')
    route+='\nconstexpr int UNITFLAG_ISVALIDTARGET=1,UNITFLAG_CANBEATTACKED=2;struct D2MissileUnitFindArgStrc{D2MissilesTxt* pMissilesTxtRecord;D2UnitStrc* pMissile;D2UnitStrc* pOwner;D2GameStrc* pGame;};\n'
    finder=function(mt,'MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster');route+=finder
    route+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int victim:{0,1,2})for(int repeat:{0,1})for(int terminal:{0,1})for(int owned:{0,1})for(int friendly:{0,1})for(int delayed:{0,1})for(int allocation:{0,1}){
Reset(123);actor.pMissileData=&actor.md;actor.dwClassId=114;actor.dwMissileMode=3;target.dwUnitType=victim==2?UNIT_PLAYER:UNIT_MONSTER;target.dwUnitId=10;
row.wSrvHitFunc=row.wSrvDmgFunc=row.nToHit=row.nAlwaysExplode=0;row.nCollideKill=row.nNextHit=1;row.nNextDelay=4;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];hostile=!friendly;hasOwner=owned;justHit=delayed;allocate=allocation;allocations=0;delay=-1;resets=hitEvents=direct=0;
if(repeat){actor.md.nOwnerType=target.dwUnitType;actor.md.dwOwnerGUID=target.dwUnitId;}
D2MissileUnitFindArgStrc arg{&row,&actor,owned?&owner:nullptr,&game};int find=MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster(victim?&target:nullptr,&arg);
int result=MISSMODE_SrvDmgHitHandler(&game,&actor,victim?&target:nullptr,terminal);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",victim,repeat,terminal,owned,friendly,delayed,allocation,find,result,fills,direct,hitEvents,resets,allocations,delay,int(justHit));first=false;
}printf("]\n");}
'''
    outputs={};artifacts=[]
    for name,cpp in [('damage',damage),('route',route)]:
        p=STAGE/(name+'.cpp');p.write_text(cpp);binary=STAGE/name
        run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True);log=STAGE/(name+'-compiler.log');log.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
        run=subprocess.run([str(binary)],capture_output=True);san=STAGE/(name+'-sanitizer.log');san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
        outputs[name]=json.loads(run.stdout);fixture=STAGE/(name+'-cases.json');fixture.write_text(json.dumps(outputs[name],separators=(',',':'))+'\n');artifacts.extend([p,log,san,fixture])
    assert len(outputs['damage'])==4080 and len(outputs['route'])==192
    for rank,hard,item,crit,physicalMin,physicalMax,lo,hi,flags,critCalls,rolled,calls in outputs['damage']:
        def endpoint(stem):
            value=s[stem];remaining=rank-1
            for i,limit in enumerate([7,8,6,6,999],1):n=min(limit,remaining);value+=n*s.get(stem+'Lev'+str(i),0);remaining-=n
            value<<=s['HitShift'];return value+value*hard*15//100
        assert (physicalMin,physicalMax,lo,hi,flags,critCalls,rolled,calls)==(0,0,endpoint('EMin'),endpoint('EMax'),0,0,endpoint('EMax')-1,1)
    for victim,repeat,terminal,owned,friendly,delayed,allocate,find,result,fills,direct,events,resets,allocations,delay,state in outputs['route']:
        rejected=bool(victim and (repeat or delayed or owned and friendly));accepted=bool(victim and not rejected)
        assert find==int(accepted)
        assert (result,fills,direct,events,resets,allocations,delay,state)==(1 if rejected else 2,int(accepted),int(accepted),int(bool(not rejected and (victim or terminal))),int(not rejected),int(accepted),1004 if accepted else -1,int(bool(delayed or accepted and allocate)))
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],damageCases=4080,routeCases=192,
        completeRoutines={name:hashlib.sha256(function(text,name).encode()).hexdigest() for text,name in [(ct,'MISSILE_CalculateDamageData'),(ct,'MISSILE_SetDamageStats'),(st,'SKILLS_GetMinElemDamage'),(st,'SKILLS_GetMaxElemDamage'),(mt,'MISSMODE_RollDamageValue'),(mt,'MISSMODE_SrvDmgHitHandler'),(mt,'MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster')]},
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [common,skills,mode,damageParent,routeParent,refs/'skills.json',refs/'missiles.json']},artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=['Complete creator/getters/writer/roll use current skill67/missile114,15percent hard synergies and HitShift7. All255ranks/four synergy totals exclude supplied weapon/item/origin/critical payloads.',
        'Complete finder rejects historical JUSTHIT and LastCollide before selection. Complete router starts four-frame historical delay before damage and removes accepted impact missiles. Suppressed targets do not consume missiles. Failed state allocation still delivers this hit.'],
        remaining=['JUSTHIT supplied here is historical global state,not modern delay identity. Retain and identify the existing per-cast160ms adapter until current caster/cast/skill boundaries are established. Official2.7 only establishes same-skill isolation: https://news.blizzard.com/en-gb/article/23938388/diablo-ii-resurrected-ladder-season-4-has-concluded',
        'Shared fan/factory/vector/do1/steps have separate bounded evidence. Native geometry,candidate ordering,global frame phase,FCR,events,retired-owner/client equivalence need native acceptance; no full modern executable proof is claimed.'])
    path=ROOT/'tools/reference/teeth-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Teeth:4080complete damage/roll and192finder/router contexts pass;modern delay identity remains explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
