#!/usr/bin/env python3
"""Compose unchanged Blade Fury channel, generic wrappers and actual mana bodies.

Learned player context only. World/animation services remain explicit. The old
rank-one consumer disagrees with current tables and is reported, never adopted.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_blade_fury_channel_reference.py'), '--check'],
                   check=True, stdout=subprocess.DEVNULL)
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, routines = {}, {}

    def extract(rel, name):
        raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            x['sha'] for x in tree['tree'] if x['path'] == rel)
        hashes[rel] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    # Reuse only the declared ABI; inherited compiled bodies are checked below.
    base = (STAGE / 'blade-fury-channel-source-probe.cpp').read_text().split('int main()', 1)[0]
    for name in ['SKILLS_GetProgressiveSkillMissileId', 'SKILLS_EvaluateProgressiveSkillCalc',
                 'SKILLS_SrvDo048_BladeFury', 'SKILLS_SrvSt26_BladeFury',
                 'SKILLS_StatRemoveCallback_RemoveState']:
        assert extract('source/D2Game/src/SKILLS/SkillAss.cpp', name).strip() in base

    def replace(old, new):
        nonlocal base
        assert old in base, old
        base = base.replace(old, new, 1)

    base = '#include <iterator>\n#include <string>\n' + base
    replace('unsigned dwFlags=64;}owner,created;',
            'unsigned dwFlags=64,dwSeqFrame=0;void* pDynamicPath=nullptr;}owner,created;')
    replace('struct D2SkillStrc{int param=0;}', 'struct D2SkillStrc{int param=0,nCharges=0;}')
    replace('wStartMana=6;}row;', 'wStartMana=6,wMana=8,wLevelMana=1,nManaShift=5,wMinMana=0;'
            'int wSrvStartFunc=26,wSrvDoFunc=48,nLineOfSight=0,wSrvMissile=-1,wItemEffect=0,dwDelay=-1,nAnim=18;}row;')
    # The initial ABI has wSrvMissile=0 in its selector row; generic wrappers
    # require the actual pinned absent direct-missile column (-1).
    replace('wSrvMissile=0,', '')
    replace('nItemStatCostTxtRecordCount=512;}tables;',
            'nItemStatCostTxtRecordCount=512,nMissilesTxtRecordCount=1000,pMissilesTxt[1000]={},nShiftedStuff=63,nStuff=6;}tables;')
    replace('int nSkill=0,nMissile=0,nSkillLevel=0,nTargetX=0,nTargetY=0;',
            'int nSkill=0,nMissile=0,nSkillLevel=0,nTargetX=0,nTargetY=0,nX=0,nY=0;')
    replace('int mana=0,calc=5,', 'int rank=3;std::string order;int mana=0,calc=5,')
    replace('assert(formula==0&&id==266&&rank==3);return calc;',
            'assert(id==266&&rank==::rank);return formula==0?calc:0;')
    replace('&&p->nSkillLevel==3&&', '&&p->nSkillLevel==rank&&')
    replace('emits++;if(factory)', "emits++;order+='E';if(factory)")
    replace('void D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int id,int rank){assert(id==266&&rank==3);payCalls++;if(mana>=320){mana-=320;paid++;}}',
            'int32_t D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int,int);')
    base += r'''
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,STATE_BLOOD_MANA=2,STAT_ITEM_CHARGED_SKILL=1;
constexpr int SKILLSFLAGINDEX_INTOWN=1,SKILLSFLAGINDEX_PERIODIC=2,SKILLSFLAGINDEX_LOB=3,SKILLSFLAGINDEX_DECQUANT=4,SKILLSFLAGINDEX2_USEMANAONDO=5;
constexpr int SKILLSLOS_NONE=0,SKILLSLOS_MISSILE_BARRIER=1,SKILLSLOS_PLAYER_PATH=2,SKILLSLOS_PLAYER_MONSTER=3,SKILLSLOS_PLAYER_FLYING=4,SKILLSLOS_RADIAL_BARRIER=5;
constexpr int COLLIDE_MISSILE_BARRIER=1,COLLIDE_MASK_PLAYER_PATH=2,COLLIDE_PLAYER=4,COLLIDE_MONSTER=8,COLLIDE_DOOR=16,COLLIDE_WALL=32;
constexpr int PLRMODE_TOWNNEUTRAL=1,PLRMODE_SEQUENCE=18,EVENTTYPE_PERIODICSKILLS=2;
struct D2ActiveRoomStrc{}room;bool inTown=false;
using SkillStartFunc=int32_t(*)(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);using SkillDoFunc=SkillStartFunc;
SkillStartFunc gpSkillSrvStartFnTable_6FD408B0[70]={};SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[160]={};
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return inTown;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){assert(false);}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){assert(false);}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return learned?&skill:nullptr;}
int SKILLS_GetFlags(D2SkillStrc*){return 0;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return 266;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return rank;}
int SKILLS_GetOwnerGUIDFromSkill(D2SkillStrc*){return -1;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return record?&row:nullptr;}
bool STATES_IsUnitShapeShifted(D2UnitStrc*){assert(false);return false;}
int D2GAME_SKILLS_BloodMana_6FD025E0(D2UnitStrc*,int){assert(false);return 0;}
void STATLIST_AddUnitStat(D2UnitStrc*,int stat,int amount,int){assert(stat==STAT_MANA);mana+=amount;paid++;order+='M';}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,int){assert(false);return nullptr;}
int ITEMMODS_GetItemCharges(D2UnitStrc*,int,int,int*,D2StatListStrc**){assert(false);return 0;}
void STATLIST_SetBaseStat2(D2StatListStrc*,int,int,int,D2UnitStrc*){assert(false);}
void* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){assert(false);return nullptr;}
void D2GAME_PACKETS_SendPacket0x3E_6FC3EC20(void*,D2UnitStrc*,int,int,int,int){assert(false);}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){assert(false);return nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc*){assert(false);return 0;}int CLIENTS_GetUnitY(D2UnitStrc*){assert(false);return 0;}
int D2COMMON_10175_PathGetFirstPointX(void*){assert(false);return 0;}int D2COMMON_10176_PathGetFirstPointY(void*){assert(false);return 0;}
int D2Common_11026(int,int,D2UnitStrc*,unsigned){assert(false);return 0;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){assert(false);}
void sub_6FD11420(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){assert(false);}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int){assert(false);}
'''
    # Blood Mana is a declared unexecuted branch, rather than conflating it with
    # the inferno state in the inherited state-query stub.
    replace('assert(s==STATE_INFERNO);return active;',
            'if(s==2){return false;}assert(s==STATE_INFERNO);return active;')
    for name in ['D2GAME_SKILLMANA_Consume_6FD10A50', 'sub_6FD10CE0',
                 'sub_6FD12950', 'D2GAME_SKILLS_Handler_6FD12BA0']:
        base += extract('source/D2Game/src/SKILLS/Skills.cpp', name)
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int level:{1,3,20})for(int before:{0,1})for(int next:{999,1000,1001})
for(int amount:{0,255,256,319,320,863,864,1535,1536,2048})
for(int later:{0,255,256,319,320,863,864,1535,1536,2048})for(int target:{0,1})for(int make:{0,1}){
 owner={};row={};skill={};state.until=1020;rank=level;active=before;skill.param=next;mana=amount;
 record=learned=allocation=true;aim=target;factory=make;calc=5;emits=made=paid=rewinds=allocs=0;order.clear();
 row.dwFlags[1]=gdwBitMasks[SKILLSFLAGINDEX2_USEMANAONDO];
 gpSkillSrvStartFnTable_6FD408B0[26]=SKILLS_SrvSt26_BladeFury;gpSkillSrvDoFnTable_6FD40A20[48]=SKILLS_SrvDo048_BladeFury;
 int start=sub_6FD12950(&game,&owner,266,level,0),startMana=mana,startEmit=emits,startPaid=paid;
 mana=later;int result=start?D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,266,level,1,0,0):-1;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,\"%s\"]",first?"":",",level,before,next,amount,later,target,make,start,startMana,startEmit,startPaid,result,mana,emits,made,paid,active,state.until,skill.param,rewinds,owner.dwFlags,order.c_str());first=false;
}printf("]\n");}
'''
    path = STAGE / 'blade-fury-resource-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'blade-fury-resource-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    rows = json.loads(run.stdout)
    assert len(rows) == 7200
    for (level, before, deadline, amount, later, target, make, start, start_mana, start_emit, start_paid,
         result, left, emits, made, paid, active, until, next_shot, rewinds, flags, order) in rows:
        current_cost = (8 + level - 1) * 32
        legacy_cost = current_cost if level > 1 else 0
        start_gate = amount >= current_cost
        attempt_start = bool(start_gate and before and deadline < 1000 and target)
        started = bool(start_gate and (before and (deadline > 1000 or attempt_start)
                                      or not before and amount >= 1536))
        after_active = bool(before or started)
        after_deadline = 1004 if attempt_start else 0 if started and not before else deadline
        do_gate = started and later >= current_cost
        attempt_do = bool(do_gate and target and after_deadline < 1000)
        count = int(attempt_start) + int(attempt_do)
        expect = (int(started), amount - int(attempt_start) * legacy_cost,
                  int(attempt_start), int(attempt_start), int(attempt_do) if started else -1,
                  later - int(attempt_do) * legacy_cost, count, count * make, count,
                  int(after_active), 1007 if start_gate and before else 1021 if started else 1020,
                  1004 if count else after_deadline,
                  int(start_gate and before and (deadline > 1000 or target)) + int(do_gate and target and after_active),
                  0 if start_gate and before and deadline <= 1000 or do_gate else 64,
                  ('EM' if attempt_start else '') + ('EM' if attempt_do else ''))
        assert (start, start_mana, start_emit, start_paid, result, left, emits, made, paid,
                active, until, next_shot, rewinds, flags, order) == expect, (rows.index([
                    level, before, deadline, amount, later, target, make, start, start_mana,
                    start_emit, start_paid, result, left, emits, made, paid, active, until,
                    next_shot, rewinds, flags, order]), expect)
    output = STAGE / 'blade-fury-resource-source-probe.json'
    output.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'],
                    nativeVerifiedByThisSourceProbe=False, sourceHashes=hashes,
                    compiledRoutineHashes=routines,
                    sharedAbiEvidence='tools/reference/blade-fury-channel-source-evidence.json',
                    fixture=dict(path=str(output.relative_to(ROOT)), cases=len(rows),
                                 sha256=hashlib.sha256(output.read_bytes()).hexdigest()),
                    established=[
                        'Complete generic start/do wrappers, st26/do48 and actual ordinary-player mana eligibility/consumer compose. Fresh state checks six-mana availability; usemanaondo prevents generic start debit. Existing-state start can itself emit and debit before a do event.',
                        'Generic do rejects insufficient current-table mana before do48. A same-frame start emission sets a future deadline, preventing a duplicate do-event shot/payment. Failed missile allocation still counts as an attempted shot and consumes mana under these callers.',
                        'The complete old consumer debits zero at effective rank1, while its eligibility still requires one mana from the pinned table. Ranks3 and20 debit320 and864FP. This rank1 disagreement is explicit, not a current-build certificate or approved free-cast policy.'
                    ], limits=[
                        'Ordinary learned player, rank1/3/20, valid room/nontown, no charges/Blood Mana/LoS/delay/quantity. Unexecuted special branches assert; their presence in compiled bodies is not verification.',
                        'Start followed by a same-frame do with declared intervening mana value is an orchestration control, not full production held-input/SQ scheduling. Animation rewind, aim, stat/event storage and missile allocation remain explicit services.',
                        'Current table resource policy must override the legacy rank1 debit discrepancy. Modern warmup/cadence, source SQ23/rewind, native player input, weapon/missile/persistence remain pending.'
                    ], sanitizers='ASan/UBSan; exit0 and empty runtime stderr')
    target = ROOT / 'tools/reference/blade-fury-resource-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('7200 complete Blade Fury start/do/channel/mana compositions pass; old rank1 zero-debit disagreement remains explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
