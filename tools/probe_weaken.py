#!/usr/bin/env python3
"""Reuse complete pinned curse and ordinary NPC damage services for Weaken."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/weaken-source'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    parent=ROOT/'tools/reference/amplify-damage-source-evidence.json'
    proof=json.loads(parent.read_text());base=ROOT/'.local/class-skills/amplify-damage-source/probe.cpp'
    assert sha(base)==proof['artifacts'][str(base.relative_to(ROOT))]
    tables=ROOT/'tools/reference/d2r-named-equipment/json'
    skills=json.loads((tables/'skills.json').read_text());row=skills['72']
    state=json.loads((tables/'states.json').read_text())['weaken']['*ID']
    assert (row['skill'],row['srvdofunc'],row['aurafilter'],row['aurastat1'],row['aurastatcalc1'],state)==('Weaken',30,3,'damagepercent','ln56',19)
    assert [row['Param'+str(i)] for i in range(1,7)]==[9,1,350,60,-33,-1]
    cpp=base.read_text().split('int main(){')[0]+r'''
int main(){D2UnitStrc owner,target;owner.dwUnitType=0;owner.dwUnitId=7;owner.pvp=true;owner.room=target.room=&room;target.dwFlags=7;bool first=true;std::printf("{\"receiver\":[");
for(int type:{0,1})for(int hire:{0,1})for(int basis:{-100,0,99,100,119,120,200})for(int oldRank:{0,1,20,255})for(int rank:{1,3,20,21,255})for(int other:{0,1})for(int resistance:{0,50,99,100})for(int attract:{0,1}){
 D2StatListStrc old;old.skill=other?66:72;old.state=other?9:19;old.rank=oldRank;old.expire=777;old.creator=999;old.stats[16]=other?0:-32-oldRank;
 target.dwUnitType=type;target.hireling=hire;target.baseResistance=basis;target.stats[8]=resistance;target.attract=attract;target.list=oldRank?&old:nullptr;removals=events=0;
 D2UnkNecSkillStrc arg;arg.pGame=&game;arg.pUnit=&owner;arg.nSkillId=72;arg.nAuraTargetState=19;arg.nAuraStat[0]=16;arg.nAuraStatCalc[0]=-32-rank;arg.nSkillLevel=rank;arg.nDuration=350+60*(rank-1);
 int result=sub_6FD0B450(&target,&arg);auto* p=target.list;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",type,hire,basis,oldRank,rank,other,resistance,attract,result,p?p->rank:0,p?p->creator:0,p?p->expire:0,p?p->stats[16]:0,removals,events);
}std::printf("]}\n");}
'''
    STAGE.mkdir(parents=True,exist_ok=True)
    def execute(name,code):
        p=STAGE/(name+'.cpp');p.write_text(code);binary=STAGE/name
        with (STAGE/(name+'-compiler.log')).open('w') as log:
            subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(binary)],check=True,stdout=log,stderr=log)
        with (STAGE/(name+'-sanitizer.log')).open('w') as log:
            return json.loads(subprocess.check_output([str(binary)],stderr=log))
    receiver=execute('receiver',cpp)['receiver']
    for typ,hire,basis,old,rank,other,resistance,attract,result,stored,owner,expiry,value,removed,events in receiver:
        accept=resistance<100 and not attract and (not old or other or rank>=old)
        equal=old and not other and rank==old
        if accept:
            duration=350+60*(rank-1);duration-=duration*resistance//100
            expected=(rank,999 if equal else 7,100+duration,-32-rank)
        else:expected=(old,999,777,0 if other else -32-old) if old else (0,0,0,0)
        assert (result,stored,owner,expiry,value,removed,events)==(int(accept),*expected,int(accept and old>0 and not equal),int(accept))
    assert len(receiver)==8960
    damage_proof_path=ROOT/'tools/reference/npc-equipment-damage-source-evidence.json'
    damage_proof=json.loads(damage_proof_path.read_text())
    key='.local/class-skills/npc-equipment-damage-source/source.cpp'
    damage_base=ROOT/key
    if not damage_base.exists():damage_base=Path('/media/aomukai/FILES/Sanctuary')/key
    assert sha(damage_base)==damage_proof['artifacts'][key]
    damage_cpp=damage_base.read_text().split('int main(){')[0]+r'''
int main(){Init();printf("[");bool first=true;
for(int rank:{1,20,21,90,255})for(int aura:{0,100,200})for(int strength:{0,12})for(int weaponMode:{0,1})for(int source:{64,128})for(int choice:{0,1}){
 new(&root)D2StatListStrc;new(&item)D2StatListStrc;new(&bonus)D2StatListStrc;
 actor={1,1,0,&root,nullptr};weapon={2,4,0,&item,nullptr};root.pOwner=&actor;root.dwOwnerType=1;
 for(auto* s:{&root.Stats,&root.FullStats}){Raw(*s,21,50);Raw(*s,22,70);Raw(*s,25,aura-32-rank);Raw(*s,0,strength);}
 armed=weaponMode;wieldType=1;strengthCoefficient=100;dexterityCoefficient=0;edge=choice;lastBound=0;
 int result=SUNITDMG_ApplyDamageBonuses(&actor,1,nullptr,0,0,0,0,source);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,aura,strength,weaponMode,source,choice,result,lastBound);first=false;
}printf("]\n");}
'''
    damage=execute('damage',damage_cpp);assert len(damage)==240
    for rank,aura,strength,armed,source,edge,result,bound in damage:
        percent=max(-90,aura-32-rank+strength)
        low=50*256+(50*256*percent//100 if percent>=0 else -((-50*256*percent)//100))
        high=70*256+(70*256*percent//100 if percent>=0 else -((-70*256*percent)//100))
        roll=low if not edge else high-1
        assert result==roll*source//128 and bound==high-low
    result=STAGE/'cases.json';result.write_text(json.dumps(dict(receiver=receiver,damage=damage),separators=(',',':'))+'\n')
    artifacts=[result,*STAGE.glob('*.cpp'),*STAGE.glob('*-compiler.log'),*STAGE.glob('*-sanitizer.log')]
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=proof['legacyCommit'],receiverContexts=8960,damageContexts=240,
                  reusedReferences={str(p.relative_to(ROOT)):sha(p) for p in [parent,damage_proof_path]},
                  reusedReceiverCpp=dict(path=str(base.relative_to(ROOT)),sha256=sha(base)),
                  reusedDamageCpp=dict(path=key,sha256=sha(damage_base)),
                  pinnedTables={name:sha(tables/(name+'.json')) for name in ['skills','states']},
                  artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(artifacts)},
                  established=[
                      'Complete unchanged shared curse callback/eligibility/factory/alignment/percentage routines execute with current Weaken stat16,state19,skill72 inputs. Same-skill lower-rank rejection,equal-rank creator/stat retention and expiry refresh,higher/different replacement,curse resistance and Attract match.',
                      'Physical immunity/hireling/player distinctions do not divide damagepercent. Current Weaken is -33 minus1per additional rank,radius9plus1per rank,duration350plus60frames per rank,cost4.',
                      'Complete ordinary NPC stat-list/damage routines compose Weaken with a supplied positive aura and Strength before the -90floor. Armed/unarmed endpoints,exclusive random maximum and sourceDamage64/128 are tested.'],
                  limits=[
                      'Reuses hash-validated accepted source harnesses without changing their artifacts. Stat/state allocation,alignment/owner and event services remain declared;no current executable,formula VM,do30/radial or full mode/event scheduler is executed.',
                      'The weapon consumer probe supplies raw aggregate endpoints/Strength/aura and controlled endpoint draws. Full native actor/AI/item/global-frame/geometry/PvP and client behavior require separate acceptance. Native eligibility reuses the prior1024context receiver proof;it is not recounted here.'])
    dest=ROOT/'tools/reference/weaken-source-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Weaken:8960complete receiver/factory and240complete NPC stat/damage contexts passed.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
