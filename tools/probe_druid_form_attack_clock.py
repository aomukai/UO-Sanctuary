#!/usr/bin/env python3
"""Execute the ordinary source animation scheduler with prepared modern form rates.

Rate selection is the supplementary pinned calculator model, not the old engine's
rate writer. Animation metadata is current. Queue insertion and mode conversion
are declared services; this is preparation, not current executable acceptance.
"""
import argparse
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
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}

    def source(name):
        path = BASE / 'd2moo-pinned-source' / name
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        hashes[name] = sha(path)
        return raw.decode()

    def extract(name, symbol):
        body = function(source(name), symbol)
        routines[symbol] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def enum(name, symbol):
        text = source(name)
        match = re.search(r'enum ' + symbol + r'\s*\{.*?\};', text, re.S)
        assert match, symbol
        return match.group() + '\n'

    progress_path = ROOT / 'tools/reference/modern-form-progress-source-evidence.json'
    progress = json.loads(progress_path.read_text())
    fixture_path = ROOT / progress['fixture']['path']
    assert sha(fixture_path) == progress['fixture']['sha256']
    inputs = json.loads(fixture_path.read_text())
    assert len(inputs) == 14400
    rate_path = ROOT / progress['predecessor']['path']
    assert sha(rate_path) == progress['predecessor']['sha256']
    rates = json.loads(rate_path.read_text())
    wolf, bear = (rates['animation']['records'][key] for key in ['40A1HTH', 'TGA1HTH'])
    assert wolf['frames'] == 13 and bear['frames'] == 12
    assert wolf['events'] == bear['events'] == [[7, 1]]
    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
'''
    cpp += enum('source/D2Common/include/D2Composit.h', 'D2WeaponClasses')
    cpp += enum('source/D2Common/include/Units/Player.h', 'D2C_PlayerModes')
    cpp += enum('source/D2Common/include/Units/Player.h', 'D2C_PlayerClasses')
    cpp += enum('source/D2Common/include/Units/Units.h', 'D2C_UnitTypes')
    cpp += r'''
constexpr int MONMODE_SEQUENCE=14,EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2;
struct D2AnimSeqTxt{};
struct Anim{int dwFrames=0;uint8_t pFrameFlags[144]={};};
struct D2UnitStrc{int dwUnitType=UNIT_PLAYER,dwClassId=PCLASS_DRUID,dwAnimMode=PLRMODE_ATTACK1;
 int nActionFrame=99,nSeqCurrentFramePrecise=99,dwSeqSpeed=0,dwSeqFrameCount=0,wAnimSpeed=0,dwFrameCountPrecise=0;
 void* pDynamicPath=nullptr;D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=nullptr;};
