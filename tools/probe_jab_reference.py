#!/usr/bin/env python3
"""Compile unchanged pinned Jab start/strike bodies and sequence scheduling."""
from pathlib import Path
import argparse, hashlib, json, re, subprocess
ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'

def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}
    def source(path):
        raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
        expected = next(row['sha'] for row in tree['tree'] if row['path'] == path)
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == expected
        hashes[path] = hashlib.sha256(raw).hexdigest()
        return raw.decode()
    def routine(text, name):
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        blocks[name] = text[match.start():end]
        return blocks[name] + '\n'
    ama = source('source/D2Game/src/SKILLS/SkillAma.cpp')
    seq = source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
    units = source('source/D2Game/src/UNIT/SUnit.cpp')
    skills = source('source/D2Game/src/SKILLS/Skills.cpp')
    items = source('source/D2Game/src/ITEMS/Items.cpp')
    sequences = {}
    for name, count, weapon in [('Jab_BOW',18,'1HT'), ('Jab_1HS',21,'2HT')]:
        body = re.search(r'D2AnimSeqTxt gPlayerSequence' + name + r'\[' + str(count) + r'\] =\s*\{.*?\n\};', seq, re.S).group(0)
        entries = re.findall(r'\{\s*0,\s*(PLRMODE_\w+),\s*(\d+),\s*0,\s*(ANIMSEQ_EVENT_\w+)\s*\}', body)
        events = [i for i, row in enumerate(entries) if row[2] == 'ANIMSEQ_EVENT_MELEE_ATTACK']
        assert events == ([3,9,15] if count == 18 else [3,10,17])
        sequences[weapon] = dict(symbol='gPlayerSequence'+name, points=count, strikePoints=events, body=body)
    # These source symbol suffixes are misleading: the actual selector order is HTH,1HT,2HT.
    assert re.search(r'gPlayerWeaponsSequenceJab\s*=\s*\{\s*gPlayerSequenceHandToHand1, 13, 13,\s*gPlayerSequenceJab_BOW, 18, 18,\s*gPlayerSequenceJab_1HS, 21, 21,', seq)
    cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <array>
