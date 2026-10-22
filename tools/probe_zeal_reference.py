#!/usr/bin/env python3
"""Compile unchanged Zeal callbacks, cyclic selection and animation event scheduling.

This is supplementary legacy engine evidence, with explicit world/combat/formula stubs.
It does not certify the modern executable or the provenance of the legacy animation dump.
"""
from pathlib import Path
import hashlib
import json
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
sources = {
    'ama': (STAGE/'SkillAma.cpp.reference', 'c20eeddf9c781b711a9a769053ffdbffa6b037377bb162022f6a1f652224c129'),
    'skills': (ROOT/'.local/research/d2moo-Skills.cpp', 'ae7608f7c95eafd3d2b632af8ece88a1f6784490416e2f9ced3706bcbdd1dbcc'),
    'unit': (ROOT/'.local/research/d2moo-SUnit.cpp', 'b9faca7d12e19e95b072e952ba596b6b63fc9e41d902cfcbde0ae9d638ab5db6'),
    'anim': (ROOT/'.local/research/AnimData.d2', 'e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb'),
}
for path, expected in sources.values():
    assert hashlib.sha256(path.read_bytes()).hexdigest() == expected, path


def routine(key, name):
    text = sources[key][0].read_text()
    at = text.index(name+'(')
    start = text.rfind('\n', 0, at)+1
    end = text.index('{', at)+1
    depth = 1
    while depth:
        depth += (text[end] == '{')-(text[end] == '}')
        end += 1
    return text[start:end]


animations = []
blob = sources['anim'][0].read_bytes()
pos = 0
for bucket in range(256):
    count = struct.unpack_from('<i', blob, pos)[0]
    pos += 4
    for _ in range(count):
        row = blob[pos:pos+160]
        pos += 160
        code = row[:8].rstrip(b'\0').decode()
        if code in ('PAA1HTH', 'PAA11HS', 'PAA11HT', 'PAA12HS', 'PAA12HT', 'PAA1STF'):
            frames, speed = struct.unpack_from('<ii', row, 8)
            events = [[i, value] for i, value in enumerate(row[16:]) if value]
            assert len(events) == 1 and events[0][1] == 1 and speed == 256
            animations.append({'code': code, 'frames': frames, 'speed': speed, 'event': events[0][0]})
assert pos == len(blob) and len(animations) == 6

cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2;
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},dwParam[8]={0,100},nEType=0,nSrcDam=128;};
struct D2SkillStrc{int p[3]={};};
// The leading bytes reproduce the zero high byte of AnimData speed at frameFlags[-1].
struct Anim{uint32_t speed=256;uint8_t pFrameFlags[144]={};};
struct D2UnitStrc{int dwUnitType=1,dwUnitId=0,nSeqCurrentFramePrecise=0,dwSeqSpeed=0,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=15*256;void* pAnimSeq=nullptr;Anim* pAnimData=nullptr;bool inRange=true,alive=true;};
struct D2GameStrc{int dwGameFrame=100;};
struct D2DamageStrc{int wResultFlags=0,dwEnDmgPct=0,dwConvPct=0,nConvType=0;};
struct D2AuraCallbackStrc{void* pArgs;};
struct D2_6FD10880_Strc{D2UnitStrc* unk0x00=nullptr;uint32_t unk0x04=0;D2UnitStrc* unk0x08=nullptr;uint32_t unk0x0C=0,unk0x10=0;int unk0x14=0;};
struct Event{int type,frame,event,param;};
struct D2AnimSeqTxt{};
std::vector<D2UnitStrc*> candidates;std::vector<Event> events;D2SkillsTxt row;D2SkillStrc used;D2UnitStrc* selected=nullptr;
bool hasRow=true,hasSkill=true;int hitCount=5,hitFlag=1,allocations=0,drains=0,lastTarget=-1,lastDamage=0,lastSource=0,rollbacks=0,lastRollback=0,elementCalls=0;
int sub_6FD10880(D2AuraCallbackStrc*,D2UnitStrc*);
void sub_6FD0FE80(D2GameStrc*,D2UnitStrc*,int,int,int,uint32_t,int(*fn)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int,const char*,int){D2AuraCallbackStrc callback{args};for(auto p:candidates)if(p->alive&&p->inRange)fn(&callback,p);}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&used:nullptr;}
int sub_6FD15460(D2UnitStrc*){return 5;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected;}
void SKILLS_SetParam1(D2SkillStrc* p,int x){p->p[0]=x;}void SKILLS_SetParam2(D2SkillStrc* p,int x){p->p[1]=x;}void SKILLS_SetParam3(D2SkillStrc* p,int x){p->p[2]=x;}
int SKILLS_GetParam1(D2SkillStrc* p){return p->p[0];}int SKILLS_GetParam2(D2SkillStrc* p){return p->p[1];}int SKILLS_GetParam3(D2SkillStrc* p){return p->p[2];}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int){return calc==0?hitCount:336;}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int type,int id){for(auto p:candidates)if(p->dwUnitType==type&&p->dwUnitId==id&&p->alive)return p;return nullptr;}
bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc* p,int){return p->inRange;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){return hitFlag;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 200;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){elementCalls++;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t,D2DamageStrc* d,int src){allocations++;lastTarget=t->dwUnitId;lastDamage=d->dwEnDmgPct;lastSource=src;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int n){drains+=n;}
void sub_6FD15080(D2UnitStrc*,int n){rollbacks++;lastRollback=n;}
int UNITS_GetFrameBonus(D2UnitStrc*){return 0;} // Original Paladin A1 table: all weapon classes zero.
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){std::erase_if(events,[&](auto e){return e.type==type;});}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int event,int param){events.push_back({type,frame,event,param});}
'''
for key, name in [('skills','sub_6FD10880'), ('skills','sub_6FD107F0'), ('ama','SKILLS_SrvSt37_Zeal_Fury_BloodLordFrenzy'), ('ama','SKILLS_SrvDo013_Fend_Zeal_Fury'), ('unit','sub_6FCBCE70'), ('unit','sub_6FCBCFD0')]:
    cpp += '\n'+routine(key, name)+'\n'
cpp += r'''
int main(){D2GameStrc game;D2UnitStrc owner,a,b,c; a.dwUnitId=10;b.dwUnitId=20;c.dwUnitId=30;candidates={&c,&a,&b};
printf("{\"selectionCases\":[");bool first=true;
for(int last:{-1,0,10,15,20,30,40})for(int missing:{0,1,2}){a.alive=missing!=1;b.alive=missing!=2;auto target=sub_6FD107F0(&game,&owner,0,0,5,0x20003,last,nullptr);
if(!first)printf(",");first=false;printf("[%d,%d,%d]",last,missing,target?target->dwUnitId:-1);}
a.alive=b.alive=true;printf("],\"startCases\":[");first=true;
for(int guard:{0,1,2,3,4}){hasRow=guard!=1;hasSkill=guard!=2;selected=guard==3?nullptr:&b;candidates=guard==4?std::vector<D2UnitStrc*>{}:std::vector<D2UnitStrc*>{&c,&a,&b};if(guard==4)selected=nullptr;used={};
int ok=SKILLS_SrvSt37_Zeal_Fury_BloodLordFrenzy(&game,&owner,106,20);if(!first)printf(",");first=false;printf("[%d,%d,%d,%d]",guard,ok,used.p[0],used.p[2]);}
printf("],\"strikeCases\":[");first=true;
for(int miss:{0,1})for(int remaining:{1,2,5})for(int src:{0,128}){hasRow=hasSkill=true;hitFlag=miss?0:1;row.nSrcDam=src;used.p[0]=remaining;used.p[1]=1;used.p[2]=20;candidates={&c,&a,&b};allocations=drains=rollbacks=elementCalls=0;
int ok=SKILLS_SrvDo013_Fend_Zeal_Fury(&game,&owner,106,20);if(!first)printf(",");first=false;
printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",miss,remaining,src,ok,lastTarget,lastDamage,lastSource,used.p[0],used.p[2],rollbacks,drains,elementCalls);}
printf("],\"timingCases\":[");first=true;
'''
for animation in animations:
    cpp += f'''for(int advance:{{38,128,256,341,448}})for(int count:{{2,5}}){{
Anim anim;anim.pFrameFlags[{animation['event']}]=1;D2UnitStrc unit;unit.pAnimData=&anim;unit.wAnimSpeed=advance;unit.dwFrameCountPrecise={animation['frames']}*256;game.dwGameFrame=100;events.clear();sub_6FCBCE70(&game,&unit);std::vector<int> hits;int end=0;
for(int i=0;i<count;i++){{auto it=std::find_if(events.begin(),events.end(),[](auto e){{return e.type==EVENTTYPE_MODECHANGE;}});if(it==events.end())return 3;game.dwGameFrame=it->frame;hits.push_back(game.dwGameFrame-100);for(auto e:events)if(e.type==EVENTTYPE_ENDANIM)end=e.frame-100;if(i+1<count)sub_6FCBCFD0(&game,&unit,100);}}
if(!first)printf(",");first=false;printf("[\\"{animation['code']}\\",%d,%d,[",advance,count);for(size_t i=0;i<hits.size();i++)printf("%s%d",i?",":"",hits[i]);printf("],%d]",end);}}
'''
cpp += 'printf("]}\\n");}\n'
path = STAGE/'zeal-source-probe.cpp'
path.write_text(cpp)
binary = STAGE/'zeal-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data = json.loads(subprocess.check_output([str(binary)]))
for last, missing, selected in data['selectionCases']:
    ids = [x for x in (10,20,30) if (missing,x) not in ((1,10),(2,20))]
    assert selected == next((x for x in ids if x > last), ids[0])
assert data['startCases'] == [[0,1,5,20],[1,0,0,0],[2,0,0,0],[3,1,5,10],[4,0,0,0]]
for miss, remaining, src, ok, target, damage, source, left, next_target, rollback, drain, elements in data['strikeCases']:
    assert (ok,target,damage,source,left,drain,elements)==(0,20,0 if miss else 336,128,remaining-1,4,0)
    assert (next_target,rollback)==((30,1) if remaining>1 else (20,0))
for code, advance, count, hits, end in data['timingCases']:
    row = next(a for a in animations if a['code']==code)
    first = max(1,(row['event']*256+advance-1)//advance)
    duration = max(2,(row['frames']*256+advance-1)//advance)
    assert hits == [first*(i+1) for i in range(count)] and end == first*(count-1)+duration
evidence = {'sourceCommit':'0c0bbd1e6bef9181a3da1c046523af622a925416', 'sourceHashes':{str(p.relative_to(ROOT)):h for p,h in sources.values()},'animations':animations,'probe':data,
    'limits':['Supplementary legacy engine reconstruction; modern executable equivalence is unverified.','Legacy AnimData acquisition provenance is missing.','World/aura eligibility, damage allocation, AR/block, source formulas, durability and mana lifecycle are explicit stubs.','Only A1 single-event Paladin melee animations are exercised; transformed forms and sequence animations are excluded.','The original rollback reads frameFlags[-1]; the probe reproduces the preceding zero high byte of the animation speed field.']}
(STAGE/'zeal-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
(ROOT/'tools/reference/zeal-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Zeal original-source probes pass: '+', '.join(f'{len(v)} {k}' for k,v in data.items()))
