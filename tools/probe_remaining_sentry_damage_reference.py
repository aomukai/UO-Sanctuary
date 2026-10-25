#!/usr/bin/env python3
"""Reuse complete elemental getters/creator for the remaining sentry missiles."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable,str(ROOT/'tools/probe_sentry_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    parent=json.loads((ROOT/'tools/reference/sentry-damage-source-evidence.json').read_text())
    creator=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text())
    original=(STAGE/'sentry-damage-source-probe.cpp').read_text().split('int main()',1)[0]
    routines=parent['blockHashes']|creator['blockHashes'];fields=json.loads((STAGE/'bow-launch-source-probe.json').read_text())['fields']
    tables,pins={},{}
    for name in ['skills','missiles']:
        path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';raw=path.read_bytes();pins[name]=hashlib.sha256(raw).hexdigest()
        manifest=json.loads((path.parent.parent/'sources.json').read_text());assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(raw)
    for stem,player,actor,mid,missile,element,synergy,two in [
        ('lightning-sentry',271,313,526,'sentrylightningbolt','ltng',18,True),
        ('wake-of-inferno',272,311,523,'inferno sentry 1','fire',18,True),
        ('death-sentry-lightning',276,324,641,'sentrylightningbolt2','ltng',12,False)]:
        skill=tables['skills'][str(player)];row=tables['missiles'][missile]
        assert row['Skill']==skill['skill'] and not any(row.get(k) for k in ['MissileSkill','SrcDamage','SrcMissDmg']) and not skill.get('SrcDam')
        base=re.sub(r'\b261\b',str(player),original);base=re.sub(r'\b306\b',str(actor),base)
        before=f'return id=={player}?(fireHard+lightHard)*9:0;';assert before in base
        base=base.replace(before,f'return id=={player}?'+('(fireHard+lightHard)' if two else 'fireHard')+f'*{synergy}:0;',1)
        for name,digest in routines.items():
            match=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',base,re.M);assert match,name
            end,depth=base.index('{',match.start())+1,1
            while depth:depth+=(base[end]=='{')-(base[end]=='}');end+=1
            assert hashlib.sha256(base[match.start():end].encode()).hexdigest()==digest,name
        cpp=base+f'''
D2UnitStrc origin;bool first=true;
void emit(int rank,int hard1,int hard2,int mastery,int noise,int overrideSkill,int hasOrigin){{
 owner={{}};weapon={{}};missile={{}};origin={{}};skill={{}};actorSkill={{}};row={{}};missileData={{}};
 owner.dwUnitType=UNIT_MONSTER;owner.pInventory=nullptr;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId={mid};missileData.nSkill={actor};
 row.nSkill={player};row.dwMissileFlags=overrideSkill?gdwBitMasks[MISSILESFLAGINDEX_MISSILESKILL]:0;
 skill.nEType=ELEMTYPE_{'LTNG' if element=='ltng' else 'FIRE'};actorSkill.nEType=ELEMTYPE_NONE;actorSkill.dwEMin=actorSkill.dwEMax=0;
'''
        for field,key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:cpp+=f'skill.{field}={skill[key]};\n'
        for i in range(5):cpp+=f"skill.dwEMinLev[{i}]={skill['EMinLev'+str(i+1)]};skill.dwEMaxLev[{i}]={skill['EMaxLev'+str(i+1)]};\n"
        cpp+=f'''
 fireHard=hard1;lightHard=hard2;critCalls=0;critical=true;for(int& stat:owner.stats)stat=noise?37:0;for(int& stat:origin.stats)stat=777;for(int& stat:weapon.stats)stat=999;
 owner.stats[STAT_PASSIVE_{'LTNG' if element=='ltng' else 'FIRE'}_MASTERY]=mastery;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,hasOrigin?&origin:nullptr,&missile,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,[",first?"":",",rank,hard1,hard2,mastery,noise,overrideSkill,hasOrigin);first=false;
'''
        for i,field in enumerate(fields):cpp+=f'printf("{"," if i else ""}%d",d.{field});\n'
        cpp+='''printf("],%d]",critCalls);}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
for(int rank:{1,3,8,9,16,17,20,22,23,28,40,255})for(int hard1:{0,1,20})for(int hard2:{0,4,20})for(int mastery:{-50,0,25,100})for(int noise:{0,1})for(int overrideSkill:{0,1})for(int origin:{0,1})emit(rank,hard1,hard2,mastery,noise,overrideSkill,origin);
for(int rank=1;rank<=255;rank++)emit(rank,20,20,25,0,0,1);printf("]\\n");}
'''
        source=STAGE/f'{stem}-damage-source-probe.cpp';source.write_text(cpp);binary=source.with_suffix('')
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(binary)],check=True)
        run=subprocess.run([str(binary)],capture_output=True);(STAGE/f'{stem}-damage-sanitizer.log').write_bytes(run.stderr)
        assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
        cases=json.loads(run.stdout);assert len(cases)==3711
        def trunc(value):return abs(value)//100*(1 if value>=0 else -1)
        for rank,hard1,hard2,mastery,noise,override,origin,values,critical in cases:
            expected=[]
            for lane in ['EMin','EMax']:
                amount,left=skill[lane],rank-1
                for i,size in enumerate([7,8,6,6,2**30],1):
                    count=min(left,size);amount+=count*skill[f'{lane}Lev{i}'];left-=count
                amount<<=skill['HitShift']
                if lane=='EMax' or amount>256 or skill['EMinLev1']:
                    amount+=trunc(amount*(hard1+(hard2 if two else 0))*synergy)
                amount+=trunc(amount*mastery);expected.append(0 if override else amount)
            actual=dict(zip(fields,values));prefix='Light' if element=='ltng' else 'Fire';allowed=[f'n{prefix}MinDamage',f'n{prefix}MaxDamage']
            assert [actual[k] for k in allowed]==expected,(stem,rank,hard1,hard2,mastery)
            assert critical==0 and all(value==0 for key,value in actual.items() if key not in allowed)
        fixture=STAGE/f'{stem}-damage-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,cases=cases),separators=(',',':'))+'\n')
        e=dict(referenceVersion='3.3.93847',talentId={271:'assassin.lightningsentry',272:'assassin.wakeofinferno',276:'assassin.deathsentry'}[player],handlerEnabled=False,
            legacyCommit=parent['legacyCommit'],sourceHashes=parent['sourceHashes'],compiledUnchangedRoutines=routines,pinnedInputHashes=pins,
            fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),completeDamageCases=len(cases),allRankGoldens=255),
            established=[
                f'Complete source-zero creator/getters choose player skill{player} from missile{mid} despite stored actor skill{actor}; explicit MissileSkill override selects the damage-free NPC row in these controls.',
                'Current five-tier endpoints, HitShift, hard-only synergy and inherited elemental mastery execute through complete unchanged source routines. The lightning minimum1/FP256 bypasses synergy while mastery still applies; Inferno minimum starts320/FP256 and receives synergy.',
                'Weapon/item physical/typed/poison/drain/critical and a noisy origin do not enter these source-zero packets. Captured creator fields contain only the intended elemental endpoints; no critical draw is made.',
                'All255 effective ranks plus tier boundaries, negative/positive mastery, independent hard inputs, owner noise, origin presence and override controls are covered.'],
            variant=dict(playerSkill=player,actorSkill=actor,missile=mid,element=element,synergyPercent=synergy,synergyInputs=2 if two else 1),
            services=['Owner stats, formula/hard-rank lookup, current table access, ratio arithmetic and physical-zero getters are declared services from the existing sentry harness.',
                      'This is the complete getter/creator composition only. Placement/AI/sequence/beam density, missile factory/motion/collision, DamageRate reduction scaling, defenses, corpse explosion, retirement/GUID lifetime, native combat/input and modern executable equivalence remain separate.'],
            sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
        out=ROOT/'tools/reference'/f'{stem}-damage-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
        if check:assert out.read_text()==encoded,stem+' damage evidence stale'
        else:out.write_text(encoded)
        print(f'{stem}:3711 complete source damage contexts,255rank goldens; sanitizers clean; native producer/collision/lifecycle remain separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
