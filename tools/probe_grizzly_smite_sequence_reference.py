#!/usr/bin/env python3
"""Compose complete BearSmite preparation/release with the monster sequence clock."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    parent_path=ROOT/'tools/reference/bear-smite-engine-evidence.json'
    parent=json.loads(parent_path.read_text())
    original=BASE/'bear-smite-source-probe.cpp'
    assert sha(original)==parent['fixture']['generatedSourceSha256']
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']==parent['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes=dict(parent['sourceHashes']);routines=dict(parent['unchangedRoutineHashes'])
    for relative,digest in hashes.items():
        assert sha(BASE/'d2moo-pinned-source'/relative)==digest,relative

    def extract(relative,name):
        raw=(BASE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(
            x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest()
        body=function(raw.decode(),name);routines[name]=hashlib.sha256(body.encode()).hexdigest()
        return body

    mode='source/D2Game/src/MONSTER/MonsterMode.cpp'
    mode_text=(BASE/'d2moo-pinned-source'/mode).read_text()
    table=mode_text.split('constexpr D2MonModeInfoTableStrc stru_6FD28738[16] =',1)[1].split('};',1)[0]
    assert re.findall(r'\{\s*(\d),\s*(\d),\s*(\d)\s*\}',table)[14]==('1','0','0')
    preflight_path=ROOT/'tools/reference/grizzly-preflight.json'
    preflight=json.loads(preflight_path.read_text())
    assert len(preflight['sequence']['rows'])==17
    assert [int(r['event'] or 0) for r in preflight['sequence']['rows']]==[0]*8+[1]+[0]*8
    cpp=original.read_text().split('bool first=true;\nvoid emit(',1)[0]
    old='struct D2CombatStrc;'
    assert cpp.count(old)==1
    cpp=cpp.replace(old,'struct D2AnimSeqTxt{unsigned nMode,nFrame;int nDir,nEvent;};D2AnimSeqTxt rows[17];\n'+old,1)
    old='uint32_t dwFlags=0;D2GameStrc*pGame='
    assert cpp.count(old)==1
    cpp=cpp.replace(old,'D2AnimSeqTxt*pAnimSeq=nullptr;unsigned dwSeqFrameCount=0,dwSeqFrame=0,dwSeqSpeed=0,dwSeqMode=0;int dwFrameCountPrecise=0,nSeqCurrentFramePrecise=0,nActionFrame=0,wAnimSpeed=0;uint32_t dwFlags=0;D2GameStrc*pGame=',1)
    cpp+=r'''
using D2AnimSeqEvent=int;constexpr int ANIMSEQ_EVENT_NONE=0,UNITFLAG_SQGFXCHANGE=32;
D2AnimSeqTxt*DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){return rows;}
int DATATBLS_GetSeqFramePointsCount(D2UnitStrc*){return 17*256;}
int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 17;}
void UNITS_SetAnimActionFrame(D2UnitStrc*,int){assert(false);}
int UNITS_GetFrameBonus(D2UnitStrc*){assert(false);return 0;}
'''
    seq='source/D2Common/src/DataTbls/SequenceTbls.cpp'
    units='source/D2Common/src/Units/Units.cpp'
    for relative,name in [(seq,'DATATBLS_ComputeSequenceAnimation'),(seq,'DATATBLS_GetSequenceEvent'),
                          (units,'UNITS_InitializeSequence'),(units,'UNITS_StopSequence'),(units,'UNITS_IsAtEndOfFrameCycle')]:
        cpp+='\n'+extract(relative,name)+'\n'
    cpp+=r'''
struct D2ModeChangeStrc{D2UnitStrc*pUnit;};struct D2SkillStrc{int flags=0;}usedSkill;
int dispatches=0,ends=0;bool perFrame=false;
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc*u,int mode){u->pCombat=nullptr;u->dwAnimMode=mode;UNITS_InitializeSequence(u);}
int sub_6FD12FD0(D2GameStrc*g,D2UnitStrc*u){return SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite(g,u,326,npcRank);}
void sub_6FC63680(D2GameStrc*,D2UnitStrc*u){ends++;u->dwAnimMode=1;u->pCombat=nullptr;perFrame=false;}
D2SkillStrc*UNITS_GetUsedSkill(D2UnitStrc*){return &usedSkill;}
int SKILLS_GetFlags(D2SkillStrc*s){return s->flags;}
void SKILLS_SetFlags(D2SkillStrc*s,int flags){s->flags=flags;}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){assert(false);}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
void sub_6FD13410(D2GameStrc*g,D2UnitStrc*u){dispatches++;SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws(g,u,326,npcRank+1);}
'''
    for name in ['sub_6FC64E60','sub_6FC64E90']:
        cpp+='\n'+extract(mode,name)+'\n'
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;for(int i=0;i<17;i++)rows[i]={5u,unsigned(i),0,i==8?1:0};
printf("[");bool first=true;
for(int rank:{1,49})for(int percent:{25,295})for(int pair:{0,1})for(int speed:{124,248,256,434})for(int control=0;control<9;control++)for(int resist:{0,33}){
 game={};attacker={};defender={};allocated={};skill={};usedSkill={};npcRank=rank;resultFlags=1;
 attacker.dwUnitType=defender.dwUnitType=UNIT_MONSTER;attacker.dwClassId=428;defender.dwClassId=28;attacker.dwUnitId=17;defender.dwUnitId=29;
 attacker.pGame=defender.pGame=&game;attacker.pInventory=defender.pInventory=nullptr;
 attacker.stats[STAT_MINDAMAGE]=pair?496:30;attacker.stats[STAT_MAXDAMAGE]=pair?526:60;attacker.stats[STAT_DAMAGEPERCENT]=percent;
 attacker.stats[STAT_HITPOINTS]=defender.stats[STAT_HITPOINTS]=10000000;
 for(int stat:{STAT_DAMAGERESIST,STAT_FIRERESIST,STAT_LIGHTRESIST,STAT_COLDRESIST})defender.stats[stat]=resist;
 high=false;criticalRoll=5;draws=events=modeCalls=thornsCalls=freeCalls=dispatches=ends=0;rangeBonus=-1;dispatched={};bounds.clear();trace.clear();
 hasRow=hasTarget=hasRoom=interact=inRange=true;town=false;perFrame=true;
 D2ModeChangeStrc change{&attacker};assert(sub_6FC64E60(&game,&change)==1);attacker.dwSeqSpeed=speed;
 const int cachedTotal=attacker.pCombat->tDamage.dwDmgTotal,cachedPhysical=attacker.pCombat->tDamage.dwPhysDamage;
 assert(defender.stats[STAT_HITPOINTS]==10000000);
 for(int frame=0;frame<=40;frame++){
  game.dwGameFrame=1000+frame;
  if(frame==4){
   if(control==1)inRange=false;if(control==2)hasTarget=false;if(control==3)hasRoom=false;if(control==4)interact=false;
   if(control==5){attacker.stats[STAT_DAMAGEPERCENT]=900;defender.stats[STAT_DAMAGERESIST]=0;}
   if(control==6){attacker.dwAnimMode=1;attacker.pCombat=nullptr;perFrame=false;}
   if(control==7)defender.stats[STAT_HITPOINTS]=100*256;
   if(control==8)hasRow=false;
  }
  if(frame>0&&perFrame)sub_6FC64E90(&game,&attacker);
  bool active=attacker.dwAnimMode==14;
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,percent,pair,speed,control,resist,frame,attacker.dwAnimMode,active?attacker.dwSeqFrame:0,active?attacker.dwFrameCountPrecise:0,active?attacker.nActionFrame:0,(attacker.dwFlags&64)!=0,dispatches,ends,defender.stats[STAT_HITPOINTS],draws,cachedTotal,cachedPhysical,rangeBonus,modeCalls,freeCalls);first=false;
 }
}printf("]\n");}
'''
    generated=BASE/'grizzly-smite-sequence-source-probe.cpp';generated.write_text(cpp)
    with (BASE/'grizzly-smite-sequence-compiler.log').open('wb') as log:
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',
                        str(generated),'-o',str(generated.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    result=subprocess.run([str(generated.with_suffix(''))],capture_output=True)
    (BASE/'grizzly-smite-sequence-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode==0 and not result.stderr,result.stderr.decode()[-2000:]
    comparisons=json.loads(result.stdout);assert len(comparisons)==576*41
    fixture=BASE/'grizzly-smite-sequence-source-probe.json';fixture.write_text(json.dumps(comparisons,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],installed=False,handlerEnabled=False,nativeConsumer=False,
        preflightSha256=sha(preflight_path),reusedAbiSha256=sha(original),
        reusedEvidence=dict(path=str(parent_path.relative_to(ROOT)),sha256=sha(parent_path)),
        sourceHashes=hashes,unchangedRoutineHashes=routines,
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture),contexts=576,phases=len(comparisons),
            generatedSourceSha256=sha(generated),sanitizers='ASan+UBSan'),
        established=[
            'Complete mode14entry clears server-do and invokes complete start32/allocation/fill/mitigation before per-frame callbacks. Complete mode14callback consumes the existing sequence action before complete advance. Complete do2/durability/event-health executes at release with the cached packet.',
            '576contexts cover innate ranks1/49,copied percentages25/295,two physical endpoint pairs,supplied speeds124/248/256/434,resistance0/33and nine transition controls. Range loss,target/room/interaction loss,changed release stats,cancellation,changed target HP and missing release row stay distinct. Cancellation is a declared driver boundary,not an executed full mode-change routine.',
            'Header-backed mode-info14selects infinite per-frame callbacks. Sparse initial animation scheduling is not used as the BearSmite action clock. Current17A2rows have action1at index8. Health is unchanged at entry;start RNG draws are not repeated by release.',
        ],limits=[
            'The outer per-frame driver,end/neutral callback,used-skill flags0,base-speed supply and mode-entry/free-combat services are explicit. The full mode changer,global queue/AI ordering and current executable are not executed. Unreachable missing-row/HP controls are not modern gameplay certificates.',
            'Final mode/death/knockback geometry,Thorns/global callbacks and cold receiver retain the parent recording-service limits. Native sequence/release/world/save cancellation and learned casting require acceptance.',
        ])
    path=ROOT/'tools/reference/grizzly-smite-sequence-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print('Grizzly BearSmite sequence:576complete composed contexts/23616phases;ASan+UBSan clean;native action pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    build(parser.parse_args().check)
