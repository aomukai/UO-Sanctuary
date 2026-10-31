#!/usr/bin/env python3
"""Execute unchanged older Bone Armor event22; isolate its overflow discrepancy."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'bone-armor-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(path):
        p=BASE/'d2moo-pinned-source'/path;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);return p,raw.decode()
    src,raw=source('source/D2Game/src/SKILLS/SkillNec.cpp');body=function(raw,'D2GAME_EventFunc22_6FD0F000')
    pct,pctraw=source('source/D2Game/src/MONSTER/MonsterUnique.cpp');percentage=function(pctraw,'MONSTERUNIQUE_CalculatePercentage')
    parent=BASE/'cyclone-armor-source-probe.cpp';cpp=parent.read_text().split('int32_t __fastcall D2GAME_EventFunc25_6FD00140')[0];assert percentage in cpp
    assert cpp.count('assert(id==235)')==1;cpp=cpp.replace('assert(id==235)','assert(id==68)');cpp='#include <cstdlib>\n'+cpp+body+r'''
int main(){D2GameStrc game;bool first=true;printf("[");
for(int guard=0;guard<=10;guard++)for(int capacity:{0,1,127,256,512,1024})for(int maximum:{0,1024})
for(int physical:{0,1,127,256,512,1024,2048})for(int old:{0,25,100}){
 if(guard&&(capacity!=256||maximum!=1024||physical!=512||old!=25))continue;
 row={};rowPresent=guard!=3;listPresent=guard!=6;if(guard==4)row.nAuraState=-1;if(guard==5)row.nAuraState=8;
 if(guard==7)row.wAuraStat[1]=-1;if(guard==8)row.wAuraStat[1]=4;if(guard==9)row.wAuraStat[0]=-1;if(guard==10)row.wAuraStat[0]=4;
 list={};list.stats[0]=old;list.stats[1]=capacity;list.stats[2]=maximum;removed=freed=gfx=detached=0;
 D2DamageStrc damage;damage.dwPhysDamage=physical;
 int result=D2GAME_EventFunc22_6FD0F000(&game,0,guard==1?nullptr:&actor,&other,guard==2?nullptr:&damage,68,1);
 assert(damage.dwFireDamage==0&&damage.dwColdDamage==0&&damage.dwLtngDamage==0&&damage.dwPoisDamage==23&&damage.dwMagDamage==29&&removed==freed&&freed==detached);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,capacity,maximum,physical,old,result,list.stats[1],damage.dwPhysDamage,removed,freed,gfx,list.stats[0]);first=false;
}printf("]\n");}
'''
    generated=STAGE/'probe.cpp';generated.write_text(cpp);binary=STAGE/'probe'
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],capture_output=True)
    (STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    rows=json.loads(run.stdout);assert len(rows)==262
    for guard,cap,maximum,damage,old,result,left,out,removed,freed,gfx,new in rows:
        valid=guard==0 and maximum>0;remaining=max(0,cap-damage) if valid else cap
        # Preserve exactly the observed older exhaustion branch: zeroing the
        # pool before subtracting it leaves that full incoming hit unchanged.
        output=0 if valid and cap>damage else damage
        percent=remaining*100//maximum if maximum else 0;changed=valid and remaining>0 and abs(percent-old)>=5
        assert result==int(valid) and left==remaining and out==output and removed==freed==int(valid and remaining==0)
        assert gfx==int(changed) and new==(percent if changed else old)
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';skill=json.loads((refs/'skills.json').read_text())['68'];assert skill['auraeventfunc1']==22
    fields=['skill','*Id','srvdofunc','aurastate','aurastat1','aurastatcalc1','aurastat2','aurastatcalc2','auraevent1','auraeventfunc1','Param1','Param2','Param8']
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceSha256=sha(src),routineSha256=hashlib.sha256(body.encode()).hexdigest(),skill={k:skill.get(k) for k in fields},cases=262,
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [src,pct,parent,refs/'skills.json']},artifacts={str(p.relative_to(ROOT)):sha(p) for p in [generated,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Complete unchanged event22 and percentage helper execute252numeric contexts and10missing/invalid service guards with clean ASan/UBSan. Within-pool hits subtract exact FP256 physical damage;nonphysical channels are untouched.',
            'Positive maximum with exhausted pool unlinks/frees the state. Its percentage marker updates on an absolute change of at least5;this is separate from final presentation.',
            'At damage>=remaining the older source zeroes nValue0 before subtracting it,so the full hit passes through. This is a demonstrated discrepancy from Sanctuary current min(pool,damage) overflow,not a modern behavioral certificate.'],
        remaining=['The native correction retains existing conventional overflow while fixing fractional consumption;modern overflow behavior remains unverified. No silent legacy-bug port is authorized by this receipt.',
            'Stat lists,row/layout,unlink/gfx and actor services are supplied;full modern do18/event registration,incoming event ordering,client and old-content migration require separate evidence.'])
    path=ROOT/'tools/reference/bone-armor-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Bone Armor:262complete event22contexts;FP consumption established,older exhaustion discrepancy retained explicitly.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