struct D2GameStrc{int dwGameFrame=1000;};
struct Event{int type,frame,kind,param;};std::vector<Event> events;
int form=0,weapon=0,rate=0,updates=0;bool wield=false;
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int* type,int* cls,int* mode,const char*,int)
{if(form){*type=UNIT_MONSTER;*cls=form;*mode=4;}}
D2UnitStrc* D2Common_10434(D2UnitStrc* p,int slot){assert(slot==1);return wield?p:nullptr;}
int ITEMS_GetWeaponClassId(D2UnitStrc*){return weapon;}
bool UNITS_CanDualWield(D2UnitStrc*){return false;}
D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){assert(false);return nullptr;}
void UNITS_InitializeSequence(D2UnitStrc*){assert(false);}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc* p,const char*,int){p->wAnimSpeed=rate;updates++;}
void UNITS_SetAnimData(D2UnitStrc*,int,int,int){assert(false);}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int kind,int param){events.push_back({type,frame,kind,param});}
'''
    cpp += extract('source/D2Common/src/Units/Units.cpp', 'UNITS_GetFrameBonus')
    for symbol in ['sub_6FCBC4D0', 'sub_6FCBCE70']:
        cpp += extract('source/D2Game/src/UNIT/SUnit.cpp', symbol)
    cpp += 'struct Input{int form,frames,rate;};\nInput inputs[]={\n'
    cpp += ',\n'.join('{%d,%d,%d}' % (139 if row[0] == 2 else 140, row[7][2], row[7][8]) for row in inputs)
    cpp += r'''
};
int main(){
 // Actual Druid frame-bonus function, with declared human/form conversion.
 for(int selectedForm:{0,139,140})for(weapon=0;weapon<NUM_WEAPON_CLASSES;weapon++)
 for(int mode:{PLRMODE_ATTACK1,PLRMODE_ATTACK2,PLRMODE_CAST,PLRMODE_SPECIAL3}){
  form=selectedForm;D2UnitStrc p;p.dwAnimMode=mode;wield=true;assert(UNITS_GetFrameBonus(&p)==0);
 }
 printf("[");bool first=true;
 for(auto input:inputs){
  Anim anim;anim.dwFrames=input.frames;anim.pFrameFlags[7]=1;
  D2UnitStrc p;p.pAnimData=&anim;p.pDynamicPath=&p;D2GameStrc game;
  form=input.form;rate=input.rate;updates=0;events.clear();
  sub_6FCBC4D0(&p);
  assert(updates==1&&p.nActionFrame==0&&p.nSeqCurrentFramePrecise==0&&p.dwFrameCountPrecise==input.frames*256&&!p.pAnimSeq);
  sub_6FCBCE70(&game,&p);
  assert(p.nSeqCurrentFramePrecise==game.dwGameFrame*256);
  printf("%s[",first?"":",");first=false;bool sep=false;
  for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame-game.dwGameFrame,e.kind,e.param);sep=true;}
  printf("]");
 }
 printf("]\n");
 // Zero-rate scheduler deliberately has only a next-frame end and no action.
 Anim anim;anim.dwFrames=13;anim.pFrameFlags[7]=1;D2UnitStrc p;p.pAnimData=&anim;p.pDynamicPath=&p;
 D2GameStrc game;form=139;rate=0;events.clear();sub_6FCBC4D0(&p);sub_6FCBCE70(&game,&p);
 assert(events.size()==1&&events[0].type==EVENTTYPE_ENDANIM&&events[0].frame==1001);
}
'''
    generated = BASE / 'druid-form-attack-clock-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-form-attack-clock-compiler.log'
    with compiler.open('wb') as output:
        # Retain the original initializer's mixed &&/|| expression verbatim.
        subprocess.run(['g++', '-std=c++20', '-Wall', '-Wextra', '-Werror', '-Wno-parentheses', '-O1', '-g', '-fsanitize=address,undefined',
            '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=output, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-form-attack-clock-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    schedules = json.loads(process.stdout)
    assert len(schedules) == len(inputs)
    cases = []
    for row, events in zip(inputs, schedules, strict=True):
        frames, advance = row[7][2], row[7][8]
        action = (7 * 256 + advance - 1) // advance
        end = (frames * 256 + advance - 1) // advance
        assert events == [[1, action, 1, 0], [2, end, 0, 0]], (row, events)
        assert end == row[7][7] + 1
        cases.append([row[0], row[1], row[2], row[3], row[4], row[5], row[6], advance, events])
    fixture = BASE / 'druid-form-attack-clock-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        sourceHashes=hashes, completeRoutineHashes=routines,
        rateInput=dict(path=str(progress_path.relative_to(ROOT)), sha256=sha(progress_path), fixtureSha256=sha(fixture_path)),
        contexts=14400, druidFrameBonusControls=168, zeroRateControls=1,
        animation=dict(wolf=wolf, bear=bear), generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        established=[
            'Three complete source routines execute unchanged: actual Druid frame-bonus lookup,ordinary animation initialization and event scheduling. Converted-type/metadata/rate services are explicit. All168Druid human/form/weapon/mode frame-bonus controls are zero. The sequence path is guarded unreachable.',
            'For all14400prepared modern-rate inputs,current form A1frame7queues one melee event at ceil(7*256/progress),followed by ENDANIM at ceil(formFrames*256/progress). The no-speed control queues only an end next frame. Ordered event parameters are compared,not just a rounded duration.',
            'The legacy ENDANIM offset is one frame beyond the supplementary calculator cycle column in every case. These are distinct timeline quantities. This preparation does not establish modern accepted-input reentry or justify replacing one with the other.',
        ], remaining=[
            'Modern executable action/end/reentry,first callback order,equal-deadline global queue,cancellation/rescheduling and source-to-native transport still need acceptance. Rate selection uses the separately pinned3.3author model,not the obsolete legacy form rate writer.',
            'Only ordinary Druid form A1is covered. Other form actions,sequences,Fury/Maul/charge interactions,AR/SrcDam/melee-only dispatch,range,ammunition and learned casting remain separate. No native timing consumer or learned form handler is enabled.',
        ])
    path = ROOT / 'tools/reference/druid-form-attack-clock-engine-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid form A1:14400complete initializer/scheduler comparisons,168frame-bonus controls,ASan/UBSan clean; modern reentry remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
