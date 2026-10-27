#!/usr/bin/env python3
"""Execute complete sequence clock routines with current BearSmite rows.

This is preparation for a world consumer, not native Grizzly action acceptance.
"""
import argparse
import hashlib
import json
from pathlib import Path
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

    def extract(relative, name):
        raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        body = function(raw.decode(), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    preflight_path = ROOT / 'tools/reference/grizzly-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    rows = preflight['sequence']['rows']
    assert len(rows) == 17 and all(r['mode'] == 'A2' for r in rows)
    assert [int(r['frame']) for r in rows] == list(range(17))
    assert [int(r['event'] or 0) for r in rows] == [0] * 8 + [1] + [0] * 8
    animation = next(x for x in preflight['animation']['records'] if x['code'] == 'B7A2HTH')
    assert animation == dict(code='B7A2HTH', frames=17, speed=248, events=[[8, 1]])
    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define __fastcall
#define D2_ASSERT assert
using D2AnimSeqEvent=int;constexpr int ANIMSEQ_EVENT_NONE=0,UNITFLAG_SQGFXCHANGE=32;
struct D2AnimSeqTxt{unsigned nMode,nFrame;int nDir,nEvent;};
D2AnimSeqTxt rows[17];
struct D2UnitStrc{D2AnimSeqTxt*pAnimSeq=nullptr;unsigned dwSeqFrameCount=0,dwSeqFrame=0,dwSeqSpeed=0,dwSeqMode=0,dwFlags=0;int dwFrameCountPrecise=0,nSeqCurrentFramePrecise=0,nActionFrame=0,wAnimSpeed=0;};
D2AnimSeqTxt*DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){return rows;}
int DATATBLS_GetSeqFramePointsCount(D2UnitStrc*){return 17*256;}
int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 17;}
void UNITS_SetAnimActionFrame(D2UnitStrc*,int){assert(false);}
int UNITS_GetFrameBonus(D2UnitStrc*){assert(false);return 0;}
'''
    seq = 'source/D2Common/src/DataTbls/SequenceTbls.cpp'
    units = 'source/D2Common/src/Units/Units.cpp'
    for relative, name in [(seq, 'DATATBLS_ComputeSequenceAnimation'), (seq, 'DATATBLS_GetSequenceEvent'),
                           (units, 'UNITS_InitializeSequence'), (units, 'UNITS_StopSequence'),
                           (units, 'UNITS_IsAtEndOfFrameCycle')]:
        cpp += '\n' + extract(relative, name) + '\n'
    cpp += r'''
int main(){for(int i=0;i<17;i++)rows[i]={5u,unsigned(i),0,i==8?1:0};
 D2UnitStrc unit;UNITS_InitializeSequence(&unit);
 assert(unit.dwSeqFrameCount==4352&&unit.dwSeqFrame==0&&unit.dwSeqSpeed==256&&unit.dwFrameCountPrecise==4352&&unit.dwSeqMode==5&&unit.nActionFrame==0);
 printf("[");bool first=true;int total=0;
 for(int speed:{0,1,64,128,175,248,256,384,512,768,1024,4352})
 for(int start:{0,255,256,1791,1792,2047,2048,2303,2304,4096,4351})
 for(int remaining:{1,256,1024,4352}){
  unit={};UNITS_InitializeSequence(&unit);unit.dwSeqFrame=start;unit.dwSeqSpeed=speed;unit.dwFrameCountPrecise=remaining;
  for(int step=0;step<40&&!UNITS_IsAtEndOfFrameCycle(&unit);step++){
   unsigned old=unit.dwSeqFrame;int before=unit.dwFrameCountPrecise;
   UNITS_StopSequence(&unit);assert(unit.dwSeqMode==5);
   printf("%s[%d,%u,%d,%u,%d,%d]",first?"":",",speed,old,before,unit.dwSeqFrame,unit.dwFrameCountPrecise,unit.nActionFrame);first=false;total++;
  }
 }
 printf("]\n");assert(total>3000);
}
'''
    generated = BASE / 'grizzly-sequence-source-probe.cpp'
    generated.write_text(cpp)
    binary = generated.with_suffix('')
    with (BASE / 'grizzly-sequence-compiler.log').open('wb') as log:
        subprocess.run(['g++', '-std=c++17', '-Wall', '-Wextra', '-Werror', '-O1', '-g',
                        '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)],
                       stdout=log, stderr=subprocess.STDOUT, check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'grizzly-sequence-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-2000:]
    comparisons = json.loads(result.stdout)
    fixture = BASE / 'grizzly-sequence-source-probe.json'
    fixture.write_text(json.dumps(comparisons, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], installed=False,
        handlerEnabled=False, nativeConsumer=False, preflightSha256=sha(preflight_path),
        sourceHashes=hashes, unchangedRoutineHashes=routines, sequence=rows, animation=animation,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(comparisons),
                     generatedSourceSha256=sha(generated), sanitizers='ASan+UBSan'),
        established=[
            'Complete initializer,advance,event lookup and end test execute with17current A2rows and action1at index8. Initial point0,remaining4352,speed256and animation mode5are distinct from outer monster mode14.',
            'Supplied speed matrix includes current metadata248,slow/fast/fractional positions,wrap and zero-speed controls. Advance returns the last crossed event;outer consumer must consume the current action before advancing again. Existing parameterized sentry clock is a reuse candidate.',
        ], limits=[
            'Table lookup,ordinary-animation branch and frame bonus are explicit services;only sequence clock branches execute. Synthetic starting fractions/remaining/speeds are not asserted reachable gameplay states.',
            'Attack-rate selection,sequence entry/start32,mode14callbacks,do2release/event timing,live world geometry and saved cancellation remain separate acceptance. No native Grizzly action or learned handler is claimed.',
        ])
    path = ROOT / 'tools/reference/grizzly-sequence-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Grizzly sequence:{len(comparisons)}complete source clock steps; sanitizers clean; native action pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
