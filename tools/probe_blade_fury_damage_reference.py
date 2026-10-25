#!/usr/bin/env python3
"""Reuse the complete blade missile creator for Fury's current skill endpoints."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess
import probe_blade_sentinel_damage_reference as basis

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    basis.build(check=True)
    old=json.loads((ROOT/'tools/reference/blade-sentinel-damage-engine-evidence.json').read_text())
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    assert tree['sha']==old['legacyCommit']
    ref=ROOT/'tools/reference/d2r-named-equipment'
    manifest=json.loads((ref/'sources.json').read_text())
    tables={}
    for name in ['skills','missiles']:
        raw=(ref/f'json/{name}.json').read_bytes()
        assert hashlib.sha256(raw).hexdigest()==manifest['files'][f'json/{name}.json']['sha256']
        tables[name]=json.loads(raw)
    skill=tables['skills']['266']
    missile=next(v for v in tables['missiles'].values() if v.get('Missile')=='bladefragment1')
    assert skill['SrcDam']==96 and skill['HitShift']==8 and missile['Half2HSrc']==1 and missile['Skill']=='Blade Fury'
    assert missile['*ID']==506 and not missile.get('pSrvHitFunc') and not missile.get('pSrvDmgFunc')
    assert skill['DmgSymPerCalc']=="(skill('Blade Sentinel'.blvl)+skill('Blade Shield'.blvl))*par8" and skill['Param8']==10
    assert [skill['MinDam'],skill['MaxDam']]==[8,10]
    assert [skill[f'MinLevDam{i}'] for i in range(1,6)]==[3,5,8,8,8]
    assert [skill[f'MaxLevDam{i}'] for i in range(1,6)]==[3,5,8,8,8]
    cpp=(STAGE/'blade-sentinel-damage-source-probe.cpp').read_text().split('int main()',1)[0]

    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old
        cpp=cpp.replace(old,new)

    replace('dwMinDam=6,dwMaxDam=10,dwMinLvlDam[5]={3,4,5,5,5},dwMaxLvlDam[5]={3,4,5,5,5}',
            'dwMinDam=8,dwMaxDam=10,dwMinLvlDam[5]={3,5,8,8,8},dwMaxLvlDam[5]={3,5,8,8,8}')
    replace('expression==1 && id==257 && rank>0','expression==1 && id==266 && rank>0')
    fields=json.loads((STAGE/'blade-sentinel-damage-source-probe.json').read_text())['fields']
    supplied=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text())['sourceInputs']
    supplied=supplied|dict(STAT_STRENGTH=80,STAT_SECONDARY_MINDAMAGE=25,STAT_SECONDARY_MAXDAMAGE=35,
                          STAT_ITEM_THROW_MINDAMAGE=40,STAT_ITEM_THROW_MAXDAMAGE=60)
    # Confirm all inherited executable bodies remain the exact pinned source.
    for name,expected in old['compiledUnchangedRoutines'].items():
        match=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',cpp,re.M)
        assert match,name
        end=cpp.index('{',match.start())+1
        depth=1
        while depth:
            depth+=(cpp[end]=='{')-(cpp[end]=='}')
            end+=1
        assert hashlib.sha256(cpp[match.start():end].encode()).hexdigest()==expected,name
    cpp+='''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank:{1,3,8,9,16,17,20,21,22,23,28,255})for(int h:{0,1,20,40})for(int g:{0,1,2,3,4})for(int pct:{-200,37})for(int crit:{0,1}){
owner={};weapon={};missile={};row={};skill={};owner.dwUnitType=UNIT_PLAYER;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=506;row.nSkill=266;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_HALF2HSRC];hard=h;gear=g;critical=crit;
owner.stats[STAT_MINDAMAGE]=9;owner.stats[STAT_MAXDAMAGE]=14;owner.stats[STAT_DAMAGEPERCENT]=pct;
'''
    for name,value in supplied.items():
        cpp+=f'owner.stats[{name}]={value};\n'
    cpp+='D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,rank);printf("%s[%d,%d,%d,%d,%d,[",first?"":",",rank,h,g,pct,crit);first=false;\n'
    for i,field in enumerate(fields):
        cpp+=f'printf("{"," if i else ""}%d",d.{field});\n'
    cpp+='printf("]]");}printf("]\\n");}\n'
    path=STAGE/'blade-fury-damage-source-probe.cpp'
    path.write_text(cpp)
    binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True)
    (STAGE/'blade-fury-damage-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    rows=json.loads(run.stdout)
    assert len(rows)==960

    def tier(base,rank):
        return base+sum(3 if n<=8 else 5 if n<=16 else 8 for n in range(2,rank+1))

    for rank,hard,gear,percent,critical,values in rows:
        data=dict(zip(fields,values))
        factor=48 if gear==1 else 96
        low,high=(40,60) if gear==3 else (25,35) if gear==4 else (9,14)
        expected=dict(nMinDamage=(tier(8,rank)+tier(8,rank)*hard*10//100)*256+low*256*factor//128,
                      nMaxDamage=(tier(10,rank)+tier(10,rank)*hard*10//100)*256+high*256*factor//128,
                      nDamagePercent=max(-90,percent+15+(0 if gear==2 else 118)),nDemonDamagePercent=11,
                      nUndeadDamagePercent=13,nFlags=1+critical*2,nColdLength=15*factor//128,
                      nPoisonLength=101,nPoisonCount=2*factor//128,nLifeDrainMinDamage=7*factor//128,
                      nManaDrainMinDamage=5*factor//128)
        for label,stat in [('Fire','FIRE'),('Light','LIGHT'),('Magic','MAGIC'),('Cold','COLD')]:
            for end in ['Min','Max']:
                expected['n'+label+end+'Damage']=supplied[f'STAT_{stat}{end.upper()}DAM']*256*factor//128
        for end in ['Min','Max']:
            expected['nPoison'+end+'Damage']=supplied[f'STAT_POISON{end.upper()}DAM']*factor//128
        assert all(data[k]==v for k,v in expected.items()),(rank,hard,gear,percent,critical,data,expected)
        assert all(v==0 for k,v in data.items() if k not in expected)
    out=STAGE/'blade-fury-damage-source-probe.json'
    out.write_text(json.dumps(dict(fields=fields,cases=rows),separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',talentId='assassin.bladefury',nativeVerifiedByThisSourceProbe=False,
                  legacyCommit=tree['sha'],sourceHashes=old['sourceHashes'],pinnedHashes=old['pinnedHashes'],
                  compiledUnchangedRoutines=old['compiledUnchangedRoutines'],sharedAbiEvidence='tools/reference/blade-sentinel-damage-engine-evidence.json',
                  fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=960),
                  established=[
                      'Complete unchanged missile creator/add/scale/tier/physical getters reuse Blade Sentinel source contracts with pinned Fury skill266 and missile506. Base8/10, tier3/5/8/8/8 and hard Sentinel+Shield total*10 are checked independently through rank255.',
                      'Skill physical is added after SrcDam96 (48 for two-handed) weapon endpoints; it is not scaled a second time. Five explicit selected-item/unarmed/throwing/secondary endpoint contexts, positive/negative percent and critical controls match.',
                      'Player creation retains separate generic/mastery/stat percent, demon/undead, typed/poison/count/length and drain lanes. This is a player snapshot, without Sentinel NPC ownership reassignment.'
                  ],limits=[
                      'Formula hard-total, raw owner/weapon stats, weapon metadata and critical selection are explicit services. Actual live selected-hand collector, mastery/poison/Venom, hit/AR/events/mitigation, durability and modern executable acceptance remain native integration work.',
                      'Source packet creation does not prove current attack-rating injection: Fury gained its documented10percent per rank after this engine version. Preserve current pinned ToHitCalc and official2.4 notes; do not silently inherit the old missing bonus.',
                      'No projectile motion, collision, ownership/save or held input is implemented by this probe; no learned handler enabled.'
                  ],sanitizers='ASan/UBSan; exit0 and empty runtime stderr')
    target=ROOT/'tools/reference/blade-fury-damage-source-evidence.json'
    rendered=json.dumps(evidence,indent=2)+'\n'
    if check:
        assert target.read_text()==rendered
    else:
        target.write_text(rendered)
    print('960 complete Blade Fury player missile creator/physical/typed controls pass; native collector/AR/flight/input remain pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--check',action='store_true')
    build(parser.parse_args().check)
