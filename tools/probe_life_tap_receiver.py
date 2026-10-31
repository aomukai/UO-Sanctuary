#!/usr/bin/env python3
"""Exercise the complete shared curse factory with Life Tap's empty stat slots."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/life-tap-receiver-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    parent=ROOT/'tools/reference/amplify-damage-source-evidence.json';proof=json.loads(parent.read_text())
    base=ROOT/'.local/class-skills/amplify-damage-source/probe.cpp';assert sha(base)==proof['artifacts'][str(base.relative_to(ROOT))]
    table=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';row=json.loads(table.read_text())['82']
    assert row['srvdofunc']==30 and row['aurafilter']==3 and row['mana']==9
    assert not any(row.get('aurastat'+str(i)) for i in range(1,7))
    assert [row['Param'+str(i)] for i in range(1,7)]==[4,1,400,60,50,0]
    states=ROOT/'tools/reference/d2r-named-equipment/json/states.json';state=json.loads(states.read_text())['lifetap'];assert state['*ID']==58
    cpp=base.read_text().split('int main(){')[0]+r'''
int main(){D2UnitStrc owner,target;owner.dwUnitType=0;owner.dwUnitId=7;owner.pvp=true;owner.room=target.room=&room;target.dwFlags=7;bool first=true;printf("[");
for(int oldRank:{0,1,20,255})for(int rank:{1,2,3,20,21,255})for(int other:{0,1})for(int resistance:{0,50,99,100})for(int attract:{0,1}){
 D2StatListStrc old;old.skill=other?72:82;old.state=other?19:58;old.rank=oldRank;old.expire=777;old.creator=999;
 target.dwUnitType=1;target.stats[8]=resistance;target.attract=attract;target.list=oldRank?&old:nullptr;removals=events=updates=0;
 D2UnkNecSkillStrc arg;arg.pGame=&game;arg.pUnit=&owner;arg.nSkillId=82;arg.nAuraTargetState=58;arg.nSkillLevel=rank;arg.nDuration=400+60*(rank-1);
 for(int i=0;i<6;i++){arg.nAuraStat[i]=-1;arg.nAuraStatCalc[i]=0;}
 int result=sub_6FD0B450(&target,&arg);auto*p=target.list;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",oldRank,rank,other,resistance,attract,result,p?p->rank:0,p?p->creator:0,p?p->expire:0,removals,events,p?int(p->stats.size()):0);first=false;
}printf("]\n");}
'''
    STAGE.mkdir(parents=True,exist_ok=True);src=STAGE/'probe.cpp';src.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(src),'-o',str(STAGE/'probe')],capture_output=True);(STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    data=json.loads(run.stdout)
    for old,rank,other,resist,attract,result,stored,creator,expire,removed,events,stats in data:
        accept=resist<100 and not attract and (not old or other or rank>=old);equal=old and not other and rank==old
        duration=400+60*(rank-1);duration-=duration*resist//100
        expected=(rank,999 if equal else 7,100+duration) if accept else (old,999,777) if old else (0,0,0)
        assert (result,stored,creator,expire,removed,events,stats)==(int(accept),*expected,int(accept and old>0 and not equal),int(accept),0)
    assert len(data)==384
    cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=proof['legacyCommit'],receiverContexts=384,
                  reusedReference=dict(path=str(parent.relative_to(ROOT)),sha256=sha(parent)),reusedReceiverCpp=dict(path=str(base.relative_to(ROOT)),sha256=sha(base)),
                  pinnedTable=dict(path=str(table.relative_to(ROOT)),sha256=sha(table)),pinnedState=dict(path=str(states.relative_to(ROOT)),sha256=sha(states),id=58),
                  artifacts={str(p.relative_to(ROOT)):sha(p) for p in [src,cases,STAGE/'compiler.log',STAGE/'sanitizer.log']},
                  established=['Complete unchanged curse receiver/factory accepts an empty aura-stat array and creates rank/creator/state/expiry metadata without a fabricated resistance or damage stat.','Weaker rank rejects;equal retains creator while refreshing expiry;higher/different replaces. Attract/full resistance reject.'],
                  remaining=['Owner/hostility/statlist/allocation/clock/event services remain declared. Do30/formula VM/radial traversal and actual damage-event registration/current executable are not executed.'])
    dest=ROOT/'tools/reference/life-tap-receiver-source-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Life Tap:384 complete no-stat receiver/factory contexts passed ASan/UBSan.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
