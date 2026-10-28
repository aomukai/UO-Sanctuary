#!/usr/bin/env python3
"""Execute full legacy elemental suppliers/creator/writer with current Miasma rows."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'miasma-packets'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={};routines={};directory=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((directory/'sources.json').read_text());tables={}
    for name in ['skills','missiles']:
        p=directory/f'json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
    local_path=ROOT/'tools/reference/d2r-local-missile-data-evidence.json';local=json.loads(local_path.read_text())
    assert local['referenceVersion']=='3.3.93847' and local['comparison']['unexplainedDifferences']==0
    assert local['cached']['sha256']==inputs[str((directory/'json/missiles.json').relative_to(ROOT))]
    owned=ROOT/local['local']['path'];assert sha(owned)==local['local']['sha256']
    inputs[str(local_path.relative_to(ROOT))]=sha(local_path);inputs[str(owned.relative_to(ROOT))]=sha(owned)
    skill_proof=ROOT/'tools/reference/warlock-foundation-preflight.json';assert json.loads(skill_proof.read_text())['comparedPlayerFields']==1709
    inputs[str(skill_proof.relative_to(ROOT))]=sha(skill_proof)
    s=tables['skills']['395'];m=tables['missiles']['miasmaboltcloud'];assert s['EType']==m['EType']=='mag' and m['HitShift']==8 and m['Explosion']==1
    abi=STAGE/'damage-abi-base.cpp';STAGE.mkdir(parents=True,exist_ok=True)
    if not abi.exists():abi.write_bytes((BASE/'arctic-stream/damage-abi-base.cpp').read_bytes())
    assert sha(abi)=='7bf979729851ec1cad0616be87689e7bc0890eba21ab1565d760dcfdf62495e1'
    cpp=abi.read_text().split('void setup(',1)[0]
    for rel,names in [('D2Common/src/D2Skills.cpp',['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage']),
        ('D2Common/src/Units/Missile.cpp',['MISSILE_CalculateDamageData','MISSILE_AddStatsToDamage','MISSILE_CalculateFinalDamage','MISSILE_SetDamageStats','MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage'])]:
        p=BASE/'d2moo-pinned-source/source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']=='source/'+rel)
        inputs[str(p.relative_to(ROOT))]=sha(p)
        for name in names:
            body=function(raw.decode(),name);assert body in cpp,name;routines[name]=hashlib.sha256(body.encode()).hexdigest()
    old='return formula==2?volcano*12:hard*8;';assert cpp.count(old)==1;cpp=cpp.replace(old,'return formula==2?0:(hard+volcano)*10;')
    old='int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return hard*8;}';assert cpp.count(old)==1;cpp=cpp.replace(old,old.replace('hard*8','(hard+volcano)*20'))
    cpp+='void setup(int cloud){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_MAGIC;skill.dwELen=0;for(int& v:skill.dwELevLen)v=0;'
    for dest,key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:cpp+=f'skill.{dest}={s[key]};'
    for i in range(5):
        for dest,key in [('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:cpp+=f'skill.{dest}[{i}]={s[key+str(i+1)]};'
    cpp+='row.nSkill=cloud?0:395;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=cloud?ELEMTYPE_MAGIC:ELEMTYPE_NONE;row.dwElemDmgSymPerCalc=cloud?1:-1;'
    for dest,key in [('dwElemMin','EMin'),('dwElemMax','EMax'),('nHitShift','HitShift')]:cpp+=f'row.{dest}={m[key]};'
    for i in range(5):
        for dest,key in [('dwMinElemLev','MinELev'),('dwMaxElemLev','MaxELev')]:cpp+=f'row.{dest}[{i}]={m[key+str(i+1)]};'
    cpp+='}\n'+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int cloud:{0,1})for(int rank=1;rank<=255;rank++)for(int chains:{0,1,20})for(int abyss:{0,1,20})for(int noise:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup(cloud);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=cloud?705:697;missileData.nSkill=395;missileData.nLevel=rank;hard=chains;volcano=abyss;critical=true;critCalls=0;
 for(int& stat:owner.stats)stat=noise;D2UnitStrc origin;for(int& stat:origin.stats)stat=noise;
 D2MissileDamageDataStrc p;MISSILE_CalculateDamageData(&p,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&p,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",cloud,rank,chains,abyss,noise,p.nMagicMinDamage,p.nMagicMaxDamage,p.nMinDamage,p.nMaxDamage,p.nFireMaxDamage,p.nFlags,critCalls,missile.stats[STAT_MAGICMINDAM],missile.stats[STAT_MAGICMAXDAM]);first=false;
}printf("]\n");}'''
    path=STAGE/'miasma-damage.cpp';path.write_text(cpp);compiler=STAGE/'miasma-damage.compiler.log';sanitizer=STAGE/'miasma-damage.sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(path.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(path.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    data=json.loads(run.stdout);assert len(data)==9180
    def tier(row,maximum,rank,cloud):
        value=row['EMax' if maximum else 'EMin'];left=rank-1
        for i,n in enumerate([7,8,6,6,2**30],1):
            step=min(left,n);value+=step*row[('MaxELev' if maximum else 'MinELev')+str(i) if cloud else ('EMaxLev' if maximum else 'EMinLev')+str(i)];left-=step
        return value
    for cloud,rank,chains,abyss,noise,minimum,maximum,physmin,physmax,fire,flags,critical,storedmin,storedmax in data:
        expected=[]
        for maximumend in [False,True]:
            raw=tier(m if cloud else s,maximumend,rank,cloud);pct=(chains+abyss)*(20 if cloud else 10)
            if cloud:raw=(raw+raw*pct//100)<<8
            else:raw<<=7;raw+=raw*pct//100
            expected.append(raw)
        assert [minimum,maximum]==expected and [physmin,physmax,fire,flags,critical]==[0]*5 and (storedmin,storedmax)==(minimum,maximum)
    output=BASE/'miasma-packets-source-probe.json';output.write_bytes(run.stdout)
    artifacts=[Path(__file__).resolve(),STAGE/'damage-abi-base.cpp',path,compiler,sanitizer,output]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,cases=9180,inputs=inputs,routines=routines,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=['Full unchanged legacy skill and missile elemental getters, creator and stat writer use current Miasma rows. Both bolt and cloud carry magic only, no physical/weapon/elemental/item/critical inheritance, across255ranks,asymmetric hard synergies and noisy owner/origin stats.',
            'Bolt uses skill395/HitShift7and10percent per hard Chain/Abyss point, applying synergy after shift. Cloud705uses its own missile endpoints/HitShift8and20percent per hard point, truncating synergy before shift. Rank1Chain1cloud minimum remains3rather than3.6.',
            'Local missile parameter/formula consumers must preserve separate parameter scope: cloud Range uses missile Param4=50/Param5=5; synergy explicitly reads Miasma Bolt skill Param9=20.'],
        services=['Formula evaluators supply declared hard-rank sums with current10/20coefficients. Metadata/stat/owner lookups, ratio helper, zero monster-type layers and unused-element length services are declared. Magic mastery stays zero; no modern magic mastery callback is inferred.'],
        remaining=['New hit41/cloud-do41 behavior, maker integration/emission geometry, NextHit identity, exact first/last damaging frames, allocation and modern magic mastery/pierce timing need integration. No learned handler or current executable parity is claimed.',
            'Released official patch3.2requires no extra expiry hit or double tick; cloud delay/damage PTR nerfs were reverted. Those released changes must constrain the later world implementation.'])
    out=ROOT/'tools/reference/miasma-packets-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Miasma:9180full-source magic packets, distinct skill/missile synergy rounding, clean ASan/UBSan; learned cast remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
