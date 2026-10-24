#!/usr/bin/env python3
"""Execute the complete pinned event translation unit for actor-clock ordering."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes = {}

    def source(rel):
        raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
        hashes[rel] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    header = source('source/D2Game/include/GAME/Event.h')
    body = source('source/D2Game/src/GAME/Event.cpp')
    strip = lambda s: re.sub(r'^#(?:include|pragma)[^\n]*\n', '', s, flags=re.M)
    cpp = r'''#include <cassert>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <cstdio>
#include <vector>
#include <array>
#define __fastcall
#define __forceinline inline
#define D2_ASSERT assert
#define ARRAY_SIZE(x) (sizeof(x)/sizeof(x[0]))
#define D2_CALLOC_STRC_POOL(pool,T) static_cast<T*>(calloc(1,sizeof(T)))
#define D2_ALLOC_STRC_POOL(pool,T) static_cast<T*>(malloc(sizeof(T)))
#define D2_FREE_POOL(pool,p) free(p)
struct D2GameStrc;struct D2UnitStrc;
'''+strip(header)+r'''
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_MISSILE=3,UNIT_ITEM=4,UNIT_TYPES_COUNT=6,D2UnitInvalidGUID=-1,STATE_UNINTERRUPTABLE=1;
struct D2UnitStrc{int dwUnitType=0,dwUnitId=0;D2EventTimerStrc* timer=nullptr;};
struct D2GameStrc{int dwGameFrame=0;D2EventTimerQueueStrc* pTimerQueue=nullptr;void* pMemoryPool=nullptr;};
D2EventTimerStrc* SUNIT_GetTimerFromUnit(D2UnitStrc* u){assert(u);return u->timer;}
void SUNIT_SetTimerOnUnit(D2UnitStrc* u,D2EventTimerStrc* t){assert(u);u->timer=t;}
int STATES_CheckState(D2UnitStrc*,int){return 0;}void sub_6FCBDE90(D2UnitStrc*,int){assert(false);}
void Dispatch(D2GameStrc*,D2UnitStrc*,D2C_EventTypes,int,int);
void D2GAME_EVENTS_Callback_6FC81BD0(D2GameStrc* g,D2UnitStrc* u,D2C_EventTypes e,int a,int b){Dispatch(g,u,e,a,b);}
void MONSTERMODE_EventHandler(D2GameStrc* g,D2UnitStrc* u,D2C_EventTypes e,int a,int b){Dispatch(g,u,e,a,b);}
void D2GAME_OBJMODE_InvokeEventFunction_6FC75250(D2GameStrc* g,D2UnitStrc* u,D2C_EventTypes e){Dispatch(g,u,e,0,0);}
void MISSMODE_SrvDoHandler(D2GameStrc* g,D2UnitStrc* u,D2C_EventTypes e){Dispatch(g,u,e,0,0);}
void D2GAME_Items_EventsHandler_6FC4A460(D2GameStrc* g,D2UnitStrc* u,D2C_EventTypes e){Dispatch(g,u,e,0,0);}
'''+strip(body)+r'''
std::vector<std::array<int,4>> trace;D2UnitStrc units[8];int scenario=0,origin=0;
void Dispatch(D2GameStrc* g,D2UnitStrc* u,D2C_EventTypes event,int custom,int param){
 trace.push_back({g->dwGameFrame-origin,u->dwUnitId,int(event),custom});
 if(scenario==1 && u->dwUnitId==1 && event==EVENTTYPE_AITHINK){sub_6FC35570(g,&units[1],EVENTTYPE_MODECHANGE,0,0);sub_6FC35570(g,&units[3],EVENTTYPE_MODECHANGE,0,0);}
 if(scenario==2 && u->dwUnitId==1 && event==EVENTTYPE_MODECHANGE && custom==1){D2GAME_EVENTS_Delete_6FC34840(g,u,event,1);sub_6FC35570(g,&units[6],EVENTTYPE_MODECHANGE,2,0);}
 if(scenario==3 && u->dwUnitId==3 && event==EVENTTYPE_MODECHANGE){D2GAME_EVENTS_Delete_6FC34840(g,u,event,0);sub_6FC35570(g,&units[1],EVENTTYPE_MODECHANGE,0,0);}
 if(scenario==4 && custom==1){D2GAME_EVENTS_Delete_6FC34840(g,u,event,0);D2GAME_EVENTS_Delete_6FC34840(g,&units[6],event,0);}
 if(scenario==5 && custom==1){EVENT_SetEvent(g,u,EVENTTYPE_AITHINK,g->dwGameFrame,2,0);}
}
int main(){bool first=true;printf("[");for(int start:{63,64,1000})for(int which=0;which<7;which++){
 scenario=which;origin=start;trace.clear();D2GameStrc game;game.dwGameFrame=start;for(int i=0;i<8;i++)units[i]={i<5?i:UNIT_MONSTER,i,nullptr};EVENT_AllocEventQueue(&game);
 if(which==0)for(int type=0;type<5;type++){sub_6FC35570(&game,&units[type],EVENTTYPE_MODECHANGE,0,0);EVENT_SetEvent(&game,&units[type],EVENTTYPE_AITHINK,start+1,0,0);}
 if(which==1)EVENT_SetEvent(&game,&units[1],EVENTTYPE_AITHINK,start+1,0,0);
 if(which==2)sub_6FC35570(&game,&units[1],EVENTTYPE_MODECHANGE,1,0);
 if(which==3)sub_6FC35570(&game,&units[3],EVENTTYPE_MODECHANGE,0,0);
 if(which==4){sub_6FC35570(&game,&units[6],EVENTTYPE_MODECHANGE,2,0);sub_6FC35570(&game,&units[1],EVENTTYPE_MODECHANGE,1,0);}
 if(which==5)EVENT_SetEvent(&game,&units[1],EVENTTYPE_AITHINK,start,1,0);
 if(which==6){EVENT_SetEvent(&game,&units[1],EVENTTYPE_AITHINK,start+65,2,0);EVENT_SetEvent(&game,&units[1],EVENTTYPE_AITHINK,start+1,1,0);}
 for(int frame=1;frame<=66;frame++){game.dwGameFrame=start+frame;EVENT_IterateEvents(&game);}
 if(!first)printf(",");first=false;printf("{\"input\":[%d,%d],\"trace\":[",start,which);for(size_t i=0;i<trace.size();i++){auto x=trace[i];printf("%s[%d,%d,%d,%d]",i?",":"",x[0],x[1],x[2],x[3]);}printf("]}");
 for(auto& u:units)D2GAME_DeleteTimersOnUnit_6FC34A30(&game,&u);EVENT_FreeEventQueue(&game);
}printf("]\n");}
'''
    p = STAGE / 'source-events-probe.cpp'
    p.write_text(cpp)
    binary = p.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(p), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'source-events-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    data = json.loads(result.stdout)
    assert len(data) == 21
    for row in data:
        start, which = row['input']
        trace = row['trace']
        if which == 0:
            assert trace[:10] == [[1, t, e, 0] for t in [3, 0, 1, 2, 4] for e in [0, 2]]
        elif which == 1:
            assert trace == [[1, 1, 2, 0]] + [[f, t, 0, 0] for f in range(2, 67) for t in [3, 1]]
        elif which == 2:
            assert trace == [[1, 1, 0, 1]] + [[f, 6, 0, 2] for f in range(2, 67)]
        elif which == 3:
            assert trace == [[1, 3, 0, 0]] + [[f, 1, 0, 0] for f in range(1, 67)]
        elif which == 4:
            assert trace == [[1, 1, 0, 1]]
        elif which == 5:
            assert trace == [[1, 1, 2, 1], [2, 1, 2, 2]]
        elif which == 6:
            assert trace == [[1, 1, 2, 1], [65, 1, 2, 2]]
    fixture = STAGE / 'source-events-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes,
                    translationUnit='Complete unchanged Event.cpp; Event.h declarations/enums/records retained. Only includes/pragmas supplied by the host ABI.',
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), scenarios=21, framesPerScenario=66),
                    sanitizers='ASan/UBSan; successful exit and zero stderr required',
                    established=['Phase order is missile infinite/timed, player infinite/timed, monster infinite/timed, object infinite/timed, item infinite/timed.',
                                 'Monster timed AI that creates movement and following-missile infinite events runs both next frame; missile phase precedes monster movement. Infinite callbacks inserting at their own phase head defer the new event until next frame. A missile callback can insert a monster event that runs later in that same frame.',
                                 'Deleting the current event is deferred safely; deleting the next event advances the saved iterator. Timed events requested at/past current frame clamp to next frame. Bucket collisions64 frames apart do not fire early.'],
                    limits=['Allocator macros use host malloc/calloc/free; unit timer fields, state checks and game callbacks are declared services. Only ordinary uninterruptible-state=false unit contexts are exercised. The full queue implementation is executed, but this does not yet execute monster mode/AI/movement or missile combat callbacks.',
                            'Reconstructed legacy queue behavior is supplementary source evidence. It does not prove current D2R executable scheduling or native controller acceptance, and enables no learned skill.'])
    p = ROOT / 'tools/reference/source-events-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert p.read_text() == rendered
    else:
        p.write_text(rendered)
    print('Complete source event queue:21 scenarios/1,386 frames pass ordering/deletion/insertion/ring-bucket goldens and sanitizers.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
