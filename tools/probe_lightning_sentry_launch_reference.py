#!/usr/bin/env python3
"""Compose the generic NPC skill handler, absolute-aim wrapper and factory.

Uses the existing complete factory harness; no charged-bolt fan is substituted.
Animation, native movement and retired-owner lifetime remain separate.
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable,str(ROOT/'tools/probe_guided_arrow_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    shared=json.loads((ROOT/'tools/reference/guided-arrow-engine-evidence.json').read_text())
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    relative='source/D2Game/src/SKILLS/Skills.cpp'
    raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    routines={}
    def routine(name):
        source=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',source,re.M);assert m,name
        end,depth=source.index('{',m.start())+1,1
        while depth:depth+=(source[end]=='{')-(source[end]=='}');end+=1
        body=source[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    pin=ROOT/'tools/reference/d2r-named-equipment'
    manifest=json.loads((pin/'sources.json').read_text());tables={};pins={}
    for name in ['skills','missiles']:
        data=(pin/'json'/f'{name}.json').read_bytes();pins[name]=hashlib.sha256(data).hexdigest()
        assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(data)
    actor=tables['skills']['313'];missile=tables['missiles']['sentrylightningbolt']
    assert actor['srvmissile']=='sentrylightningbolt' and not actor.get('srvdofunc') and not actor.get('srvstfunc')
    assert missile['Range']=='10 +  (lvl *  2)' and missile['Vel']==30 and missile['Size']==2
    base=(STAGE/'guided-arrow-source-probe.cpp').read_text().split('int main()',1)[0]
    def replace(old,new):
        nonlocal base
        assert base.count(old)==1,old
        base=base.replace(old,new)
    replace('#include <algorithm>','#include <algorithm>\n#include <cassert>')
    replace('nSeqCurrentFramePrecise=0,','dwSeqFrame=0,nSeqCurrentFramePrecise=0,')
    replace('struct D2SkillsTxt{int wSrvMissileA=86,wSrvMissileB=86;int dwCalc[4]={};};',
            'struct D2SkillsTxt{int wSrvMissileA=86,wSrvMissileB=86;int dwCalc[4]={};unsigned dwFlags[2]={};int wSrvDoFunc=0,wSrvStartFunc=0,wSrvMissile=526,wItemEffect=1,dwDelay=0,nAnim=18;};')
    replace('int nMissilesTxtRecordCount=1000;','int nMissilesTxtRecordCount=1000,pMissilesTxt[1000]={};')
    replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return calc;}',
            'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 0;}')
    base+=r'''
constexpr int SKILLSFLAGINDEX_INTOWN=1,SKILLSFLAGINDEX_LOB=3,SKILLSFLAGINDEX_DECQUANT=4,SKILLSFLAGINDEX2_USEMANAONDO=5,PLRMODE_TOWNNEUTRAL=1,PLRMODE_SEQUENCE=18;
struct D2SkillStrc{int id=313,rank=1;}used,highest;
using SkillDoFunc=int32_t(*)(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);
SkillDoFunc gpSkillSrvStartFnTable_6FD408B0[70]={},gpSkillSrvDoFnTable_6FD40A20[160]={};
bool haveUsed=true,haveHighest=true;int consumeCalls=0,clears=0;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return haveUsed?&used:nullptr;}
int SKILLS_GetFlags(D2SkillStrc*){return 0;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* s,const char*,int){return s->id;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc* s,int){return s->rank;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return haveHighest?&highest:nullptr;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){haveUsed=false;clears++;}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){assert(false);}
int sub_6FD10CE0(D2UnitStrc*){assert(false);return 0;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->x;}
int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->y;}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
int D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc* u,int,int){assert(u->dwUnitType==UNIT_MONSTER);consumeCalls++;return 0;}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int){assert(false);}
D2UnitStrc* sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){assert(false);return nullptr;}
'''
    base+=routine('sub_6FD11420')+routine('D2GAME_SKILLS_Handler_6FD12BA0')
    base+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
int aims[][2]={{50,70},{40,70},{0,70},{50,0},{139,70},{140,70}};
for(int rank:{1,3,20,255})for(int aim=0;aim<6;aim++)for(int mode:{0,1,2})for(int gates=0;gates<64;gates++)for(int tohit:{0,77}){
 reset();owner.dwUnitType=UNIT_MONSTER;owner.dwClassId=410;skill={};row={};row.nVel=row.nMaxVel=30;row.wRange=10;row.wLevRange=2;row.dwDamageRate=0;
 hasSkill=gates&1;hasRoom=gates&2;allocated=gates&4;haveUsed=gates&8;haveHighest=gates&16;hasRecord=gates&32;used={313,rank};highest={313,rank};consumeCalls=clears=0;
 owner.stats[STAT_TOHIT]=tohit;owner.path.x=aims[aim][0];owner.path.y=aims[aim][1];target.x=aims[aim][0];target.y=aims[aim][1];target.dead=mode==2;selected=mode?&target:nullptr;
 int result=D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,313,rank,1,0,0);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,aim,mode,gates,tohit,result,creates,created.dwUnitType==UNIT_MISSILE,creates&&allocated?created.x:0,creates&&allocated?created.y:0,creates&&allocated?created.path.x:0,creates&&allocated?created.path.y:0,creates&&allocated?created.total:0,creates&&allocated?created.path.velocity:0,creates&&allocated?created.skill:0,consumeCalls,clears,created.stats[STAT_TOHIT]);first=false;
}printf("]\n");}
'''
    path=STAGE/'lightning-sentry-launch-source-probe.cpp';path.write_text(base);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'lightning-sentry-launch-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    data=json.loads(run.stdout);assert len(data)==9216
    aims=[(50,70),(40,70),(0,70),(50,0),(139,70),(140,70)]
    for rank,aim,mode,gates,tohit,result,creates,is_missile,x,y,tx,ty,total,velocity,skill_id,pay,clears,attack in data:
        row_ok,room,allocated,used,highest,missile=[bool(gates&(1<<i)) for i in range(6)]
        # A present used skill checks the room before any missile or skill fallback.
        reaches_handler=row_ok and (not used or room) and (used or highest)
        coordinates=all(aims[aim]);distance_ok=abs(aims[aim][0]-40)<100 and abs(aims[aim][1]-70)<100
        allocation=reaches_handler and coordinates and missile and room and distance_ok
        success=allocation and allocated
        assert (result,pay,clears,creates,is_missile)==(int(reaches_handler),int(reaches_handler),int(row_ok and used and not room),int(allocation),int(success)),(rank,aim,mode,gates,tohit,result,pay,clears,creates,is_missile)
        if success:
            target=(41,71) if aims[aim]==(40,70) else aims[aim]
            assert (x,y,tx,ty,total,velocity,skill_id)==(40,70,*target,10+2*rank,5760,313)
        else:assert (x,y,tx,ty,total,velocity,skill_id)==(0,)*7
        assert attack==(tohit if success else 0)
    fixture=STAGE/'lightning-sentry-launch-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',talentId='assassin.lightningsentry',handlerEnabled=False,legacyCommit=tree['sha'],
        sourceHashes={relative:hashlib.sha256(raw).hexdigest(),'source/D2Game/src/MISSILES/Missiles.cpp':shared['sourceHashes']['source/D2Game/src/MISSILES/Missiles.cpp']},
        compiledRoutineHashes={**routines,'MISSILES_CreateMissileFromParams':shared['blockHashes']['MISSILES_CreateMissileFromParams']},pinnedInputHashes=pins,
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data)),
        established=['Current NPC313 has no custom start/do and launches missile526 through the complete generic handler and absolute-target wrapper. One factory call is attempted; there is no CBS fan, charged-bolt seed or waypoint initialization.',
            'Absolute coordinates and flags33 originate at the trap. A present target, including a dead but registered target, supplies its current position; otherwise the retained first path point supplies aim. Either zero coordinate suppresses allocation. Same-position moving aim falls back to(+1,+1).',
            'Complete factory uses current affine range10+2*rank and velocity30*256*75/100=5760FP8; actor skill313/rank stay stored while the separate source-zero damage proof selects player271 from missile526.',
            'The generic handler reports success and reaches its generic payment callback even when coordinates, room, missile row or allocation prevent a projectile. This is not proof of a player mana debit: the supplier is an NPC and the consumer is an explicitly declared service here.',
            'Missing skill rows and missing current/highest skills reject before launch. Present used skill with no room clears the used skill. Optional NPC attack rating adds the factory attack-bonus flag; it does not change the zero-ToHit current missile into an attack-roll skill.'],
        services=['Existing Guided Arrow complete-factory harness supplies room/allocation/path/stat/owner/damage/event services; unchanged bodies are fingerprinted. Damage, path construction and stat writes are not recomposed in this launch proof.',
            'Used/highest skill lookup, coordinate accessors, zero delay and NPC mana-consumer calls are explicit services. Noncurrent lob/item/quantity/mana-gate branches abort. Native NPC animation/action caller, actual world movement/hits, retirement and modern executable behavior remain separate.'],
        sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out=ROOT/'tools/reference/lightning-sentry-launch-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Lightning Sentry launch evidence stale'
    else:out.write_text(encoded)
    print('Lightning Sentry:9,216 complete generic-handler/absolute-aim/factory contexts; sanitizers clean.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
