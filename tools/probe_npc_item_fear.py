#!/usr/bin/env python3
"""Complete item howl callback, direct Terror helper and curse-factory composition."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from probe_current_item_affixes import function
from probe_npc_damage_fill import enum

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'npc-item-fear-source'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    parent=BASE/'npc-item-blind-source/probe.cpp'
    proof=json.loads((ROOT/'tools/reference/npc-item-blind-source-evidence.json').read_text())
    assert sha(parent)==proof['artifacts'][str(parent.relative_to(ROOT))]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={str(parent.relative_to(ROOT)):sha(parent)}
    def read(name):
        path=BASE/'d2moo-pinned-source'/name;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==name)
        inputs[str(path.relative_to(ROOT))]=sha(path)
        return raw.decode()
    item=read('source/D2Game/src/SKILLS/SkillItem.cpp')
    util=read('source/D2Game/src/AI/AiUtil.cpp')
    skills=read('source/D2Game/src/SKILLS/Skills.cpp')
    bodies={name:function(text,name) for text,names in ((item,('SKILLITEM_EventFunc08_Howl',)),(util,('AIUTIL_ApplyTerrorCurseState',)),(skills,('sub_6FD10EC0',))) for name in names}
    values={}
    for name,kind in (('source/D2Common/include/D2Monsters.h','D2C_MonTypeFlags'),('source/D2Common/include/DataTbls/SkillsIds.h','D2C_Skills'),('source/D2Common/include/D2StatList.h','D2C_ItemStats')):
        values.update(enum(read(name),kind))
    wanted=('MONTYPEFLAG_UNIQUE','MONTYPEFLAG_CHAMPION','MONTYPEFLAG_SUPERUNIQUE','SKILL_HOWL','SKILL_TERROR','STAT_ITEM_HOWL')
    cpp=parent.read_text().split('D2UnitStrc actor,target;D2StatListStrc prior;')[0]
    cpp=cpp.replace('struct D2UnitStrc{','struct MonsterData{void* pAiControl=nullptr;};\nstruct D2UnitStrc{')
    cpp=cpp.replace('pSeed=0;void*','pSeed=0;MonsterData* pMonsterData=nullptr;D2UnitStrc* dynamicTarget=nullptr;void*')
    bound={name:values[name]for name in wanted}
    for name,value in bound.items():
        assert not re.search(r'\b'+name+r'=',cpp)
        cpp+='\nconstexpr int '+name+'='+str(value)+';'
    cpp+='''
int idleFrames=0;
bool AIUTIL_CanUnitSwitchAi(D2UnitStrc* p,int){return p&&p->aiAllowed;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc* p,D2UnitStrc* source){p->dynamicTarget=source;}
void AITACTICS_Idle(D2GameStrc*,D2UnitStrc*,int frames){idleFrames=frames;}
'''
    cpp+='\n'+bodies['AIUTIL_ApplyTerrorCurseState']+'\n'+bodies['SKILLITEM_EventFunc08_Howl']
    cpp+=r'''
D2UnitStrc actor,target;D2StatListStrc prior;MonsterData data;
void reset(){actor={};target={};prior={};actor.dwUnitId=7;target.dwUnitId=42;actor.dwUnitType=target.dwUnitType=UNIT_MONSTER;actor.room=target.room=&room;target.pMonsterData=&data;target.dwFlags=UNITFLAG_CANBEATTACKED|UNITFLAG_ISVALIDTARGET|UNITFLAG_TARGETABLE;row={};for(int&i:row.wAuraStat)i=-1;validRow=validState=validMon=validModes=validDifficulty=curseMask=true;room={};mon={};modes={};modes.dwFlags=1u<<MONSTATS2FLAGINDEX_ISATT;game={};game.dwGameFrame=100;removals=events=writer=updates=permissionCalls=aiCalls=traversals=draws=idleFrames=0;actor.stats[STAT_ITEM_HOWL]=33;raw=0;}
void result(int success){auto*p=target.list;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",success,draws,p?p->skill:0,p?p->rank:0,p?p->creator:0,p?p->expire:0,target.dynamicTarget?target.dynamicTarget->dwUnitId:0,aiCalls,idleFrames,removals,events);}
int main(){printf("{\"callback\":[");bool first=true;
for(int bonus:{-1,0,1,33,52,64,128,200})for(uint32_t roll:{0u,1u,32u,33u,127u,128u,255u})for(int a:{-1,0,1})for(int t:{-1,0,1,3})for(int flags:{0,MONTYPEFLAG_UNIQUE,MONTYPEFLAG_CHAMPION,MONTYPEFLAG_SUPERUNIQUE})for(int event:{5,6}){
reset();actor.stats[STAT_ITEM_HOWL]=bonus;raw=roll;actor.dwUnitType=a;target.dwUnitType=t;target.monType=flags;
printf("%s{\"input\":[%d,%u,%d,%d,%d,%d],\"output\":",first?"":",",bonus,roll,a,t,flags,event);
int success=SKILLITEM_EventFunc08_Howl(&game,event,a<0?nullptr:&actor,t<0?nullptr:&target,nullptr,STAT_ITEM_HOWL<<16,0);result(success);printf("}");first=false;}
printf("],\"composition\":[");first=true;
for(int old:{0,1,2,3,4})for(int allow:{0,1})for(int dead:{0,1})for(int resistance:{0,50,99,100})for(int metadata:{0,1})for(int state:{0,1})for(int npc:{0,1})for(int attackable:{0,1}){
reset();target.aiAllowed=allow;target.dead=dead;target.stats[STAT_CURSE_RESISTANCE]=resistance;target.pMonsterData=metadata?&data:nullptr;validState=state;if(npc)mon.dwMonStatsFlags=1u<<MONSTATSFLAGINDEX_NPC;if(!attackable)modes.dwFlags=0;
prior.skill=old==3?66:old==4?86:SKILL_TERROR;prior.state=old==3?19:old==4?STATE_ATTRACT:STATE_TERROR;prior.rank=old==2?2:1;prior.creator=999;prior.expire=777;target.list=old?&prior:nullptr;target.attract=old==4;
printf("%s{\"input\":[%d,%d,%d,%d,%d,%d,%d,%d],\"output\":",first?"":",",old,allow,dead,resistance,metadata,state,npc,attackable);
int success=SKILLITEM_EventFunc08_Howl(&game,5,&actor,&target,nullptr,STAT_ITEM_HOWL<<16,0);result(success);printf("}");first=false;}
printf("]}\n");}
'''
    for name,body in bodies.items():assert function(cpp,name)==body,name
    path=STAGE/'probe.cpp';path.write_text(cpp);binary=STAGE/'probe'
    with(STAGE/'compiler.log').open('w')as log:
        subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(binary)],capture_output=True,timeout=60)
    (STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    cases=json.loads(run.stdout)
    assert {k:len(v)for k,v in cases.items()}==dict(callback=5376,composition=1280)
    ordinary=next(r for r in cases['composition']if r['input']==[0,1,0,0,1,1,0,1])
    assert ordinary['output']==[1,1,77,1,7,120,7,1,1,0,1],ordinary
    attract=next(r for r in cases['composition']if r['input']==[4,1,0,0,1,1,0,1])
    assert attract['output']==[1,1,86,1,999,777,7,1,1,0,0],attract
    out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
    record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts={k:len(v)for k,v in cases.items()},completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},headerBindings=bound,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in (path,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log')},
        scope='Three complete unchanged callback08/direct Terror helper/curse factory bodies. The inherited blind proof provides the independently rebound full factory environment. Raw NPC RNG, AI eligibility, stat-list services and AI initializer/timer are declared services; real controller acceptance is separate. Chance uses raw&127; helper always supplies Terror rank1/20frames, then resets AI/target and idles one frame even when factory refuses. No difficulty or caster-level scale is introduced.',
        limits=['Older supplementary source is not a current-executable certificate. Full AI switch/class callbacks, global event scheduling, path/target scans and current-engine/PvP/client parity remain separate.'])
    target=ROOT/'tools/reference/npc-item-fear-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'NPC fear source proof stale'
    else:target.write_text(encoded)
    print('NPC item fear:5376 callback/1280 curse compositions; three unchanged routines; ASan/UBSan pass.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    build(parser.parse_args().check)
