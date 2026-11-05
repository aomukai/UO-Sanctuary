#!/usr/bin/env python3
"""Execute complete pinned item-effect handler and target/position wrappers."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function
ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'item-effect-casts-source'
NAMES = ('SKILLITEM_HandleItemEffectSkill', 'SKILLITEM_CastSkillOnTarget', 'SKILLITEM_CastSkillOnPosition')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True)
    tree = json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {}
    def read(relative):
        p = BASE/'d2moo-pinned-source'/relative
        raw = p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path']==relative)
        inputs[str(p.relative_to(ROOT))] = sha(p)
        return raw.decode()
    text = read('source/D2Game/src/SKILLS/SkillItem.cpp')
    bodies = {n:function(text,n) for n in NAMES}
    header = read('source/D2Common/include/DataTbls/SkillsTbls.h')
    assert header[header.index('SKILLSFLAGINDEX2_TGTPLACECHECK = 0'):header.index('SKILLSFLAGINDEX2_ITEMCHECKSTART')].count(',') == 1
    for value,name in enumerate(('ATTACKER','CASTER','RANDOM','RANDOM_CORPSE','LAST_ATTACKER')):
        assert f'SKILLSITEMTARGET_{name} = {value}' in header
    assert 'UNITFLAG_SKSRVDOFUNC = 0x00000040' in read('source/D2Common/include/Units/Units.h')
    cpp = r'''
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int SKILLSITEMTARGET_CASTER=1,SKILLSITEMTARGET_RANDOM=2,SKILLSITEMTARGET_RANDOM_CORPSE=3,SKILLSITEMTARGET_LAST_ATTACKER=4;
constexpr int SKILLSFLAGINDEX2_ITEMCHECKSTART=1,UNITFLAG_SKSRVDOFUNC=64,STATE_UNINTERRUPTABLE=1;
uint32_t gdwBitMasks[32];
struct D2GameStrc{};
struct D2UnitStrc;
struct D2DynamicPathStrc{int x=17,y=19;D2UnitStrc*target=nullptr;};
struct D2UnitStrc{int dwUnitType=1,dwUnitId=0;D2GameStrc*pGame=nullptr;D2DynamicPathStrc*pDynamicPath=nullptr;uint32_t dwFlags=0;};
struct D2SkillsTxt{uint16_t wItemEffect=1;uint8_t nItemTarget=0;uint32_t dwFlags[2]={};}record;
D2UnitStrc units[7];D2DynamicPathStrc path;bool exists=true,interrupted=false,corpse=false,attacker=false;int findResult=0,startResult=0,executeResult=0;
std::vector<std::vector<int>>trace;
int tag(D2UnitStrc*p){return p?p->dwUnitId:0;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int skill){trace.push_back({0,skill});return exists?&record:nullptr;}
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*p){return p->pDynamicPath->target;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*p){return p->x;}
int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*p){return p->y;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*p,D2UnitStrc*t){trace.push_back({1,tag(t)});p->pDynamicPath->target=t;}
void D2COMMON_10170_PathSetTargetPos(D2DynamicPathStrc*p,int x,int y){trace.push_back({2,x,y});p->target=nullptr;p->x=x;p->y=y;}
int SKILLITEM_FindTargetPosition(D2GameStrc*,D2UnitStrc*p,int skill,int level){trace.push_back({3,skill,level});if(findResult){D2COMMON_10170_PathSetTargetPos(p->pDynamicPath,findResult==1?0:23,findResult==2?0:29);}return findResult!=0;}
D2UnitStrc*sub_6FD15210(D2UnitStrc*,D2UnitStrc*t,int skill,int level){trace.push_back({4,tag(t),skill,level});return corpse?&units[4]:nullptr;}
D2UnitStrc*SUNIT_GetLastAttacker(D2GameStrc*,D2UnitStrc*){trace.push_back({5});return attacker?&units[5]:nullptr;}
void mutate(D2UnitStrc*p,int x,int y){D2COMMON_10170_PathSetTargetPos(p->pDynamicPath,x,y);UNITS_SetTargetUnitForDynamicUnit(p,&units[6]);p->dwFlags^=UNITFLAG_SKSRVDOFUNC;}
int sub_6FD12950(D2GameStrc*,D2UnitStrc*p,int skill,int level,int item){trace.push_back({6,skill,level,item});mutate(p,31,37);return startResult;}
int D2GAME_SKILLS_Handler_6FD12BA0(D2GameStrc*,D2UnitStrc*p,int skill,int level,int mode,int item,int flag){trace.push_back({7,skill,level,mode,item,flag,tag(path.target),path.x,path.y,(p->dwFlags&64)!=0});mutate(p,41,43);return executeResult;}
int STATES_CheckState(D2UnitStrc*,int){return interrupted;}
void sub_6FCC60D0(D2UnitStrc*p,int skill,int level,int type,int id,int flag){trace.push_back({8,skill,level,type,id,flag,tag(path.target),path.x,path.y,(p->dwFlags&64)!=0});}
void sub_6FCC6150(D2UnitStrc*p,int skill,int level,int x,int y,int flag){trace.push_back({9,skill,level,x,y,flag,tag(path.target),path.x,path.y,(p->dwFlags&64)!=0});}
'''+'\n'.join(bodies.values())+r'''
bool comma=false;
void run(int entry,int targetKind,int original,int serverDo,int requested,int find,int hasCorpse,int hasAttacker,int check,int start,int execute,int guard){
D2GameStrc game;path=D2DynamicPathStrc{};for(int i=1;i<7;i++){units[i]=D2UnitStrc{};units[i].dwUnitId=i;units[i].pGame=&game;units[i].pDynamicPath=&path;}
path.target=original?&units[3]:nullptr;units[1].dwFlags=0x8000|(serverDo?64:0);if(guard==2)units[1].pGame=nullptr;
exists=guard!=4;interrupted=guard==3;corpse=hasCorpse;attacker=hasAttacker;findResult=find;startResult=start;executeResult=execute;
record.wItemEffect=guard==5?0:1;record.nItemTarget=targetKind;record.dwFlags[1]=check?2:0;trace.clear();
int skill=82,level=original?63:serverDo?0:5,flag=(targetKind+serverDo)%2;
D2UnitStrc*p=guard==1?nullptr:&units[1];int result=entry==1?SKILLITEM_CastSkillOnTarget(p,skill,level,requested?&units[2]:nullptr,flag):SKILLITEM_CastSkillOnPosition(p,skill,level,11,13,flag);
if(comma)printf(",");comma=true;printf("[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%u],[",entry,targetKind,original,serverDo,requested,find,hasCorpse,hasAttacker,check,start,execute,guard,level,flag,result,tag(path.target),path.x,path.y,units[1].dwFlags);
for(size_t i=0;i<trace.size();i++){printf("%s[",i?",":"");for(size_t j=0;j<trace[i].size();j++)printf("%s%d",j?",":"",trace[i][j]);printf("]");}printf("]]");
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
for(int entry:{1,2})for(int kind:{0,1,2,3,4,7})for(int original:{0,1})for(int flag:{0,1})for(int requested:{0,1}){
for(int find:{0,1,2,3})for(int corpse:{0,1})for(int attacker:{0,1})for(int check:{0,1})for(int start:{0,1})for(int execute:{0,1,7})run(entry,kind,original,flag,requested,find,corpse,attacker,check,start,execute,0);
for(int guard:{1,2,3,4,5})run(entry,kind,original,flag,requested,3,1,1,1,1,1,guard);
}printf("]\n");}
'''
    for name,body in bodies.items(): assert function(cpp,name)==body
    source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
    with (STAGE/'compiler.log').open('w') as log:
        subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(binary)],capture_output=True)
    (STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()
    cases=json.loads(run.stdout);assert len(cases)==18912,len(cases)
    output=STAGE/'cases.json';output.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=len(cases),inputs=inputs,
        completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in (source,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve())},
        established=['Three complete older routines execute both real wrapper call shapes with paired identity/position outputs. Return status,ordered target/path/search/start/execute/notification trace and final target/path/server-do state are emitted.',
            'Covers attacker/default,caster,random position,random corpse,last attacker and unknown target-kind default;missing source/target/game/record/effect,uninterruptible,search failure,start failure,zero and nonzero executor results,existing/no target,both original flag states and levels0/5/63.',
            'Nested start/execution mutate target,path and server-do to exercise cleanup even on failure. Existing original target restores the target only;otherwise first path point is restored. Notifications run after restoration. Position wrapper uses returned XY only when returned X is nonzero.'],
        services=['Target/path/getters,setters,skill metadata,random/corpse/last-attacker search,start and skill execution/notifications are declared services. The trace is a contract certificate,not execution of every underlying skill or native D2 room/path services.',
            'Current header pins item target enum0..4,item-check-start index1 and server-do bit0x40. Both actual wrappers always provide all four result pointers;other direct optional-output pointer combinations are not accepted.'],
        remaining=['Actual NPC spell adapter must preserve its own original AI/controller and not charge player mana. Only admitted spells may use this driver;unimplemented search/start/skill routes must fail explicitly.','Generic item event subscriptions and other spells,charge actions,full generated NPC item initializer,Shadow/Valk AI and current executable/client parity remain unfinished.'])
    target=ROOT/'tools/reference/item-effect-casts-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check: assert target.read_text()==encoded,'Item effect cast source evidence stale'
    else: target.write_text(encoded)
    print(f'Item effect casts:{len(cases)}complete source wrapper/handler controls;target/path/server-do restoration and notification routing;ASan+UBSan clean.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
