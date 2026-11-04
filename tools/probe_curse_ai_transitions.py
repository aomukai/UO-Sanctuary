#!/usr/bin/env python3
"""Compose the complete older AI table lookup and state transition dispatcher."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/curse-ai-transitions-source'
FAMILIES={'Skeleton':2,'Zombie':3,'Fallen':6,'Brute':7,'Goatman':12,'Megademon':89}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def build(check=False):
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    rel='source/D2Game/src/AI/AiThink.cpp'
    path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel
    raw=path.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
    names=['AITHINK_GetAiTableRecord','AITHINK_ExecuteAiFn']
    bodies={name:function(raw.decode(),name)for name in names}
    lookup=bodies[names[0]]
    table=lookup.split('gpAiTable_6FD3F990[] =',1)[1].split('};',1)[0]
    rows=re.findall(r'\{\s*(\d+),\s*(\w+),\s*(\w+),\s*(\w+)\s*\}',table)
    for family,index in FAMILIES.items():
        flags,init,tick,exit=rows[index]
        assert flags=='1'and init==exit=='nullptr'
        assert family in tick or family=='Goatman'and 'Goatman_Swarm'in tick
    pointers=sorted({p for r in re.findall(r'\{\s*\d+,\s*(\w+),\s*(\w+),\s*(\w+)\s*\}',lookup)for p in r if p!='nullptr'})
    cpp=r'''#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>
#define __fastcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
#define D2_VERSION_HAS_UBERS 1
#define TRUE 1
using FARPROC=void(*)();
enum D2C_AiSpecialState:uint32_t{AISPECIALSTATE_NONE=0,AISPECIALSTATE_NO_MON_STATS=1,AISPECIALSTATE_DIMVISION=10,AISPECIALSTATE_TERROR=11,AISPECIALSTATE_TAUNT=12,AISPECIALSTATE_TABLE_COUNT=18};
constexpr int UNIT_MONSTER=1,STATE_UNINTERRUPTABLE=1;
struct D2GameStrc{};
struct D2MonStatsTxt{int wAI=0;};struct D2MonStats2Txt{};
struct MonsterData{D2MonStatsTxt* pMonstatsTxt;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=0;MonsterData* pMonsterData=nullptr;};
struct D2AiTickParamStrc;
using AIPARAMFN=void(*)(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*);
struct D2AiControlStrc{D2C_AiSpecialState nAiSpecialState=AISPECIALSTATE_NONE;AIPARAMFN pAiParamFn=nullptr;int dwAiParam[3]={11,22,33};};
struct D2AiTickParamStrc{D2AiControlStrc* pAiControl;D2MonStatsTxt* pMonstatsTxt;D2MonStats2Txt* pMonstats2Txt;};
struct D2AiTableStrc{int unk0x00;AIPARAMFN unk0x04,pAiParamFn,unk0x0C;};
struct Tables{int nMonStatsTxtRecordCount=751;}tables;auto* sgptDataTables=&tables;
D2MonStatsTxt mon;D2MonStats2Txt modes;int freed=0,warnings=0;bool switchable=true;
std::vector<int>callbacks;
bool UNITS_CanSwitchAI(int){return switchable;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
void FOG_DisplayWarning(const char*,const char*,int){warnings++;}
void FOG_DisplayAssert(const char*,const char*,int){std::abort();}
bool IsBadCodePtr(FARPROC){return false;}
void AIGENERAL_FreeAllAiCommands(D2GameStrc*,D2UnitStrc*){freed++;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &modes;}
'''
    for i,p in enumerate(pointers):cpp+=f'void {p}(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*){{callbacks.push_back({i});}}\n'
    cpp+='\n'.join(bodies.values())
    cpp+='\nint main(){D2GameStrc game;MonsterData data{&mon};D2UnitStrc unit;unit.pMonsterData=&data;bool comma=false;printf("[");\n'
    cpp+='int indices[]={'+','.join(map(str,FAMILIES.values()))+'};\n'
    cpp+=r'''for(int index:indices)for(int old:{0,10,11,12})for(int next:{0,10,11,12})for(int pointer:{0,1,2}){
mon.wAI=index;freed=warnings=0;callbacks.clear();D2AiControlStrc ai;ai.nAiSpecialState=static_cast<D2C_AiSpecialState>(old);
ai.pAiParamFn=pointer==0?nullptr:pointer==1?AITHINK_GetAiTableRecord(&unit,ai.nAiSpecialState)->pAiParamFn:AITHINK_Fn000;
AITHINK_ExecuteAiFn(&game,&unit,&ai,static_cast<D2C_AiSpecialState>(next));
auto* expected=AITHINK_GetAiTableRecord(&unit,static_cast<D2C_AiSpecialState>(next));
assert(freed==1&&warnings==0&&ai.nAiSpecialState==next&&ai.pAiParamFn==expected->pAiParamFn);
assert(ai.dwAiParam[0]==0&&ai.dwAiParam[1]==0&&ai.dwAiParam[2]==0);
assert(callbacks.size()==(next==11?1:0));
printf("%s[%d,%d,%d,%d,%d,%d,%zu]",comma?",":"",index,old,next,pointer,freed,ai.nAiSpecialState,callbacks.size());comma=true;
}
// Sand Maggot owns a non-null exit callback. A correct active pointer takes
// the deferred exit route instead of immediately clearing commands/parameters.
mon.wAI=15;freed=0;callbacks.clear();D2AiControlStrc ai;ai.pAiParamFn=AITHINK_GetAiTableRecord(&unit,AISPECIALSTATE_NONE)->pAiParamFn;
AITHINK_ExecuteAiFn(&game,&unit,&ai,AISPECIALSTATE_DIMVISION);
assert(freed==0&&ai.dwAiParam[0]==11&&ai.pAiParamFn==D2GAME_AI_Unk015_6FCD34A0&&ai.nAiSpecialState==10&&callbacks.empty());
printf("]\n");}
'''
    STAGE.mkdir(parents=True,exist_ok=True);source=STAGE/'probe.cpp';source.write_text(cpp)
    compile=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],capture_output=True)
    (STAGE/'compiler.log').write_bytes(compile.stdout+compile.stderr);assert compile.returncode==0,compile.stderr.decode()[-4000:]
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    cases=json.loads(run.stdout);assert len(cases)==288
    (STAGE/'cases.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],families=FAMILIES,cases=len(cases),
        routines={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},
        evidence={str(p.relative_to(ROOT)):sha(p)for p in [path,Path(__file__),source,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Six ordinary families have no source entry/exit callbacks. Complete table lookup and transition dispatch clear three AI parameters and all commands, then install the selected state function for288 old/new/pointer contexts. Terror alone calls its existing initializer service.',
            'Sand Maggot counterexample follows its deferred exit callback without clearing commands. It is not admitted by the ordinary-family bridge.'],
        limits=['Function pointers outside the two complete dispatcher bodies are identity/invocation services; their AI bodies do not execute. Terror initializer and blind/fear behavior retain their existing independent source/native evidence.',
            'Current monstats family names are pinned data. This does not prove current executable table indices, engine timing, native path/scan/global RNG, or unsupported ownership/conversion transitions. No native monster classifications are invented.'])
    output=ROOT/'tools/reference/curse-ai-transitions-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==render
    else:output.write_text(render)
    print('Curse AI transitions:288 complete source lookup/dispatch cases; six families; deferred-exit counterexample.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
