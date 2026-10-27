#!/usr/bin/env python3
"""Compose complete legacy form effect with outer start, payment and delay."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    prior_path = ROOT / 'tools/reference/druid-form-modes-engine-evidence.json'
    prior = json.loads(prior_path.read_text());original = BASE / 'druid-form-modes-source-probe.cpp'
    assert sha(original) == prior['generatedSourceSha256']
    cpp = original.read_text().split('bool first=true;\nvoid emit', 1)[0]
    for name, digest in prior['completeRoutineHashes'].items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == prior['legacyCommit']
    preflight = json.loads((ROOT / 'tools/reference/druid-forms-preflight.json').read_text())
    local = ROOT / '.local/research/d2r-3.3.93847' / preflight['localTables']['skills']['filename']
    assert sha(local) == preflight['localTables']['skills']['sha256']
    current = list(csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    forms = [row for row in current if row['skill'] in ['Wearwolf', 'Wearbear']]
    fields = ['srvstfunc', 'srvdofunc', 'usemanaondo', 'mana', 'lvlmana', 'minmana', 'manashift', 'globaldelay', 'localdelay', 'InTown', 'anim']
    assert len(forms) == 2
    for row in forms:
        assert [row[key] for key in fields] == ['', '116', '', '15', '0', '1', '8', '25', '', '1', 'SC']
    global_rows = [(row['skill'], row['globaldelay']) for row in current if row.get('globaldelay') not in ['', '0']]
    assert global_rows == [('Wearwolf', '25'), ('Wearbear', '25')]
    hashes, routines = {}, {}

    def source(name):
        path = BASE / 'd2moo-pinned-source' / name;raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        hashes[name] = sha(path)
        return raw.decode()

    def extract(name, symbol):
        body = function(source(name), symbol);routines[symbol] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def enum(name, symbol):
        match = re.search(r'enum ' + symbol + r'\s*\{.*?\};', source(name), re.S)
        assert match, symbol
        return match.group() + '\n'

    def replace(before, after):
        nonlocal cpp
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)

    replace('STATE_SKILLDELAY=121,', '')
    cpp = '#include <iterator>\n' + enum('source/D2Common/include/D2States.h', 'D2C_States') + cpp
    cpp += enum('source/D2Common/include/DataTbls/SkillsTbls.h', 'D2C_SkillsTxtFlagIndices')
    cpp += enum('source/D2Common/include/DataTbls/SkillsTbls.h', 'D2C_SkillsTxtLineOfSight')
    replace('struct D2SkillStrc{', 'struct D2SkillsTxt;struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=nullptr;')
    replace('wMana=15,wLevelMana=0,nManaShift=8;', 'wMana=15,wLevelMana=0,nManaShift=8,wMinMana=1,wSrvStartFunc=0,nLineOfSight=0,wItemEffect=0,wSrvMissile=-1,nAnim=10,nSkillId=223;unsigned dwFlags[2]={};')
    replace('int nStatesTxtRecordCount=150;', 'int nStatesTxtRecordCount=150,nMissilesTxtRecordCount=0,nShiftedStuff=63,nStuff=6;int pMissilesTxt[1]={};')
    replace('int dwFlags=0,dwUnitId=42,dwUnitType=0,dwClassId=5;', 'int dwFlags=0,dwUnitId=42,dwUnitType=0,dwClassId=5,dwSeqFrame=0;void* pDynamicPath=nullptr;')
    replace('int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}',
        'int mana=0,paid=0;int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int layer){assert(stat==STAT_MANA&&layer==0);return mana;}')
    cpp += r'''
constexpr int PLRMODE_SEQUENCE=18,PLRMODE_TOWNNEUTRAL=5,UNIT_ITEM=4,STAT_ITEM_CHARGED_SKILL=10;
constexpr int COLLIDE_MISSILE_BARRIER=0,COLLIDE_MASK_PLAYER_PATH=0,COLLIDE_PLAYER=0,COLLIDE_MONSTER=0,COLLIDE_DOOR=0,COLLIDE_WALL=0;
unsigned gdwBitMasks[32];struct D2ActiveRoomStrc{}room;
using SkillStartFunc=int32_t(*)(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);
using SkillDoFunc=SkillStartFunc;SkillStartFunc gpSkillSrvStartFnTable_6FD408B0[1]={};SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[117]={};
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){assert(false);return false;}
unsigned SKILLS_GetFlags(D2SkillStrc*){return 0;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return form?228:223;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){assert(false);}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){assert(false);}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){assert(false);return nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc*){assert(false);return 0;}
int CLIENTS_GetUnitY(D2UnitStrc*){assert(false);return 0;}
int D2COMMON_10175_PathGetFirstPointX(void*){assert(false);return 0;}
int D2COMMON_10176_PathGetFirstPointY(void*){assert(false);return 0;}
bool D2Common_11026(int,int,D2UnitStrc*,uint32_t){assert(false);return false;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
void sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){assert(false);}
void sub_6FD11420(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){assert(false);}
int D2GAME_SKILLS_BloodMana_6FD025E0(D2UnitStrc*,int){assert(false);return 0;}
void STATLIST_AddUnitStat(D2UnitStrc*,int stat,int value,int layer){assert(stat==STAT_MANA&&layer==0);mana+=value;paid++;events.push_back({11,stat,value});}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,int){assert(false);return nullptr;}
bool ITEMMODS_GetItemCharges(D2UnitStrc*,int,int,int*,D2StatListStrc**){assert(false);return false;}
void STATLIST_SetBaseStat2(D2StatListStrc*,int,int,int,D2UnitStrc*){assert(false);}
void* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){assert(false);return nullptr;}
void D2GAME_PACKETS_SendPacket0x3E_6FC3EC20(void*,D2UnitStrc*,int,int,int,int){assert(false);}
constexpr int EVENTTYPE_PERIODICSKILLS=2;
using BOOL=int;constexpr int TRUE=1,FALSE=0;
'''
    skills = 'source/D2Game/src/SKILLS/Skills.cpp'
    for name in ['D2GAME_SKILLMANA_Consume_6FD10A50', 'sub_6FD12950', 'D2GAME_SKILLS_Handler_6FD12BA0']:
        cpp += extract(skills, name)
    cpp += extract('source/D2Common/src/D2Skills.cpp', 'SKILLS_CheckSkillDelay')
    for name, digest in prior['completeRoutineHashes'].items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
 assert(STATE_SKILLDELAY==121&&STATE_WOLF==139&&STATE_BEAR==140);
 gpSkillSrvDoFnTable_6FD40A20[116]=SKILLS_SrvDo116_Wearwolf_Wearbear;
 tables.states[139].wGroup=tables.states[140].wGroup=3;
 printf("[");bool first=true;
 for(int f:{0,1})for(int level:{1,3,20})for(int startMana:{0,255,256,3839,3840,7000})
 for(int endMana:{0,255,256,3839,3840,7000})for(int initial:{0,1,2})for(int allocationFailure:{0,1}){
  form=f;rank=level;lycan=1;failure=allocationFailure?3:0;unit={};row={};skill={};skill.rank=level;allocated=0;used=hasSkill=true;mana=startMana;paid=0;events.clear();
  row.nAuraState=f?140:139;row.nSkillId=f?228:223;skill.pSkillsTxt=&row;if(f)row.wAuraStat[1]=4;row.dwFlags[0]=gdwBitMasks[SKILLSFLAGINDEX_INTOWN];
  if(initial){int state=initial==1?row.nAuraState:(f?139:140);auto*list=&storage[allocated++];*list={};list->state=state;list->posted=true;list->stats[7]=state==139?223:228;list->callback=sub_6FCFE030;unit.lists[state]=list;unit.states[state]=true;}
  int begun=sub_6FD12950(&game,&unit,f?228:223,rank,0),afterStart=mana,result=-1;
  if(begun){mana=endMana;result=D2GAME_SKILLS_Handler_6FD12BA0(&game,&unit,f?228:223,rank,1,0,0);}
  auto*delay=unit.lists[121];
  assert(SKILLS_CheckSkillDelay(&unit,&skill)==!unit.states[121]);
  row.dwDelay=12;assert(SKILLS_CheckSkillDelay(&unit,&skill));row.dwDelay=11;
  assert(SKILLS_CheckSkillDelay(&unit,nullptr)&&SKILLS_CheckSkillDelay(nullptr,&skill));
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",f,level,startMana,endMana,initial,allocationFailure,begun,afterStart,result,mana,paid,unit.states[139],unit.states[140],delay?delay->expire:0);first=false;
 }printf("]\n");
 assert(sub_6FD12950(nullptr,&unit,223,1,0)==0&&sub_6FD12950(&game,nullptr,223,1,0)==0);
 assert(D2GAME_SKILLS_Handler_6FD12BA0(nullptr,&unit,223,1,1,0,0)==0&&D2GAME_SKILLS_Handler_6FD12BA0(&game,nullptr,223,1,1,0,0)==0);
}
'''
    generated = BASE / 'druid-form-payment-source-probe.cpp';generated.write_text(cpp)
    compiler = BASE / 'druid-form-payment-compiler.log'
    with compiler.open('wb') as output:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all',
            str(generated), '-o', str(generated.with_suffix(''))], stdout=output, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-form-payment-sanitizer.log';sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    cases = json.loads(process.stdout);assert len(cases) == 1296
    for form, level, start, end, initial, failure, begun, after_start, result, mana, paid, wolf, bear, delay in cases:
        assert begun == int(initial != 0 or start >= 3840) and after_start == start
        if not begun:
            assert result == -1 and mana == start and paid == wolf == bear == delay == 0
        elif initial:
            assert result == 0 and mana == end and paid == wolf == bear == 0 and delay == (0 if failure else 125)
        elif failure:
            assert result == 0 and mana == end and paid == wolf == bear == delay == 0
        else:
            cost = 256 if level == 1 else 3840
            payment = int(end >= cost)
            assert result == 1 and mana == end - payment * cost and paid == payment
            assert [wolf, bear] == ([1, 0] if form == 0 else [0, 1]) and delay == 125
    fixture = BASE / 'druid-form-payment-source-probe.json';fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        predecessor=dict(path=str(prior_path.relative_to(ROOT)), sha256=sha(prior_path)), sourceHashes=hashes,
        currentSkillsSha256=sha(local), currentFormFields={row['skill']:{key:row[key] for key in fields} for row in forms}, currentGlobalDelayRows=global_rows,
        unchangedLifecycleRoutineHashes=prior['completeRoutineHashes'], completeOuterRoutineHashes=routines, contexts=1296,
        generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        established=[
            'Complete start handler,do dispatcher and mana consumer compose with the seven unchanged header-corrected form lifecycle routines.1296contexts vary form,rank,start/release mana,human/same/cross-group state and allocation failure under ASan/UBSan.',
            'Human entry start requires15mana but the null start routine consumes nothing. Successful do116allocates/applies the form before outer payment and global25frame delay. Failed form allocation leaves mana/state/delay unchanged. Existing form removal returns0,spends nothing and explicitly requests25frame delay; delay-allocation failure does not restore the removed form.',
            'The legacy consumer charges only minmana1at rank1despite the15mana entry threshold/current table; higher tested ranks charge15. With release mana below that legacy debit it returns0,but the dispatcher ignores that result: the applied form and25frame delay remain. These are bounded old-engine findings,not current D2Racceptance.',
            'The complete skill-delay eligibility function also executes per case: an active delay excludes positive-delay skills but permits a zero-delay control. Only Werewolf/Werebear have nonzero globaldelay in the pinned current table; both are25. This supports one shared form cooldown scope without blocking ordinary zero-delay actions.',
        ], remaining=[
            'Prefer pinned current15mana for native ordinary form entry; do not port the known generic legacy rank1debit quirk. Modern lost-mana/payment-failure behavior remains unverified. Current Param12direct switching is not established by the legacy cross-group removal cases.',
            'Room,formula,stat-list allocation,resource stores,skill lookup and unused missile/item/charged/BloodMana services retain explicit fixture boundaries. Accepted network input,timer queue/current executable/client presentation and native learned form casting remain separate. This preparation changes no live handler or candidate assembly.',
        ])
    path = ROOT / 'tools/reference/druid-form-payment-engine-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid form outer composition:1296complete source start/effect/payment/delay cases; legacy rank1/lost-mana findings remain explicitly unconfirmed for current D2R.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__);parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