#include <initializer_list>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,DAMAGERESULTFLAG_SUCCESSFULHIT=1,PLRMODE_ATTACK1=4,PLRMODE_ATTACK2=5;
constexpr int ANIMSEQ_EVENT_NONE=0,ANIMSEQ_EVENT_MELEE_ATTACK=1,ANIMSEQ_EVENT_PLAY_SOUND=3,EVENTTYPE_MODECHANGE=8,EVENTTYPE_ENDANIM=9;
struct D2GameStrc{int dwGameFrame=100;bool bExpansion=true;} game;
struct D2AnimSeqTxt{int id,mode,frame,dir,nEvent;};
struct Anim{int pFrameFlags[144]={};} anim;
struct D2UnitStrc{int hp=2,maxhp=2,qty=3;bool durable=true,throwable=false;int dwUnitType=0,dwSeqSpeed=256,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=0,nSeqCurrentFramePrecise=0;void* pSeed=nullptr;void* pAnimSeq=nullptr;Anim* pAnimData=&anim;} owner,target;
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},nEType=0,nSrcDam=128;} row;
struct D2DamageStrc{int wResultFlags=0,dwEnDmgPct=0,dwConvPct=0,nConvType=0;};
bool record=true,selected=true,hit=true;int allocation=0,drain=0,elements=0,lastDamage=0,lastSrc=0,lastAr=0,monsterCalls=0;
std::vector<std::array<int,4>> scheduled;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int rank){return 10+9*(rank-1);}
void sub_6FC627B0(D2UnitStrc*,int n){if(n!=8)std::abort();monsterCalls++;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){lastAr=ar;return hit?1:0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int rank){if(calc!=0)std::abort();return -15+3*(rank-1);}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){elements++;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* d,int src){allocation++;lastDamage=d->dwEnDmgPct;lastSrc=src;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int n){if(n!=0)std::abort();drain++;}
int UNITS_GetFrameBonus(D2UnitStrc*){return 0;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int arg,int ordinal){scheduled.push_back({type,frame,arg,ordinal});}
'''
    cpp += r"""
constexpr int ITEMTYPE_ANY_ARMOR=2,ITEMTYPE_WEAPON=1,STAT_DURABILITY=72,STAT_QUANTITY=70,STAT_ITEM_REPLENISH_DURABILITY=252,EVENTTYPE_STATREGEN=10,IFLAG_BROKEN=256;
int wearRandom=0;
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int type){return type==ITEMTYPE_WEAPON;}
bool ITEMS_HasDurability(D2UnitStrc* p){return p->durable;}
bool ITEMS_CheckIfThrowable(D2UnitStrc* p){return p->throwable;}
unsigned ITEMS_RollRandomNumber(void*){return wearRandom;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_DURABILITY?p->hp:stat==STAT_QUANTITY?p->qty:0;}
int STATLIST_GetMaxDurabilityFromUnit(D2UnitStrc* p){return p->maxhp;}
int EVENT_GetEventFrame(D2GameStrc*,D2UnitStrc*,int){return 0;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){if(stat==STAT_DURABILITY)p->hp=value;else if(stat==STAT_QUANTITY)p->qty=value;}
void* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){return nullptr;}
void D2GAME_PACKETS_SendPacket0x3E_6FC3EC20(void*,D2UnitStrc*,int,int,int,int){}
bool ITEMS_CheckItemFlag(D2UnitStrc*,int,int,const char*){return false;}
void sub_6FC4B580(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){std::abort();}
unsigned ITEMS_GetItemFormat(D2UnitStrc*){return 1;}
void sub_6FC80A30(D2GameStrc*,D2UnitStrc*){}
int ITEMS_GetTotalMaxStack(D2UnitStrc* p){return p->throwable?90:0;}
"""
    for text, name in [(ama,'SKILLS_SrvSt05_Jab'),(ama,'SKILLS_SrvDo007_Jab'),(seq,'DATATBLS_GetSequenceEvent'),(units,'sub_6FCBCE70'),(items,'ITEMS_UpdateDurability')]:
        cpp += routine(text, name)
    for row_data in sequences.values():
        cpp += row_data['body'] + '\n'
    cpp += r'''int main(){bool first=true;std::printf("{\"strikes\":[");
for(int present:{0,1})for(int targetPresent:{0,1})for(int monster:{0,1})for(int success:{0,1})for(int source:{0,64,128})for(int rank:{1,3,6,20,40,255}){
record=present;selected=targetPresent;hit=success;owner.dwUnitType=monster;row.nSrcDam=source;allocation=drain=elements=lastDamage=lastSrc=lastAr=monsterCalls=0;
int start=SKILLS_SrvSt05_Jab(&game,&owner,10,rank);int result=SKILLS_SrvDo007_Jab(&game,&owner,10,rank);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",present,targetPresent,monster,success,source,rank,start,result,allocation,drain,elements,lastDamage,lastSrc,lastAr,monsterCalls);
}std::printf("],\"schedules\":[");first=true;
for(int family:{1,2})for(int advance:{38,128,180,256,320,448}){
owner.pAnimSeq=family==1?gPlayerSequenceJab_BOW:gPlayerSequenceJab_1HS;owner.dwSeqFrameCount=(family==1?18:21)*256;owner.dwSeqSpeed=advance;scheduled.clear();sub_6FCBCE70(&game,&owner);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,[",family,advance);bool inner=true;for(auto e:scheduled){if(e[0]!=8 || e[2]!=1)continue;if(!inner)std::printf(",");inner=false;std::printf("[%d,%d]",e[1]-100,e[3]);}std::printf("],%d]",scheduled.back()[1]-100);
}std::printf("],\"wear\":[");first=true;
for(int stack:{0,1})for(int durable:{0,1})for(int hp:{1,2,5})for(int quantity:{0,1,3})for(int roll:{0,3,4,9,10,99}){
D2UnitStrc item;item.throwable=stack;item.durable=durable;item.hp=hp;item.maxhp=5;item.qty=quantity;wearRandom=roll;
ITEMS_UpdateDurability(&game,&owner,&item);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",stack,durable,hp,quantity,roll,item.hp,item.qty);
}std::printf("]}\n");}
'''
    path = STAGE / 'jab-source-probe.cpp'
    path.write_text(cpp)
    binary = STAGE / 'jab-source-probe'
    subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
    data = json.loads(subprocess.check_output([str(binary)]))
    for present,selected,monster,hit,src,rank,start,result,alloc,drain,element,damage,source_value,ar,monster_calls in data['strikes']:
        valid = present and selected
        assert (start,result,alloc,drain,element,damage,source_value,ar,monster_calls) == (int(valid),int(valid),int(valid),int(valid),int(valid and hit),-15+3*(rank-1) if valid and hit else 0,(src or 128) if valid else 0,10+9*(rank-1) if valid else 0,int(valid and monster))
    for family,advance,events,end in data['schedules']:
        row_data = sequences['1HT' if family==1 else '2HT']
        assert events == [[(point*256+advance-1)//advance,i] for i,point in enumerate(row_data['strikePoints'])]
        assert end == (row_data['points']*256+advance-1)//advance
    for stack,durable,hp,quantity,roll,after,qty in data['wear']:
        loss=bool(durable and roll<(10 if stack else 4))
        expected_hp=hp-1 if loss else hp
        expected_qty=quantity
        if loss and hp==1:
            expected_qty=max(0,quantity-1)
            expected_hp=5 if quantity>1 else 0
        assert (after,qty)==(expected_hp,expected_qty)
    data_path = STAGE / 'jab-source-probe.json'
    data_path.write_text(json.dumps(data,separators=(',',':'))+'\n')
    modern_path = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
    manifest = json.loads((modern_path.parent.parent / 'sources.json').read_text())
    assert hashlib.sha256(modern_path.read_bytes()).hexdigest() == manifest['files']['json/skills.json']['sha256']
    modern = next(v for v in json.loads(modern_path.read_text()).values() if v['skill']=='Jab')
    assert modern['srvstfunc']==5 and modern['srvdofunc']==7 and modern['seqnum']==1 and modern['itypea1']=='spea'
    assert modern['AttackNoMana']==1 and not modern.get('usemanaondo') and not modern.get('UseManaOnDo')
    assert modern['mana']==8 and modern['lvlmana']==1 and modern['manashift']==6 and modern['SrcDam']==128 and modern['ToHit']==10 and modern['LevToHit']==9 and modern['Param3']==-15 and modern['Param4']==3
    reviewed = {name:hashlib.sha256(routine(skills,name).encode()).hexdigest() for name in ['sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0']}
    evidence = dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,modernTableHash=hashlib.sha256(modern_path.read_bytes()).hexdigest(),compiledRoutines=list(blocks)[:5],reviewedOnlyPaymentWrappers=reviewed,probe=dict(strikeCases=len(data['strikes']),scheduleCases=len(data['schedules']),wearCases=len(data['wear']),dataPath=str(data_path.relative_to(ROOT))),sequences={key:{k:v for k,v in value.items() if k!='body'} for key,value in sequences.items()},rankGoldens=[dict(rank=rank,mana256=(8+rank-1)*64,attackPercent=10+9*(rank-1),physicalPercent=-15+3*(rank-1)) for rank in range(1,21)],officialGuide='https://classic.battle.net/diablo2exp/skills/amazon-javelin.shtml',currentTargetingEvidence='tools/reference/jab-current-targeting.json',established=[
        'Unchanged start5 only checks the skill table and selected target. Do7 reads current target and skill ToHit each event, adds calc1 only on successful hit, allocates with SrcDam128 fallback for zero, then invokes the ordinary durability drain. No target enumeration or retargeting occurs within either body.',
        'Source weapon selector maps 1HT to the misleading Jab_BOW symbol and2HT to Jab_1HS. Both have three events: points3/9/15 over18, or3/10/17 over21. Compiled scheduler preserves event ordinal0/1/2 across six declared sequence speeds with zero frame bonus.',
        'Reviewed start wrapper validates affordability, invokes start5 and consumes once after successful start. Reviewed do wrapper sees the nonnull start pointer and no usemanaondo flag, so these three do events do not consume again. Wrappers are hashed/reviewed only, not compiled in this fixture.',
        'Unchanged expansion ITEMS_UpdateDurability compiled with declared durability/stat/quantity/network services:4% ordinary weapons or10% throwable weapons; decrement durability, then consume one member/reset maximum on depletion, or zero durability/quantity for the last member. No maximum-durability loss. Native same-source owner RNG, repair/event/network integration still require acceptance.',
        'Pinned rank1..20 mana/AR/physical formulas agree with the official legacy table; the published decimal mana display truncates quarter points. Current data limits weapons to spea ancestry and sets AttackNoMana.'
    ],limits=[
        'This source proof alone is not a native/full modern executable certificate. Gameplay implementation and installed-build verification are recorded separately in jab-native-evidence.json.',
        'Declared hit/formula/target/allocation/durability services isolate source control flow; per-event modern target death/replacement, movement/range, weapon durability amount, IAS/Fanaticism/chill/animation and full owner/client lifecycle still need integration tests.',
        'Current mana payment, attack-without-mana fallback and uninterrupted sequence require native and current-executable acceptance separately; legacy guide and old source are not full proof of current executable behavior.',
        'No final effect or icon is created; existing separate240-entry asset registers retain Jab requirements.'
    ])
    out=ROOT/'tools/reference/jab-engine-evidence.json'
    encoded=json.dumps(evidence,indent=2)+'\n'
    if check:
        assert out.read_text()==encoded,'Jab source evidence is stale'
    else:out.write_text(encoded)
    print(f"Jab:{len(data['strikes'])} compiled start/strike cases,{len(data['schedules'])} three-event schedules and{len(data['wear'])} wear cases verified.")

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
