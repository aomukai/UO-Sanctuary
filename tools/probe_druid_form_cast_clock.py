#!/usr/bin/env python3
"""Execute source SC/FCR selection and scheduling with licensed Druid metadata."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
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
        path = BASE / 'd2moo-pinned-source' / name;raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        hashes[name] = sha(path)
        return raw.decode()

    def extract(name, symbol):
        body = function(source(name), symbol)
        routines[symbol] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def enum(name, symbol):
        match = re.search(r'enum ' + symbol + r'\s*\{.*?\};', source(name), re.S)
        assert match, symbol
        return match.group() + '\n'

    preflight_path = ROOT / 'tools/reference/druid-forms-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    for name in ['Wearwolf', 'Wearbear']:
        row = preflight['currentRows']['skills'][name]
        assert [row[key] for key in ['anim', 'seqtrans', 'seqnum', 'UseAttackRate']] == ['SC', 'SC', '22', '1']
    path = ROOT / '.local/research/d2r-3.3.93847/AnimData.d2'
    assert sha(path) == preflight['animation']['sha256']
    raw = path.read_bytes();offset = 0;animations = []
    for _ in range(256):
        count = struct.unpack_from('<I', raw, offset)[0];offset += 4
        for _ in range(count):
            code = raw[offset:offset + 8].split(b'\0')[0].decode('ascii')
            frames, speed = struct.unpack_from('<II', raw, offset + 8)
            if code.startswith('DZSC') or code in ['40SCHTH', 'TGSCHTH']:
                animations.append(dict(code=code, frames=frames, speed=speed, events=[[i, flag] for i, flag in enumerate(raw[offset + 16:offset + 160]) if flag]))
            offset += 160
    animations.sort(key=lambda row: row['code'])
    assert len(animations) == 10
    human = [row for row in animations if row['code'].startswith('DZ')]
    assert len(human) == 8 and all((row['frames'], row['speed'], row['events']) == (15, 208, [[8, 1]]) for row in human)
    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <limits>
#include <vector>
#define __stdcall
#define __fastcall
#define __forceinline inline
#define D2_ASSERT assert
using BOOL=int;constexpr int TRUE=1,FALSE=0;
template<class T>T D2Clamp(T n,T low,T high){return std::clamp(n,low,high);}
'''
    for path_, name in [('D2Composit.h', 'D2WeaponClasses'), ('Units/Player.h', 'D2C_PlayerModes'),
        ('Units/Player.h', 'D2C_PlayerClasses'), ('Units/Units.h', 'D2C_UnitTypes'), ('D2Monsters.h', 'D2C_MonModes')]:
        cpp += enum('source/D2Common/include/' + path_, name)
    cpp += r'''
constexpr int EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2,STAT_ITEM_FASTERCASTRATE=1;
struct D2AnimSeqTxt{};struct Anim{int dwFrames=0,dwAnimSpeed=0;uint8_t pFrameFlags[144]={};};
struct D2SkillsTxt{int nSkillId=223,nSeqTrans=PLRMODE_CAST;}row;
struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=&row;int dwSkillMode=PLRMODE_CAST;}skill;
struct D2UnitStrc{int dwUnitType=UNIT_PLAYER,dwClassId=PCLASS_DRUID,dwAnimMode=PLRMODE_CAST;
 int nActionFrame=99,nSeqCurrentFramePrecise=99,dwSeqSpeed=0,dwSeqFrameCount=0,wAnimSpeed=0,dwFrameCountPrecise=0;
 void* pDynamicPath=nullptr;D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=nullptr;};
struct D2GameStrc{int dwGameFrame=1000;};
struct Event{int type,frame,kind,param;};std::vector<Event> events;
bool used=true,record=true;int converted=UNIT_PLAYER,convertedClass=430,fcr=0,updates=0;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return used?&skill:nullptr;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==223);return record?&row:nullptr;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int stat,int layer){assert(stat==STAT_ITEM_FASTERCASTRATE&&layer==0);return fcr;}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int* type,int* cls,int* mode,const char*,int)
{if(converted==UNIT_MONSTER){*type=UNIT_MONSTER;*cls=convertedClass;*mode=MONMODE_CAST;}}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){assert(false);return nullptr;}
int ITEMS_GetWeaponClassId(D2UnitStrc*){assert(false);return 0;}
bool UNITS_CanDualWield(D2UnitStrc*){assert(false);return false;}
D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){assert(false);return nullptr;}
void UNITS_InitializeSequence(D2UnitStrc*){assert(false);}
void UNITS_SetAnimData(D2UnitStrc*,int,int,int){assert(false);}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int kind,int param){events.push_back({type,frame,kind,param});}
'''
    units = 'source/D2Common/src/Units/Units.cpp'
    cpp += extract('source/D2Common/src/D2Skills.cpp', 'SKILLS_GetSkillModeFromUnit')
    for name in ['UNITS_SetAnimationSpeed', 'UNITS_IsSeqAnimSpeedModulatedByFCR', 'UNITS_UpdateCastAnimRateAndVelocity', 'UNITS_GetFrameBonus']:
        cpp += extract(units, name)
    cpp += r'''
// Full mode conversion/metadata lookup is an explicit service. The source
// dispatch checks FCR before attack/OTHER_ANIMRATE; execute its selected writer.
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc* p,const char*,int)
{assert(UNITS_IsSeqAnimSpeedModulatedByFCR(p,converted,converted==UNIT_PLAYER?int(PLRMODE_CAST):int(MONMODE_CAST)));updates++;UNITS_UpdateCastAnimRateAndVelocity(p);}
'''
    for name in ['sub_6FCBC4D0', 'sub_6FCBCE70']:
        cpp += extract('source/D2Game/src/UNIT/SUnit.cpp', name)
    cpp += 'struct Input{int type,cls,frames,speed,action,event;};\nInput inputs[]={\n'
    cpp += ',\n'.join('{%d,%d,%d,%d,%d,%d}' % (0 if row['code'].startswith('DZ') else 1, 431 if row['code'].startswith('TG') else 430, row['frames'], row['speed'], *row['events'][0]) for row in animations)
    cpp += r'''
};
int main(){D2UnitStrc owner;
 assert(SKILLS_GetSkillModeFromUnit(&owner,&skill)==PLRMODE_CAST);
 assert(SKILLS_GetSkillModeFromUnit(nullptr,&skill)==0&&SKILLS_GetSkillModeFromUnit(&owner,nullptr)==0);
 assert(UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_PLAYER,PLRMODE_CAST));
 assert(UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_MONSTER,MONMODE_CAST));
 assert(!UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_PLAYER,PLRMODE_ATTACK1));
 assert(!UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_MONSTER,MONMODE_ATTACK1));
 assert(!UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_OBJECT,0));
 assert(UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_PLAYER,PLRMODE_SEQUENCE));
 row.nSeqTrans=PLRMODE_ATTACK1;assert(!UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_PLAYER,PLRMODE_SEQUENCE));row.nSeqTrans=PLRMODE_CAST;
 used=false;assert(!UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_PLAYER,PLRMODE_SEQUENCE));used=true;
 record=false;assert(!UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_PLAYER,PLRMODE_SEQUENCE));record=true;
 printf("[");bool first=true;int index=0;
 for(auto input:inputs){for(int value=0;value<259;value++){
  fcr=value<256?value:value==256?500:value==257?1000:10000;converted=input.type;convertedClass=input.cls;
  Anim anim;anim.dwFrames=input.frames;anim.dwAnimSpeed=input.speed;anim.pFrameFlags[input.action]=input.event;
  D2UnitStrc p;p.pAnimData=&anim;p.pDynamicPath=&p;D2GameStrc game;events.clear();updates=0;
  sub_6FCBC4D0(&p);assert(updates==1&&!p.pAnimSeq&&p.dwSeqSpeed==p.wAnimSpeed&&p.dwFrameCountPrecise==input.frames*256);
  sub_6FCBCE70(&game,&p);
  printf("%s[%d,%d,%d,[",first?"":",",index,fcr,p.wAnimSpeed);first=false;bool sep=false;
  for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame-game.dwGameFrame,e.kind,e.param);sep=true;}
  printf("]]");
 }index++;}printf("]\n");}
'''
    dispatch = function(source(units), 'UNITS_UpdateAnimRateAndVelocity')
    assert dispatch.index('UNITS_IsSeqAnimSpeedModulatedByFCR') < dispatch.index('UNITS_CanAnimModeUseAttackRate')
    reviewed = dict(UNITS_UpdateAnimRateAndVelocity=hashlib.sha256(dispatch.encode()).hexdigest())
    generated = BASE / 'druid-form-cast-clock-source-probe.cpp';generated.write_text(cpp)
    compiler = BASE / 'druid-form-cast-clock-compiler.log'
    with compiler.open('wb') as output:
        subprocess.run(['g++', '-std=c++20', '-Wall', '-Wextra', '-Werror', '-Wno-parentheses', '-O1', '-g',
            '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=output, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-form-cast-clock-sanitizer.log';sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    cases = json.loads(process.stdout);assert len(cases) == 2590
    for index, fcr, speed, events in cases:
        row = animations[index];effective = 120 * fcr // (120 + fcr)
        assert speed == row['speed'] * min(175, 100 + effective) // 100
        point, event = row['events'][0]
        action = (point * 256 + speed - 1) // speed;end = (row['frames'] * 256 + speed - 1) // speed
        assert events == [[1, action, event, 0], [2, end, 0, 0]]
    fixture = BASE / 'druid-form-cast-clock-source-probe.json';fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        sourceHashes=hashes, completeRoutineHashes=routines, reviewedRoutineHashes=reviewed,
        preflightSha256=sha(preflight_path), animationSha256=preflight['animation']['sha256'], animations=animations, contexts=2590,
        generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        established=[
            'Seven complete source routines execute mode getter,FCR-selection predicate,cast-speed writer,setter,frame-bonus lookup,initializer and scheduler. Current form anim/seqtransSCand retained skill modeCAST10do not independently select SQ18from seqnum22. The sequence branch is guarded unreachable for these actual SCcontexts; separate predicate controls cover explicit SQinput.',
            'All eight licensed human Druid SCweapon variants have15frames,speed208,event1at8. Wolf SCis11/168/event2at5;bear SCis10/152/event2at5.2590source contexts cover each record and FCR0..255,500,1000,10000,comparing ordered action/end events and truncated FCR speed.',
            'The reviewed dispatcher selects FCR before attack rate,so UseAttackRate1does not by itself turn an SCcast into an IASclock. Source startup schedules action/end before the effect changes form. ReverseS1sequence22remains separate animation data; its presence is not proof of a server SCtiming replacement.',
        ], remaining=[
            'Full conversion/metadata lookup and event insertion are declared services. Current executable/reentry/equal-deadline/client cltst45and reverseS1visual transport remain unverified. No current client implementation is inferred from the server reference.',
            'Compose outer start/do/return/payment/global-delay with do116before native learned casting. Free same-form unshift is user-confirmed; cross-form Param12is still unobserved. Existing generic Druid SCprofiles are not changed by this preparation. No native form casting handler is enabled.',
        ])
    path = ROOT / 'tools/reference/druid-form-cast-clock-engine-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid SCcast clock:2590complete source FCR/action/end cases across10licensed records; seqnum22does not alone select server SQ. Native casting pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__);parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
