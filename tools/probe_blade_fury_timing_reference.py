#!/usr/bin/env python3
"""Complete SQ23 event getter and channel rewind; held-input caller stays separate."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, routines = {}, {}

    def source(rel):
        raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            x['sha'] for x in tree['tree'] if x['path'] == rel)
        hashes[rel] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def extract(text, name):
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

    seq = source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
    start = seq.index('D2AnimSeqTxt gPlayerSequenceBladeFury[19]')
    data = seq[start:seq.index('};', start) + 2]
    unit = source('source/D2Game/src/UNIT/SUnit.cpp')
    common_units = source('source/D2Common/src/Units/Units.cpp')
    weapon_map = seq[seq.index('D2PlayerWeaponSequencesStrc gPlayerWeaponsSequenceBladeFury'):]
    weapon_map = weapon_map[:weapon_map.index('};') + 2]
    assert weapon_map.count('gPlayerSequenceBladeFury, 19, 19,') == 14
    mapping = seq[seq.index('D2PlayerWeaponSequencesStrc* gPlayerWeaponsSequenceTable[24]'):]
    mapping = mapping[:mapping.index('};') + 2]
    assert [x.strip() for x in mapping.split('{', 1)[1].split('}', 1)[0].split(',') if x.strip()][23] == '&gPlayerWeaponsSequenceBladeFury'
    cpp = r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
constexpr int PLRMODE_CAST=10,ANIMSEQ_EVENT_NONE=0,ANIMSEQ_EVENT_MELEE_ATTACK=1;
constexpr int NUM_WEAPON_CLASSES=14,NUMBER_OF_PLAYERCLASSES=7,UNIT_TYPES_COUNT=6,UNIT_PLAYER=0,WEAPONCLASS_HTH=0,PLRMODE_ATTACK1=7,PLRMODE_ATTACK2=8,PLRMODE_SPECIAL3=15,PLRMODE_SPECIAL4=16;
constexpr int EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2,EVENTTYPE_REMOVESTATE=3;
struct D2AnimSeqTxt{int wSequence,nMode,nFrame,nDir,nEvent;};
struct Anim{uint8_t pFrameFlags[32]={};}anim;
struct D2UnitStrc{int dwUnitType=0,dwClassId=6,dwAnimMode=18,nSeqCurrentFramePrecise=0,dwSeqSpeed=256,dwSeqFrameCount=19*256,wAnimSpeed=256,dwFrameCountPrecise=19*256;D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=&anim;}owner;
struct D2GameStrc{int dwGameFrame=1000;}game;
struct Event{int type,frame,event,param;};std::vector<Event> events;
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){std::erase_if(events,[&](auto e){return e.type==type;});}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int event,int param){events.push_back({type,frame,event,param});}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){assert(false);return nullptr;}
int ITEMS_GetWeaponClassId(D2UnitStrc*){assert(false);return 0;}
bool UNITS_CanDualWield(D2UnitStrc*){assert(false);return false;}
'''
    cpp += data + '\n' + extract(seq, 'DATATBLS_GetSequenceEvent')
    cpp += extract(common_units, 'UNITS_GetFrameBonus')
    cpp += extract(unit, 'sub_6FCBCE70') + extract(unit, 'sub_6FCBD120')
    cpp += r'''
int main(){printf("{\"initial\":[");bool first=true;
for(int sequence:{0,1})for(int speed:{0,64,128,256,341,448,512}){
 owner={};owner.pAnimSeq=sequence?gPlayerSequenceBladeFury:nullptr;owner.dwSeqSpeed=owner.wAnimSpeed=speed;
 owner.nSeqCurrentFramePrecise=900*256;anim={};anim.pFrameFlags[12]=1;events.clear();
 sub_6FCBCE70(&game,&owner);
 printf("%s[%d,%d,%d,%d,[",first?"":",",sequence,speed,UNITS_GetFrameBonus(&owner),owner.nSeqCurrentFramePrecise);first=false;bool sep=false;
 for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame,e.event,e.param);sep=true;}printf("]]");
}printf("],\"rewind\":[");first=true;
for(int sequence:{0,1})for(int speed:{0,64,128,256,341,448,512})for(int elapsed=2;elapsed<=18;elapsed++)for(int rewind:{0,1}){
 owner={};owner.pAnimSeq=sequence?gPlayerSequenceBladeFury:nullptr;owner.dwSeqSpeed=owner.wAnimSpeed=speed;
 owner.nSeqCurrentFramePrecise=(1000-elapsed)*256;anim={};anim.pFrameFlags[12]=1;
 events={{1,1009,4,0},{2,1020,0,0},{3,1021,0,0}};
 sub_6FCBD120(&game,&owner,rewind);
 printf("%s[%d,%d,%d,%d,%d,[",first?"":",",sequence,speed,elapsed,rewind,owner.nSeqCurrentFramePrecise);first=false;bool sep=false;
 for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame,e.event,e.param);sep=true;}printf("]]");
}printf("]}\n");}
'''
    path = STAGE / 'blade-fury-timing-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'blade-fury-timing-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    matrix = json.loads(run.stdout)
    rows = matrix['rewind']
    assert len(rows) == 476
    assert len(matrix['initial']) == 14
    for sequence, speed, bonus, stamp, events in matrix['initial']:
        expected = []
        frame = 1000
        index = 0
        if speed:
            point = speed
            while point < 19 * 256:
                frame += 1
                while index <= point // 256:
                    if index == 12:
                        expected.append([1, frame, 1, 0])
                    index += 1
                point += speed
            if frame == 1000:
                frame += 1
            expected.append([2, frame + 1, 0, 0])
        else:
            expected.append([2, 1001, 0, 0])
        assert bonus == 0 and stamp == (1000 if speed else 900) * 256 and events == expected
    for sequence, speed, elapsed, rewind, stamp, events in rows:
        expected_stamp = (1000 - elapsed + rewind) * 256
        expected = [[1, 1009, 4, 0], [2, 1020, 0, 0], [3, 1021, 0, 0]]
        if rewind:
            expected = [[3, 1021, 0, 0]]
            if speed:
                calc = elapsed - rewind
                index, point, frame = calc - 1, calc * 256 + speed, 1000
                while point < 19 * 256:
                    frame += 1
                    while index <= point // 256:
                        if index == 12:
                            expected.append([1, frame, 1, 0])
                        index += 1
                    point += speed
                if frame == 1000:
                    frame += 1
                expected.append([2, frame + 1, 0, 0])
            else:
                expected.append([2, 1001, 0, 0])
        assert stamp == expected_stamp and events == expected, (sequence, speed, elapsed, rewind, stamp, events, expected)
    output = STAGE / 'blade-fury-timing-source-probe.json'
    output.write_text(json.dumps(matrix, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'],
                    nativeVerifiedByThisSourceProbe=False, sourceHashes=hashes,
                    compiledRoutineHashes=routines,
                    sequenceDataSha256=hashlib.sha256(data.encode()).hexdigest(),
                    playerSequence=23, sequencePoints=19, actionPoint=12,
                    fixture=dict(path=str(output.relative_to(ROOT)), cases=dict(initial=14,rewind=476),total=490,
                                 sha256=hashlib.sha256(output.read_bytes()).hexdigest()),
                    established=[
                        'Source sequence table index23 binds Blade Fury:19 points, cast frames0..9, two extra frame9 points, sole attack event at point12, ending at cast frame16. All14 weapon records reference that19-point sequence.',
                        'Complete initial scheduler and ordinary SQ-mode frame-bonus function execute14 cases. SQ mode has zero starting-frame bonus. At speed256 the action event is at frame12 and end at frame19. Zero speed schedules end+1 without rewriting the old sequence epoch.',
                        'Complete sub_6FCBD120 and sequence event getter execute476 cases, including zero-speed and no-op controls. Positive renewal deletes mode/end events, advances the stored sequence epoch by one frame for the actual argument1 and schedules remaining events/end; removal-state events are preserved.',
                        'An existing channel is not a new fixed19-frame cast each shot. Its rewind changes future event timestamps; start/do re-entry, strict shot deadline and mana callers must be composed with that event stream.'
                    ], limits=[
                        'Sequence and mirrored nonsequence branches are bounded controls with elapsed2..18 and speeds0/64/128/256/341/448/512. This does not establish all valid caller epochs, source event-queue dispatch order, original mode entry, attack-rate calculation or current executable timings.',
                        'The complete older rewind is not yet bound into native Blade Fury or Inferno. Held input, seqinput12, release/re-entry/cancellation, per-shot resource/weapon/missile and production persistence remain pending.'
                    ], sanitizers='ASan/UBSan; exit0 and empty runtime stderr')
    target = ROOT / 'tools/reference/blade-fury-timing-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('14 complete initial scheduler and476 Blade Fury SQ23 event/rewind cases pass; actual held-input/event composition remains pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
