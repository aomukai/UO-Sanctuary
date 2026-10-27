#!/usr/bin/env python3
"""Execute complete GH/BL rate dispatch and event scheduling with licensed metadata."""
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
    prior_path = ROOT / 'tools/reference/druid-form-cast-clock-engine-evidence.json'
    prior = json.loads(prior_path.read_text())
    original = BASE / 'druid-form-cast-clock-source-probe.cpp'
    assert sha(original) == prior['generatedSourceSha256']
    cpp = original.read_text().split('struct Input{', 1)[0]
    routines = dict(prior['completeRoutineHashes'])
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == prior['legacyCommit']
    hashes = {}

    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
        hashes[relative] = sha(path)
        return raw.decode()

    def extract(relative, name):
        body = function(source(relative), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def replace(before, after):
        nonlocal cpp
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)

    states = re.search(r'enum D2C_States\s*\{.*?\};', source('source/D2Common/include/D2States.h'), re.S).group()
    cpp = cpp.replace('#define D2_ASSERT assert', '#define D2_ASSERT assert\n' + states, 1)
    replace('STAT_ITEM_FASTERCASTRATE=1;', 'STAT_ITEM_FASTERCASTRATE=1,STAT_ITEM_FASTERGETHITRATE=2,STAT_ITEM_FASTERBLOCKRATE=3;')
    replace('void* pDynamicPath=nullptr;', 'void* pInventory=nullptr;void* pDynamicPath=nullptr;')
    replace('convertedClass=430,fcr=0,updates=0;', 'convertedClass=430,fcr=0,updates=0,bonus=0,reads=0;bool holy=false;')
    replace('int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int stat,int layer){assert(stat==STAT_ITEM_FASTERCASTRATE&&layer==0);return fcr;}',
        'int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int stat,int layer){assert(layer==0&&stat==(p->dwAnimMode==PLRMODE_BLOCK?STAT_ITEM_FASTERBLOCKRATE:STAT_ITEM_FASTERGETHITRATE));reads++;return bonus;}')
    replace('void D2COMMON_11013_ConvertMode(D2UnitStrc*,int* type,int* cls,int* mode,const char*,int)\n{if(converted==UNIT_MONSTER){*type=UNIT_MONSTER;*cls=convertedClass;*mode=MONMODE_CAST;}}',
        'void D2COMMON_11013_ConvertMode(D2UnitStrc*p,int* type,int* cls,int* mode,const char*,int)\n{assert(p->dwAnimMode==PLRMODE_BLOCK||p->dwAnimMode==PLRMODE_GETHIT);if(converted==UNIT_MONSTER){*type=UNIT_MONSTER;*cls=convertedClass;*mode=p->dwAnimMode==PLRMODE_BLOCK?MONMODE_BLOCK:MONMODE_GETHIT;}}')
    units = 'source/D2Common/src/Units/Units.cpp'
    replace('void UNITS_SetAnimData(D2UnitStrc*,int,int,int){assert(false);}', r'''
void FOG_Trace(const char*,...){assert(false);}
Anim* DATATBLS_GetAnimDataRecord(D2UnitStrc*p,int,int,int,void*){updates++;return p->pAnimData;}
bool STATES_CheckState(D2UnitStrc*,int state){assert(state==STATE_HOLYSHIELD);return holy;}
''' + extract(units, 'UNITS_SetAnimData'))
    stub = function(cpp, 'UNITS_UpdateAnimRateAndVelocity')
    selected = ''.join(extract(units, name) for name in ['UNITS_IsAnimModeBlocking', 'UNITS_UpdateBlockAnimRateAndVelocity',
        'UNITS_IsAnimModeGetHit', 'UNITS_UpdateGetHitAnimRateAndVelocity'])
    selected += r'''
// Unselected branches are guarded services,not fabricated timing results.
bool UNITS_IsAnimModeKnockBack(int,int){assert(false);return false;}
void UNITS_UpdateKnockbackAnimRateAndVelocity(D2UnitStrc*,int,int,int){assert(false);}
bool UNITS_CanAnimModeUseVelocityModifier(int,int,D2UnitStrc*,int){assert(false);return false;}
void UNITS_UpdateRunWalkAnimRateAndVelocity(D2UnitStrc*,int,int,int,const char*,int){assert(false);}
bool UNITS_CanAnimModeUseAttackRate(int,int,D2UnitStrc*,int){assert(false);return false;}
void UNITS_UpdateAttackAnimRateAndVelocity(D2UnitStrc*){assert(false);}
void UNITS_UpdateOtherAnimRateAndVelocity(D2UnitStrc*,int,int,int){assert(false);}
'''
    selected += extract(units, 'UNITS_UpdateAnimRateAndVelocity')
    replace(stub, selected.rstrip())
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    animation_path = ROOT / '.local/research/d2r-3.3.93847/AnimData.d2'
    assert sha(animation_path) == prior['animationSha256']
    raw = animation_path.read_bytes()
    offset = 0
    animations = []
    for _ in range(256):
        count = struct.unpack_from('<I', raw, offset)[0]
        offset += 4
        for _ in range(count):
            code = raw[offset:offset + 8].split(b'\0')[0].decode('ascii')
            frames, speed = struct.unpack_from('<II', raw, offset + 8)
            if (code.startswith('DZ') or code[:2] in ['40', 'TG']) and code[2:4] in ['GH', 'BL']:
                animations.append(dict(code=code, frames=frames, speed=speed,
                    events=[[i, flag] for i, flag in enumerate(raw[offset + 16:offset + 160]) if flag]))
            offset += 160
    animations.sort(key=lambda row: row['code'])
    assert len(animations) == 15 and all(not row['events'] for row in animations)
    assert [(row['code'], row['frames'], row['speed']) for row in animations if not row['code'].startswith('DZ')] == [
        ('40BLHTH', 5, 256), ('40GHHTH', 4, 256), ('TGBLHTH', 5, 200), ('TGGHHTH', 5, 184)]
    cpp += '\nstruct Input{int type,cls,mode,frames,speed;};\nInput inputs[]={\n'
    cpp += ',\n'.join('{%d,%d,%s,%d,%d}' % (0 if row['code'].startswith('DZ') else 1,
        5 if row['code'].startswith('DZ') else 430 if row['code'].startswith('40') else 431,
        'PLRMODE_BLOCK' if row['code'][2:4] == 'BL' else 'PLRMODE_GETHIT', row['frames'], row['speed']) for row in animations)
    cpp += r'''
};
int main(){
 assert(UNITS_IsAnimModeBlocking(UNIT_PLAYER,PLRMODE_BLOCK)&&UNITS_IsAnimModeBlocking(UNIT_MONSTER,MONMODE_BLOCK));
 assert(!UNITS_IsAnimModeBlocking(UNIT_PLAYER,PLRMODE_GETHIT)&&!UNITS_IsAnimModeBlocking(UNIT_OBJECT,PLRMODE_BLOCK));
 assert(UNITS_IsAnimModeGetHit(UNIT_PLAYER,PLRMODE_GETHIT)&&UNITS_IsAnimModeGetHit(UNIT_MONSTER,MONMODE_GETHIT));
 assert(!UNITS_IsAnimModeGetHit(UNIT_PLAYER,PLRMODE_BLOCK)&&!UNITS_IsAnimModeGetHit(UNIT_OBJECT,PLRMODE_GETHIT));
 printf("[");bool first=true;int index=0;
 for(auto input:inputs){for(int value=0;value<259;value++)for(bool shield:{false,true}){
  bonus=value<256?value:value==256?500:value==257?1000:10000;holy=shield;converted=input.type;convertedClass=input.cls;
  Anim anim;anim.dwFrames=input.frames;anim.dwAnimSpeed=input.speed;
  D2UnitStrc p;p.dwAnimMode=input.mode;p.pAnimData=&anim;p.pDynamicPath=&p;D2GameStrc game;events.clear();updates=reads=0;
  sub_6FCBC4D0(&p);assert(updates==2&&reads==1&&!p.pAnimSeq&&p.dwFrameCountPrecise==input.frames*256);
  sub_6FCBCE70(&game,&p);assert(events.size()==1&&events[0].type==EVENTTYPE_ENDANIM&&events[0].kind==0&&events[0].param==0);
  printf("%s[%d,%d,%d,%d,%d]",first?"":",",index,bonus,holy,p.wAnimSpeed,events[0].frame-game.dwGameFrame);first=false;
 }index++;}printf("]\n");
}
'''
    generated = BASE / 'druid-form-recovery-clock-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-form-recovery-clock-compiler.log'
    with compiler.open('wb') as output:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=output, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-form-recovery-clock-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()
    cases = json.loads(process.stdout)
    assert len(cases) == 7770
    for index, bonus, holy, speed, end in cases:
        row = animations[index]
        block = row['code'][2:4] == 'BL'
        expected = row['speed'] * ((100 if block and holy else 50) + 120 * bonus // (120 + bonus)) // 100
        assert speed == expected
        assert end == max(2, (row['frames'] * 256 + speed - 1) // speed)
    fixture = BASE / 'druid-form-recovery-clock-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    timer = function(source('source/D2Game/src/GAME/Event.cpp'), 'D2GAME_InitTimer_6FC351D0')
    assert 'if (nExpireFrame <= pGame->dwGameFrame)' in timer and 'pEventTimer->nExpireFrame = nExpireFrame;' in timer
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        legacyCommit=tree['sha'], predecessor=dict(path=str(prior_path.relative_to(ROOT)), sha256=sha(prior_path)),
        sourceHashes=hashes, completeRoutineHashes=routines, animationSha256=sha(animation_path), animations=animations,
        contexts=len(cases), formContexts=sum(not animations[row[0]]['code'].startswith('DZ') for row in cases),
        zeroBonusFormGoldens=[dict(animation=animations[row[0]]['code'], speed=row[3], sourceEndFrame=row[4]) for row in cases if row[1] == 0 and row[2] == 0 and not animations[row[0]]['code'].startswith('DZ')],
        generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        staticTimerReview=dict(routine='D2GAME_InitTimer_6FC351D0', sha256=hashlib.sha256(timer.encode()).hexdigest(), executed=False),
        established=[
            'Complete rate dispatcher executes FCR/block/get-hit ordering with actual blocking/get-hit selectors and rate writers. All15licensed human/form GH/BLrecords,bonus0..255/500/1000/10000 and Holy Shield controls feed complete animation metadata assignment,initializer,frame-bonus and scheduler bodies. No attack/OTHER_ANIMRATE or movement branch is reached; each context reads exactly the matching FHR/FBR stat once.',
            'Wolf GH4frames/speed256 and BL5/256 differ from bear GH5/184 and BL5/200. Zero-bonus/no-Holy source ENDANIM offsets are8,10,14and13respectively. Human weapon-specific controls remain separate. These are scheduled server end events,not automatically current-client or player reentry-frame counts.',
            'The event allocator is statically reviewed: future expiry frames are retained; only past/current requests clamp to next game frame. This does not execute the full queue/player mode transition. It does not supply evidence for subtracting one frame from a source ENDANIM timestamp.',
        ], declaredServices=[
            'Full rate dispatcher and selected writers execute unchanged. Composite conversion and exact current-record lookup are declared services pinned to licensed records; lookup returns the selected record without modifying its frames/speed. Unselected movement,attack,knockback,OTHER_ANIMRATE and unsupported sequence/item helpers are guarded unexecuted.',
            'Holy Shield=true on a Druid is a synthetic writer-branch control,not a newly supported foreign-class form/skill route. Sequence scheduling and real event insertion/queue execution remain outside this fixture.',
        ], remaining=[
            'Native form-specific recovery/block consumers remain to be implemented. Existing native human Frames/BlockFrames use ceil(frames*256/advance)-1. Keep this projection separate from the proved source ENDANIM offset until native/current reentry policy is established; do not silently equate them or alter all human classes.',
            'Movement,knockback,modern exact reentry/soft-hit/stun/owner/client and gear/respec refresh remain separate. Current Werebear concentration protects active actions but does not eliminate idle recovery. No new handler or asset is claimed.',
        ])
    path = ROOT / 'tools/reference/druid-form-recovery-clock-engine-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid GH/BL:7770complete dispatcher/writer/scheduler contexts,15licensed records,2072form cases,ASan/UBSan clean; current/native reentry remains distinct.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
