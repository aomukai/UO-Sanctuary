#!/usr/bin/env python3
"""Execute complete curse receiver/factory with independently supplied elemental bases.
The retained two-stage diminishing curve is an input, not a formula-VM claim.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/lower-resist-source'
BASES=[[0,99,100,110],[100,110,0,99],[99,100,110,0],[110,0,99,100],[-100,200,120,0],[0]*4,[100]*4,[200]*4]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def amount(rank):return 25+45*min(100,110*rank//(rank+6))//100

def build(check=False):
    parent=ROOT/'tools/reference/amplify-damage-source-evidence.json';proof=json.loads(parent.read_text())
    base=ROOT/'.local/class-skills/amplify-damage-source/probe.cpp';assert sha(base)==proof['artifacts'][str(base.relative_to(ROOT))]
    tables=ROOT/'tools/reference/d2r-named-equipment/json';row=json.loads((tables/'skills.json').read_text())['91']
    state=json.loads((tables/'states.json').read_text())['lowerresist'];assert state['*ID']==61
    assert (row['srvdofunc'],row['aurafilter'],row['aurarangecalc'],row['auralencalc'],row['mana'])==(30,3,'ln12','ln34',22)
    assert [row['aurastat'+str(i)] for i in range(1,5)]==['fireresist','lightresist','coldresist','poisonresist']
    assert all(row['aurastatcalc'+str(i)]=='-dm56' for i in range(1,5))
    assert [row['Param'+str(i)] for i in range(1,7)]==[7,1,500,50,25,70]
    cpp=base.read_text().split('int main(){')[0]
    # Only declared base-stat service/scaffold changes. All complete routines stay unchanged.
    cpp=cpp.replace('int monType=0,baseResistance=0,hireling=0;','int bases[4]={},monType=0,baseResistance=0,hireling=0;')
    cpp=cpp.replace('int STATLIST_GetUnitBaseStat(D2UnitStrc* p,int,int){return p->baseResistance;}',
                    'int STATLIST_GetUnitBaseStat(D2UnitStrc* p,int s,int){return s>=22&&s<=25?p->bases[s-22]:p->baseResistance;}')
    cpp+=r'''
const int bases[8][4]={{0,99,100,110},{100,110,0,99},{99,100,110,0},{110,0,99,100},{-100,200,120,0},{0,0,0,0},{100,100,100,100},{200,200,200,200}};
int retainedAmount(int rank){return 25+45*std::min(100,110*rank/(rank+6))/100;}
int main(){D2UnitStrc owner,target;owner.dwUnitType=0;owner.dwUnitId=7;owner.pvp=true;owner.room=target.room=&room;target.dwFlags=7;bool first=true;std::printf("[");
for(int type:{0,1})for(int hire:{0,1})for(int pattern=0;pattern<8;pattern++)for(int oldRank:{0,1,20,255})for(int rank:{1,3,8,20,21,255})for(int other:{0,1})for(int resistance:{0,50,99,100})for(int attract:{0,1}){
 D2StatListStrc old;old.skill=other?72:91;old.state=other?19:61;old.rank=oldRank;old.expire=777;old.creator=999;
 for(int i=0;i<4;i++){target.bases[i]=bases[pattern][i];int full=-retainedAmount(oldRank);old.stats[22+i]=other?0:target.bases[i]>=100?full:full/5;}
 target.dwUnitType=type;target.hireling=hire;target.stats[8]=resistance;target.attract=attract;target.list=oldRank?&old:nullptr;removals=events=updates=0;
 D2UnkNecSkillStrc arg;arg.pGame=&game;arg.pUnit=&owner;arg.nSkillId=91;arg.nAuraTargetState=61;
 for(int i=0;i<4;i++){arg.nAuraStat[i]=22+i;arg.nAuraStatCalc[i]=-retainedAmount(rank);}arg.nAuraStat[4]=arg.nAuraStat[5]=-1;
 arg.nSkillLevel=rank;arg.nDuration=500+50*(rank-1);
 int result=sub_6FD0B450(&target,&arg);auto* p=target.list;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",type,hire,pattern,oldRank,rank,other,resistance,attract,result,p?p->rank:0,p?p->creator:0,p?p->expire:0,p?p->stats[22]:0,p?p->stats[23]:0,p?p->stats[24]:0,p?p->stats[25]:0,removals,events,updates);
}std::printf("]\n");}
'''
    STAGE.mkdir(parents=True,exist_ok=True);p=STAGE/'probe.cpp';p.write_text(cpp)
    with (STAGE/'compiler.log').open('w') as log:
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(STAGE/'probe')],check=True,stdout=log,stderr=log)
    with (STAGE/'sanitizer.log').open('w') as log:
        data=json.loads(subprocess.check_output([str(STAGE/'probe')],stderr=log))
    for typ,hire,pattern,old,rank,other,resistance,attract,result,stored,owner,expiry,*tail in data:
        accept=resistance<100 and not attract and (not old or other or rank>=old)
        equal=old and not other and rank==old
        old_values=[0 if other else -amount(old) if basis>=100 else -(amount(old)//5) for basis in BASES[pattern]]
        if accept:
            duration=500+50*(rank-1);duration-=duration*resistance//100
            values=[-(amount(rank)//5) if typ==1 and not hire and basis>=100 else -amount(rank) for basis in BASES[pattern]]
            if equal:values[0]=old_values[0]
            expected=(rank,999 if equal else 7,100+duration,*values)
        elif old:expected=(old,999,777,*old_values)
        else:expected=(0,0,0,0,0,0,0)
        assert (result,stored,owner,expiry,*tail)==(int(accept),*expected,int(accept and old>0 and not equal),int(accept),0)
    assert len(data)==12288
    cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    conflict=ROOT/'tools/reference/diminishing-reference-conflict.json'
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=proof['legacyCommit'],receiverContexts=len(data),bases=BASES,
                  reusedReference=dict(path=str(parent.relative_to(ROOT)),sha256=sha(parent)),
                  reusedReceiverCpp=dict(path=str(base.relative_to(ROOT)),sha256=sha(base)),
                  diminishingReference=dict(path=str(conflict.relative_to(ROOT)),sha256=sha(conflict)),
                  pinnedTables={name:sha(tables/(name+'.json')) for name in ['skills','states']},
                  pinnedState=state,
                  artifacts={str(q.relative_to(ROOT)):sha(q) for q in [p,cases,STAGE/'compiler.log',STAGE/'sanitizer.log']},
                  established=[
                      'Complete unchanged callback/factory/eligibility/alignment/percentage routines execute four Lower Resist slots together: fire first, then lightning, cold and poison. Independently supplied base resistances determine each immune nonplayer/nonhireling reduction.',
                      'Weaker same-skill ranks reject. Equal rank retains creator and primary fire value while restarting expiry and rewriting the three secondary channels. Higher/different curse replaces. Attract and full curse resistance reject without writes.',
                      'Current pinned radius7+(rank-1),duration500+50*(rank-1)frames,cost22mana,parameters25/70 and state61 are checked.'],
                  limits=[
                      'Retained two-stage diminishing values are declared formula inputs, not executed VM evidence. Legacy multiplication-first helper differs at some ranks (rank1 32 versus retained31;rank8 53 versus52). Current Lower Resist rounding remains uninstrumented. Historical Blizzard guide agrees with retained31/52 but is not current-build certification.',
                      'Accepted scaffold is hash validated; only independent base-stat service storage is extended. Stat/state/owner/hostility,allocation,clock/events remain declared services. Full do30/radial/formula VM,current executable,Sunder,items/PvP/AI/client behavior are not executed.'])
    dest=ROOT/'tools/reference/lower-resist-source-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Lower Resist:12288 complete four-element receiver/factory contexts passed ASan/UBSan.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
