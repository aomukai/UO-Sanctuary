#!/usr/bin/env python3
"""Compose Wake's direct S2 scheduling, action callback and generic end handoff.

Reuses the complete S1 timing harness; full source routines remain unchanged.
Only the used-skill, non-channelled, nonmoving Wake route is exercised.
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_sentry_emergence_reference.py'), '--check'],
                   check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/sentry-emergence-source-evidence.json').read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, routines = dict(parent['sourceHashes']), dict(parent['routineHashes'])

    def extract(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    animation_evidence = json.loads((ROOT / 'tools/reference/d2r-local-sentry-animation-evidence.json').read_text())
    animation = next(x for x in animation_evidence['records'] if x['code'] == 'E9S2HTH')
    assert animation == dict(code='E9S2HTH', frames=2, speed=256, events=[[1, 1]])
    tables = ROOT / 'tools/reference/d2r-named-equipment/json'
    pins = {}
    manifest = json.loads((tables.parent / 'sources.json').read_text())
    for name in ['monstats', 'monstats2']:
        raw = (tables / f'{name}.json').read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        row = json.loads(raw)['wakeofdestruction']
        if name == 'monstats':
            assert row['Code'] == 'e9' and row['Sk1mode'] == 'S2' and row['aidel'] == 15 and not row.get('SplEndGeneric') and not row.get('SplGetModeChart')
        else:
            assert row['mS2'] == 1 and not row.get('S2mv') and row['BaseW'] == 'hth'

    base = (STAGE / 'sentry-emergence-source-probe.cpp').read_text().split('int main()', 1)[0]

    def replace(before, after):
        nonlocal base
        assert before in base, before
        base = base.replace(before, after, 1)

    replace('struct D2GameStrc{', 'struct D2SkillStrc{int flags=0;};bool used=true,startResult=true;int starts=0,dispatches=0,stops=0;D2SkillStrc usedSkill;\nstruct D2GameStrc{')
    replace('struct D2UnitStrc{', 'struct D2UnitStrc{int dwFlags=0,nActionFrame=0,dwUnitId=7;')
    replace('void UNITS_SetUsedSkill(D2UnitStrc*,void*){}void* UNITS_GetUsedSkill(D2UnitStrc*){return nullptr;}',
            'void UNITS_SetUsedSkill(D2UnitStrc*,void* p){assert(!p);used=false;}D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return used?&usedSkill:nullptr;}')
    replace('int sub_6FD12FD0(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}',
            'int sub_6FD12FD0(D2GameStrc*,D2UnitStrc*){starts++;return startResult;}')
    replace('assert(mode==8);static const D2MonModeCallbackTableStrc table{nullptr,nullptr,sub_6FC63680};',
            'assert(mode==9);static const D2MonModeCallbackTableStrc table{nullptr,sub_6FC64790,sub_6FC63680};')
    replace('const D2MonModeCallbackTableStrc* MONSTERMODE_GetCallbackTableRecord',
            'int32_t sub_6FC64790(D2GameStrc*,D2UnitStrc*);\nconst D2MonModeCallbackTableStrc* MONSTERMODE_GetCallbackTableRecord')
    base += r'''
constexpr int UNITFLAG_SKSRVDOFUNC=64,UNITFLAG_ISMERC=1<<15,STAT_LEVEL=0,MONSTER_QUILLRAT1=999;
int SKILLS_GetFlags(D2SkillStrc* p){return p->flags;}
void SKILLS_SetFlags(D2SkillStrc* p,int flags){p->flags=flags;}
void sub_6FD13410(D2GameStrc*,D2UnitStrc* p){dispatches++;p->dwFlags|=64;}
// StopSequence's frame mutation is a declared boundary in this sparse-event
// harness. It must not reschedule or dispatch another ordinary action here.
void UNITS_StopSequence(D2UnitStrc*){stops++;}
template<class... T> int sub_6FCBC930(T...){assert(false);return 0;}
template<class... T> int UNITS_IsAtEndOfFrameCycle(T...){assert(false);return 0;}
template<class... T> int D2Common_11050(T...){assert(false);return -1;}
template<class... T> int STATLIST_UnitGetStatValue(T...){assert(false);return 0;}
template<class... T> int sub_6FD11420(T...){assert(false);return 0;}
template<class... T> int D2GAME_GetMonsterBaseId_6FC64B10(T...){assert(false);return 0;}
template<class... T> int CLIENTS_GetUnitX(T...){assert(false);return 0;}
template<class... T> int CLIENTS_GetUnitY(T...){assert(false);return 0;}
template<class... T> int D2COMMON_10175_PathGetFirstPointX(T...){assert(false);return 0;}
template<class... T> int D2COMMON_10176_PathGetFirstPointY(T...){assert(false);return 0;}
template<class... T> int PATH_SetTargetUnit(T...){assert(false);return 0;}
template<class... T> int D2COMMON_10170_PathSetTargetPos(T...){assert(false);return 0;}
template<class... T> int ITEMS_RollRandomNumber(T...){assert(false);return 0;}
template<class... T> int sub_6FC62D90(T...){assert(false);return 0;}
template<class... T> int SUNITDMG_DrainItemDurability(T...){assert(false);return 0;}
template<class... T> int sub_6FCBC7E0(T...){assert(false);return 0;}
struct D2SeedStrc{int nHighSeed=0;};
template<class... T> int SEED_InitSeed(T...){assert(false);return 0;}
template<class... T> int SEED_InitLowSeed(T...){assert(false);return 0;}
struct Difficulty{int dwMonsterSkillBonus=0;};
Difficulty* DATATBLS_GetDifficultyLevelsTxtRecord(int){assert(false);return nullptr;}
'''
    # Fields referenced by guarded no-used-skill branches are ABI declarations;
    # all such services assert if called instead of inventing those mechanics.
    replace('int nBaseId=411,nSplEndGeneric=0,', 'int wAiParam[3][3]={};int nBaseId=411,nSplEndGeneric=0,')
    mode = 'source/D2Game/src/MONSTER/MonsterMode.cpp'
    base += extract(mode, 'sub_6FC64790')
    base += extract(mode, 'D2GAME_MONSTERS_AiFunction01_6FC65080')
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int speed:{0,128,180,256,341,512})for(int initial:{0,10,25,100})for(int stun:{0,1})for(int start:{0,1}){
 D2GameStrc game;Anim animation;animation.pFrameFlags[1]=1;MonsterData data;D2UnitStrc actor;
 actor.pAnimData=&animation;actor.pMonsterData=&data;actor.stunned=stun;actor.wAnimSpeed=speed;actor.dwFrameCountPrecise=512;
 D2ModeChangeStrc change{&actor,9};nextAi=initial?100+initial:0;events.clear();used=true;usedSkill.flags=0;starts=dispatches=stops=0;startResult=start;
 int entered=sub_6FC645E0(&game,&change);assert(entered==1&&actor.dwAnimMode==9&&starts==1);
 int bonus=UNITS_GetFrameBonus(&actor);assert(!bonus);sub_6FCBCE70(&game,&actor);
 auto schedule=events;int shot=-1,end=-1;
 for(auto e:schedule){game.dwGameFrame=e.frame;
  if(e.type==EVENTTYPE_MODECHANGE){assert(e.arg==1);shot=e.frame-100;D2GAME_MONSTERS_AiFunction01_6FC65080(&game,&actor,e.arg,e.index);}
  else{assert(e.type==EVENTTYPE_ENDANIM);end=e.frame-100;D2GAME_MONSTERS_AiFunction02_6FC65150(&game,&actor,0,0);}
 }
 assert(actor.dwAnimMode==1&&dispatches==int(shot>=0)&&stops==dispatches);
 if(speed==256&&!stun&&!initial)assert(shot==1&&end==2&&nextAi==117);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",speed,initial,stun,start,entered,shot,end,nextAi-100,dispatches,stops);first=false;
}printf("]\n");}
'''
    path = STAGE / 'wake-of-fire-clock-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-Wno-multichar', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'wake-of-fire-clock-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    rows = json.loads(run.stdout)
    assert len(rows) == 96
    clocks = {0: (-1, 1), 128: (2, 4), 180: (2, 3), 256: (1, 2), 341: (1, 2), 512: (-1, 2)}
    for speed, initial, stun, start, entered, shot, end, ai, dispatched, stops in rows:
        assert (shot, end) == clocks[speed]
        assert ai == (initial if initial > end else end + (45 if stun else 15))
    fixture = STAGE / 'wake-of-fire-clock-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(
        referenceVersion='3.3.93847', talentId='assassin.wakeoffire', handlerEnabled=False,
        legacyCommit=tree['sha'], sourceHashes=hashes, routineHashes=routines, pinnedInputHashes=pins,
        localAnimationEvidence='tools/reference/d2r-local-sentry-animation-evidence.json',
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=96),
        established=[
            'Current Wake uses direct nonmoving S2 and generic callbacks, rather than lightning mode14/seq_chargesentry. Complete generic entry calls start but ignores its result on this route.',
            'The complete ordinary scheduler, direct action dispatcher and generic used-skill callback issue one do at +1 frame and complete at +2 at the declared current metadata speed256. No sequence controller or third-phase release is used.',
            'Complete ENDANIM callback, generic end and neutral handoff preserve any existing AI deadline that is still in the future, even if it precedes end+15. With no future deadline they schedule end+15, or end+45 when stunned; speed256 with no deadline next thinks at +17.',
            'Six declared speed controls distinguish zero-speed expiry, slow repeated frame crossings and the high-speed skipped marker. Start success/failure, stun and four earlier/later AI deadlines are independently checked.'
        ], services=[
            'Animation rate is supplied, not derived from the full live monster rate/skill graph. Mode change, callback lookup, start/do, states, registry and event-list services remain checked boundaries.',
            'StopSequence is an explicit counted service. Only non-channelled used skills are exercised; no-used-skill and channelled branches assert through unused services. Full AI101, retirement/death, source monster event phase order and native combat are not composed here.',
            'This is source timing with current local metadata, not a complete current executable or rendered animation certificate.'
        ], sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/wake-of-fire-clock-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Wake clock evidence stale'
    else:
        out.write_text(encoded)
    print('Wake direct S2: 96 complete scheduler/action/end/neutral contexts; normal shot+1/end+2/AI+17; sanitizers clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
