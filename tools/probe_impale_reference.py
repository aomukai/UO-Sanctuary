#!/usr/bin/env python3
"""Compile unchanged Impale start, durability eligibility and SQ8 scheduling."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes,blocks={},{}
    def source(path):
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def routine(text,name):
        match=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert match,name
        end,depth=text.index('{',match.start())+1,1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        blocks[name]=text[match.start():end];return blocks[name]+'\n'
    ama=source('source/D2Game/src/SKILLS/SkillAma.cpp')
    items=source('source/D2Common/src/Items/Items.cpp')
    seq=source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
    units=source('source/D2Game/src/UNIT/SUnit.cpp')
    source('source/D2Game/src/UNIT/SUnitDmg.cpp') # reviewed flag1/ordinary wear consumer
    sequences={}
    for family,name,size,event in [('1HT','Impale_BOW',21,13),('2HT','Impale_1HS',24,15)]:
        body=re.search(r'D2AnimSeqTxt gPlayerSequence'+name+r'\['+str(size)+r'\] =\s*\{.*?\n\};',seq,re.S).group(0)
        entries=re.findall(r'\{\s*0,\s*(PLRMODE_\w+),\s*(\d+),\s*0,\s*(ANIMSEQ_EVENT_\w+)\s*\}',body)
        assert len(entries)==size and [i for i,x in enumerate(entries) if x[2]=='ANIMSEQ_EVENT_MELEE_ATTACK']==[event]
        sequences[family]=dict(symbol='gPlayerSequence'+name,points=size,event=event,body=body)
    assert re.search(r'gPlayerWeaponsSequenceImpale\s*=\s*\{\s*gPlayerSequenceHandToHand1, 13, 13,\s*gPlayerSequenceImpale_BOW, 21, 21,\s*gPlayerSequenceImpale_1HS, 24, 24,',seq)
    cpp=r'''#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <initializer_list>
#define __fastcall
#define __stdcall
using BOOL=int;constexpr bool FALSE=false;
constexpr int UNIT_ITEM=4,DAMAGERESULTFLAG_SUCCESSFULHIT=1,DAMAGEHITFLAG_1=1,ITEMTYPE_WEAPON=1,STAT_DURABILITY=72,STAT_QUANTITY=70,STAT_ITEM_INDESCTRUCTIBLE=152;
constexpr int PLRMODE_ATTACK1=4,PLRMODE_ATTACK2=5,ANIMSEQ_EVENT_NONE=0,ANIMSEQ_EVENT_MELEE_ATTACK=1,ANIMSEQ_EVENT_PLAY_SOUND=3,EVENTTYPE_MODECHANGE=8,EVENTTYPE_ENDANIM=9;
struct D2GameStrc{int dwGameFrame=100;} game;
struct D2AnimSeqTxt{int id,mode,frame,dir,nEvent;};struct Anim{int pFrameFlags[144]={};} anim;
struct D2UnitStrc{int dwUnitType=0,dwClassId=0,hp=2,maxhp=2,qty=3,indestructible=0,dwSeqSpeed=256,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=0,nSeqCurrentFramePrecise=0;void* pSeed=nullptr;void* pAnimSeq=nullptr;Anim* pAnimData=&anim;} owner,target,item;
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},nEType=0,nSrcDam=128;} row;
struct D2ItemsTxt{int nNoDurability=0,nDurability=1;} itemRow;
struct D2DamageStrc{int wResultFlags=0,dwPhysDamage=0,dwConvPct=0,nConvType=0,dwHitFlags=0;} saved;
bool present=true,selected=true,reachable=true,hit=true;int kind=7,chance=46,wearRoll=0,allocs=0,elements=0,broken=0,consumed=0;
std::vector<std::array<int,4>> scheduled;std::vector<int> order;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return present?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return reachable;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 0;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){return hit?1:0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int rank){return calc==0?300+25*(rank-1):calc==1?chance:calc==2?1:0;}
int SUNITDMG_ApplyDamageBonuses(D2UnitStrc*,int,D2UnitStrc*,int,int,int bonus,int,uint8_t src){if(src!=128)std::abort();order.push_back(1);return 1000+bonus;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){order.push_back(2);elements++;}
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return kind==0?nullptr:&item;}
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int){return kind==2?nullptr:&itemRow;}
int STATLIST_GetMaxDurabilityFromUnit(D2UnitStrc* p){return p->maxhp;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_DURABILITY?p->hp:stat==STAT_QUANTITY?p->qty:p->indestructible;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){return true;}
bool ITEMS_CheckIfStackable(D2UnitStrc*){return kind==8;}
unsigned ITEMS_RollRandomNumber(void*){return wearRoll;}
void sub_6FD118C0(D2GameStrc*,D2UnitStrc*){order.push_back(3);consumed++;item.qty--;}
void sub_6FC4B580(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p){order.push_back(4);broken++;p->hp=0;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int,int value,int){order.push_back(5);p->hp=value;}
void* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){return nullptr;}
void D2GAME_PACKETS_SendPacket0x3E_6FC3EC20(void*,D2UnitStrc*,int,int,int,int){}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int src){if(src!=128)std::abort();order.push_back(6);allocs++;saved=*p;}
int UNITS_GetFrameBonus(D2UnitStrc*){return 0;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int arg,int ordinal){scheduled.push_back({type,frame,arg,ordinal});}
'''
    for text,name in [(items,'ITEMS_HasDurability'),(ama,'SKILLS_SrvSt07_Impale'),(seq,'DATATBLS_GetSequenceEvent'),(units,'sub_6FCBCE70')]:cpp+=routine(text,name)
    for row in sequences.values():cpp+=row['body']+'\n'
    cpp+=r'''void emit(bool& first,int record,int chosen,int range,int success,int type,int hp,int qty,int percent,int roll,int rank){
present=record;selected=chosen;reachable=range;hit=success;kind=type;chance=percent;wearRoll=roll;allocs=elements=broken=consumed=0;saved={};order.clear();
item.dwUnitType=kind==1?1:UNIT_ITEM;item.hp=hp;item.qty=qty;item.maxhp=kind==5?0:5;item.indestructible=kind==6?1:0;itemRow.nNoDurability=kind==3;itemRow.nDurability=kind==4?0:1;
int result=SKILLS_SrvSt07_Impale(&game,&owner,19,rank);
if(!first)std::printf(",");first=false;std::printf("{\"input\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"output\":[%d,%d,%d,%d,%d,%d,%d,%d,%d],\"order\":[",record,chosen,range,success,type,hp,qty,percent,roll,rank,result,allocs,elements,saved.dwPhysDamage,saved.dwHitFlags,item.hp,item.qty,broken,consumed);
for(size_t i=0;i<order.size();i++){if(i)std::printf(",");std::printf("%d",order[i]);}std::printf("]}");}
int main(){bool first=true;std::printf("{\"guards\":[");
for(int record:{0,1})for(int chosen:{0,1})for(int range:{0,1})for(int success:{0,1})emit(first,record,chosen,range,success,7,2,3,100,0,1);
std::printf("],\"wear\":[");first=true;
for(int type:{0,1,2,3,4,5,6,7,8})for(int hp:{1,2})for(int qty:{0,1,3})for(int percent:{0,25,46,100})for(int roll:{0,24,25,45,46,99})for(int rank:{1,20})emit(first,1,1,1,1,type,hp,qty,percent,roll,rank);
std::printf("],\"schedules\":[");first=true;
for(int family:{1,2})for(int advance:{38,128,179,256,320,448}){
owner.pAnimSeq=family==1?gPlayerSequenceImpale_BOW:gPlayerSequenceImpale_1HS;owner.dwSeqFrameCount=(family==1?21:24)*256;owner.dwSeqSpeed=advance;scheduled.clear();sub_6FCBCE70(&game,&owner);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,[",family,advance);bool inner=true;
for(auto e:scheduled){if(e[0]!=8 || e[2]!=1)continue;if(!inner)std::printf(",");inner=false;std::printf("[%d,%d]",e[1]-100,e[3]);}std::printf("],%d]",scheduled.back()[1]-100);
}std::printf("]}\n");}
'''
    cpp_path=STAGE/'impale-source-probe.cpp';cpp_path.write_text(cpp);binary=STAGE/'impale-source-probe'
    subprocess.run(['g++','-std=c++20','-O2',str(cpp_path),'-o',str(binary)],check=True)
    data=json.loads(subprocess.check_output([str(binary)]));assert len(data['guards'])==16 and len(data['wear'])==2592 and len(data['schedules'])==12
    for case in data['guards']+data['wear']:
        record,chosen,reach,hit,kind,hp,qty,chance,roll,rank=case['input'];valid=record and chosen and reach
        wear=valid and hit and kind in (7,8) and roll<chance
        consume=wear and kind==8 and qty>0;loss=wear and kind==7
        assert case['output']==[int(valid),int(valid),int(valid and hit),1000+300+25*(rank-1) if valid and hit else 0,int(valid and hit),max(0,hp-1) if loss else hp,qty-int(consume),int(loss and hp==1),int(consume)]
        order=case['order']
        if valid:
            assert order[-1]==6
            if hit:assert order[:2]==[1,2]
            if loss or consume:assert len(order)==4 and order[2] in (3,4,5)
    for family,advance,events,end in data['schedules']:
        seq=sequences['1HT' if family==1 else '2HT'];assert events==[[(seq['event']*256+advance-1)//advance,0]] and end==(seq['points']*256+advance-1)//advance
    fixture=STAGE/'impale-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},
        probe=dict(guardCases=16,specialWearCases=2592,scheduleCases=12,fixture=str(fixture.relative_to(ROOT))),
        sequences={k:{a:b for a,b in v.items() if a!='body'} for k,v in sequences.items()},speedEvidence='tools/reference/weapon-sequence-rate-evidence.json',
        established=[
            'Unchanged start7 has table/target/reach guards. Successful declared hit precomputes physical with calc1, calls elemental service, sets hitflag1, performs special wear, then allocates combat128. A miss allocates an empty packet and does not perform special wear.',
            'Compiled ITEMS_HasDurability rejects wrong unit kind, absent item row, NoDurability, zero base/max durability and indestructible items. Start7 special wear consumes one positive stack quantity or subtracts nonstack durability1/breaks at zero on its independent chance roll.',
            'SQ8 selector maps1HT to21points/event13,2HT to24points/event15;12 unchanged scheduler cases retain event ordinal0 and ceiling deadlines at declared speeds/frame bonus0.',
            'Reviewed FillDamageValues hitflag1 bypasses ordinary physical recomputation and mastery/Critical/Deadly Strike. Reviewed shared do2 applies target state before ordinary drain, and successful drain still calls ordinary weapon wear after the separate start-time opportunity.'
        ],limits=[
            'Hit, reach, damage/formula, stack-consumption and break services are declared to isolate control flow. Physical return1000+ED is a sentinel, not a damage formula; chance percentages are declared boundary inputs, not a compiled diminishing curve. Source-only proof enables no Impale native handler.',
            'Current always-hit/uninterruptibility/slow and additive30 rate correction supersede old numeric/flag assumptions. Modern precomputed-physical/no-critical/species-ED, break/last-stack effect on allocation, full wear/packet/event/owner/client acceptance remain explicit.',
            'Scheduled rates are declared; modern rate-layer corroboration is in separate evidence. Cached sequence geometry and native IAS/WSM/mode timing require native integration and current executable comparison.'
        ])
    target=ROOT/'tools/reference/impale-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
    if check:assert target.read_text()==rendered,'Impale evidence changed'
    else:target.write_text(rendered)
    print('Impale:16 start guards,2592 special wear cases and12 SQ8 schedules compiled; native handler pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
