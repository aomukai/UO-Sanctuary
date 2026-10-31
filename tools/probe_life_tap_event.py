#!/usr/bin/env python3
"""Execute complete Life Tap event and actual percentage helper at fractional boundaries."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'life-tap-event-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def percentage(value,scale):return scale*(value//100) if value>1048576 else value*scale//100

def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={}
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    event=function(source('source/D2Game/src/SKILLS/SkillNec.cpp'),'D2GAME_EventFunc05_6FD0EDE0')
    helper=function(source('source/D2Game/src/MONSTER/MonsterUnique.cpp'),'MONSTERUNIQUE_CalculatePercentage')
    old_script=ROOT/'tools/probe_life_tap_reference.py';prior=ROOT/'tools/reference/life-tap-engine-evidence.json';old=json.loads(prior.read_text())
    assert hashlib.sha256(event.encode()).hexdigest()==old['source']['bodySha256']
    # Read the prior declared scaffold as data; never import/execute the old
    # producer, which would overwrite its accepted artifacts.
    assign=next(n for n in ast.walk(ast.parse(old_script.read_text())) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets))
    node=assign.value
    while isinstance(node,ast.BinOp):node=node.left
    assert isinstance(node,ast.Constant) and isinstance(node.value,str)
    cpp=node.value
    stub='int MONSTERUNIQUE_CalculatePercentage(int value,int n,int d){return int(int64_t(value)*n/d);}'
    assert cpp.count(stub)==1;cpp=cpp.replace(stub,helper)
    cpp+=event+r'''
int main(){bool first=true;printf("[");
for(int physical:{0,1,255,257,12864,1048577,1048583,1280001,2000001})for(int hp:{0,1,257,100000,7999999})for(int maximum:{0,256,1280000,8000001})for(int pct:{0,1,50,100})for(int guard=0;guard<13;guard++){
 if(maximum>0&&hp>maximum)continue;
 for(int rank:{-1,0,7}){
 D2UnitStrc victim{guard==3?2:UNIT_MONSTER,1000,1000,false},healed{guard==4?2:UNIT_PLAYER,hp,maximum,guard==5};D2DamageStrc damage{physical};
 mode=guard;percent=pct;observedRank=-99;overlays=0;formulaOwner=0;skill.wAuraTargetState=guard==8?-1:guard==9?5:1;
 int result=D2GAME_EventFunc05_6FD0EDE0(nullptr,17,guard==1?nullptr:&victim,guard==2?nullptr:&healed,guard==6?nullptr:&damage,82,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",physical,hp,maximum,pct,guard,rank,healed.hp,result,observedRank,overlays,formulaOwner);first=false;
 }}printf("]\n");}
'''
    STAGE.mkdir(exist_ok=True);src=STAGE/'probe.cpp';src.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(src),'-o',str(STAGE/'probe')],capture_output=True);(STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    rows=json.loads(run.stdout);accepted=0
    for physical,hp,maximum,pct,guard,rank,remaining,result,observed,overlays,owner in rows:
        evaluated=guard in [0,12] and physical>0;success=evaluated and hp>0 and maximum>0
        expected=min(maximum,max(1,hp+percentage(physical,pct))) if success else hp
        assert (remaining,result,observed,overlays,owner)==(expected,int(success),(1 if rank<0 else rank) if evaluated else -99,int(success),int(evaluated and guard!=12))
        accepted+=success
    assert (len(rows),accepted)==(22464,1536)
    cases=STAGE/'cases.json';cases.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    table=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';row=json.loads(table.read_text())['82']
    assert [row['auraevent1'],row['auraevent2']]==['damagedinmelee','damagedbymissile'] and row['auraeventfunc1']==row['auraeventfunc2']==5 and [row['Param5'],row['Param6']]==[50,0]
    for p in [Path(__file__),old_script,prior,table,ROOT/'tools/review_weapon_event_order.py']:inputs[str(p.relative_to(ROOT))]=sha(p)
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],eventContexts=len(rows),acceptedContexts=accepted,
        routines=dict(event=hashlib.sha256(event.encode()).hexdigest(),percentage=hashlib.sha256(helper.encode()).hexdigest()),inputs=inputs,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [src,cases,STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Complete unchanged Life Tap event and actual shared percentage routine execute fractional healing,HP/max caps,source actor/state/statlist/caster guards,rank normalization and overlays.','Large non-divisible physical values distinguish early division from the old supplementary probe percentage stub:1048577at50percent heals524250FP,not524288FP. Current skill remains50percent at every rank.'],
        remaining=['Prior accepted scaffold uses declared unit/stat/owner/formula/overlay services;these are retained and recorded,not the full engine. The old scaffold and artifacts are not overwritten.','Full event host/current executable,NPC/item registration,mitigation/resource/HP/death/PvP/world/client chronology remain separate work. Synthetic percent0/1/100controls do not change the current50percent skill.'])
    dest=ROOT/'tools/reference/life-tap-event-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==text
    else:dest.write_text(text)
    print('Life Tap:22464 complete event/actual-percentage contexts,1536accepted;ASan/UBSan passed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
