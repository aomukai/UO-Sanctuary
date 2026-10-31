#!/usr/bin/env python3
"""Execute complete shared curse receiver/factory with all four Decrepify stats."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/decrepify-source'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    parent=ROOT/'tools/reference/amplify-damage-source-evidence.json';proof=json.loads(parent.read_text())
    base=ROOT/'.local/class-skills/amplify-damage-source/probe.cpp';assert sha(base)==proof['artifacts'][str(base.relative_to(ROOT))]
    tables=ROOT/'tools/reference/d2r-named-equipment/json';row=json.loads((tables/'skills.json').read_text())['87']
    state=json.loads((tables/'states.json').read_text())['decrepify'];assert state['*ID']==60
    assert (row['srvdofunc'],row['aurafilter'],row['aurarangecalc'],row['auralencalc'],row['mana'])==(30,3,'ln12','ln34',11)
    assert [row['aurastat'+str(i)] for i in range(1,5)]==['velocitypercent','damagepercent','damageresist','attackrate']
    assert all(row['aurastatcalc'+str(i)]=='par5' for i in range(1,5))
    assert [row['Param'+str(i)] for i in range(1,6)]==[6,0,100,15,-50]
    cpp=base.read_text().split('int main(){')[0]+r'''
constexpr int STAT_VELOCITYPERCENT=26;
int main(){D2UnitStrc owner,target;owner.dwUnitType=0;owner.dwUnitId=7;owner.pvp=true;owner.room=target.room=&room;target.dwFlags=7;bool first=true;std::printf("[");
for(int type:{0,1})for(int hire:{0,1})for(int basis:{-100,0,99,100,109,110,200})for(int oldRank:{0,1,20,255})for(int rank:{1,3,20,21,255})for(int other:{0,1})for(int resistance:{0,50,99,100})for(int attract:{0,1}){
 D2StatListStrc old;old.skill=other?72:87;old.state=other?19:60;old.rank=oldRank;old.expire=777;old.creator=999;
 old.stats[26]=other?0:-50;old.stats[16]=other?-32-oldRank:-50;old.stats[20]=other?0:basis>=100?-50:-10;old.stats[9]=other?0:-50;
 target.dwUnitType=type;target.hireling=hire;target.baseResistance=basis;target.stats[8]=resistance;target.attract=attract;target.list=oldRank?&old:nullptr;removals=events=updates=0;
 D2UnkNecSkillStrc arg;arg.pGame=&game;arg.pUnit=&owner;arg.nSkillId=87;arg.nAuraTargetState=60;
 arg.nAuraStat[0]=26;arg.nAuraStat[1]=16;arg.nAuraStat[2]=20;arg.nAuraStat[3]=9;arg.nAuraStat[4]=arg.nAuraStat[5]=-1;
 for(int i=0;i<4;i++)arg.nAuraStatCalc[i]=-50;
 arg.nSkillLevel=rank;arg.nDuration=100+15*(rank-1);arg.bUpdateAnimRate=1;
 int result=sub_6FD0B450(&target,&arg);auto* p=target.list;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",type,hire,basis,oldRank,rank,other,resistance,attract,result,p?p->rank:0,p?p->creator:0,p?p->expire:0,p?p->stats[26]:0,p?p->stats[16]:0,p?p->stats[20]:0,p?p->stats[9]:0,removals,events,updates);
}std::printf("]\n");}
'''
    STAGE.mkdir(parents=True,exist_ok=True);p=STAGE/'probe.cpp';p.write_text(cpp)
    with (STAGE/'compiler.log').open('w') as log:
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(STAGE/'probe')],check=True,stdout=log,stderr=log)
    with (STAGE/'sanitizer.log').open('w') as log:
        data=json.loads(subprocess.check_output([str(STAGE/'probe')],stderr=log))
    for typ,hire,basis,old,rank,other,resistance,attract,result,stored,owner,expiry,velocity,damage,physical,attack,removed,events,updates in data:
        accept=resistance<100 and not attract and (not old or other or rank>=old)
        equal=old and not other and rank==old
        if accept:
            duration=100+15*(rank-1);duration-=duration*resistance//100
            expected=(rank,999 if equal else 7,100+duration,-50,-50,-10 if typ==1 and not hire and basis>=100 else -50,-50)
        elif old:expected=(old,999,777,0,-32-old,0,0) if other else (old,999,777,-50,-50,-50 if basis>=100 else -10,-50)
        else:expected=(0,0,0,0,0,0,0)
        assert (result,stored,owner,expiry,velocity,damage,physical,attack,removed,events,updates)==(int(accept),*expected,int(accept and old>0 and not equal),int(accept),int(accept))
    assert len(data)==8960
    cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=proof['legacyCommit'],receiverContexts=len(data),
                  reusedReference=dict(path=str(parent.relative_to(ROOT)),sha256=sha(parent)),
                  reusedReceiverCpp=dict(path=str(base.relative_to(ROOT)),sha256=sha(base)),
                  pinnedTables={name:sha(tables/(name+'.json')) for name in ['skills','states']},
                  pinnedState=dict(id=state['*ID'],sunderfull=state.get('sunderfull')),
                  artifacts={str(q.relative_to(ROOT)):sha(q) for q in [p,cases,STAGE/'compiler.log',STAGE/'sanitizer.log']},
                  established=[
                      'Complete unchanged callback/factory/eligibility/alignment/percentage routines execute all four current Decrepify stat slots together:velocitypercent,damagepercent,damageresist,attackrate,each-50.',
                      'Physical resistance alone becomes-10for an immune nonplayer/nonhireling. The other three stats stay-50. Weaker same-skill rank rejects;equal rank keeps creator/primary state and refreshes expiry but rewrites secondary physical resistance using the new supplied base resistance. Higher/different curse replaces.',
                      'Successful callbacks request animation/velocity update,including equal-rank refresh. Attract/curse-resistance and failed-allocation rank guards leave old state and skip those updates. Current radius6,duration100+15*(rank-1)frames,cost11 are pinned.'],
                  limits=[
                      'Reuse is hash-validated and does not modify accepted harness artifacts. Stat/state/owner/hostility,allocation,clock/events and the animation-update call are declared services. Full do30/formula VM/radial/current executable/movement engine are not executed.',
                      'Current state60also declares sunderfull=1. This static flag does not establish modern Sunder arithmetic;physical Sunder remains outside supported native contexts. Full dynamic stat/slow/haste/cap/AI/PvP/client behavior needs separate acceptance.'])
    dest=ROOT/'tools/reference/decrepify-source-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Decrepify:8960complete four-stat receiver/factory contexts passed ASan/UBSan.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
