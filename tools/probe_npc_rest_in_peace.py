#!/usr/bin/env python3
"""Execute the complete older RIP callback with an explicit unit-state service."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
from probe_npc_damage_fill import enum
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-rest-in-peace-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={}
    def read(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    body=function(read('source/D2Game/src/SKILLS/SkillItem.cpp'),'SKILLITEM_EventFunc29_RestInPeace')
    state=enum(read('source/D2Common/include/D2States.h'),'D2C_States')['STATE_RESTINPEACE']
    stat=enum(read('source/D2Common/include/D2StatList.h'),'D2C_ItemStats')['STAT_ITEM_RESTINPEACE'];assert stat==108
    for name in ['npc-item-combat-events','equipment-stat-costs']:
        p=ROOT/'data/Sanctuary/Balance'/f'{name}.json';rows=json.loads(p.read_text());inputs[str(p.relative_to(ROOT))]=sha(p)
        row=next(r for r in rows if r.get('Stat',r.get('Id'))==108);assert row['Name']=='item_restinpeace'
        if name=='npc-item-combat-events':assert row['Events']==[{'Event':'kill','Function':29}]
    cpp='''#include <cstdint>
#include <cassert>
#include <cstdio>
#include <initializer_list>
#define __fastcall
struct D2GameStrc{};struct D2DamageStrc{};struct D2UnitStrc{bool rip=false;};
constexpr int STATE_RESTINPEACE='''+str(state)+''';int calls=0;
void STATES_ToggleState(D2UnitStrc* p,int state,int enabled){assert(p&&state==STATE_RESTINPEACE&&enabled==1);p->rip=true;calls++;}
'''+body+r'''
int main(){D2GameStrc g;D2DamageStrc damage;D2UnitStrc source,target;bool first=true;printf("[");
for(int src:{0,1})for(int dst:{0,1})for(int old:{0,1})for(int event:{0,1,10,99})for(int rank:{-1,0,1,40}){
target.rip=old;calls=0;int result=SKILLITEM_EventFunc29_RestInPeace(&g,event,src?&source:nullptr,dst?&target:nullptr,&damage,108<<16,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",src,dst,old,event,rank,result,target.rip,calls);first=false;}printf("]\n");}
'''
    assert function(cpp,'SKILLITEM_EventFunc29_RestInPeace')==body
    src=STAGE/'source.cpp';src.write_text(cpp);binary=STAGE/'probe'
    with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(src),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
    rows=json.loads(run.stdout);assert len(rows)==128;out=STAGE/'cases.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,contexts=128,constants={'STAT_ITEM_RESTINPEACE':stat,'STATE_RESTINPEACE':state},completeRoutineHashes={'SKILLITEM_EventFunc29_RestInPeace':hashlib.sha256(body.encode()).hexdigest()},inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [src,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out]},
        scope='Complete unchanged callback29:nonnull attacker/target toggles target state on and returns1,including an already-active state;missing pointers return0. Packet,event,rank,bonus,RNG and health are not read. Unit-state storage is an explicitly declared service.',
        limits=['No current-executable parity or global kill-producer certificate. Native pre-death state/corpse transfer,loot,eligibility and persistence require separate acceptance;no player forwarding is inferred.'])
    path=ROOT/'tools/reference/npc-rest-in-peace-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Rest in Peace:128 complete callback contexts;state/stat IDs independently bound;ASan/UBSan pass.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
