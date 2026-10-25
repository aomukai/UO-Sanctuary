#!/usr/bin/env python3
"""Compose Death Sentry's direct S2 action2 clock using existing source bodies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_wake_of_fire_clock_reference.py'), '--check'],
                   check=True, stdout=subprocess.DEVNULL)
    parent_path = ROOT / 'tools/reference/wake-of-fire-clock-source-evidence.json'
    parent = json.loads(parent_path.read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    animation_path = ROOT / 'tools/reference/d2r-local-sentry-animation-evidence.json'
    animation = next(row for row in json.loads(animation_path.read_text())['records'] if row['code'] == 'LGS2HTH')
    assert animation == dict(code='LGS2HTH', frames=15, speed=256, events=[[0, 2]])
    table_dir = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((table_dir / 'sources.json').read_text())
    pins, tables = {}, {}
    for name in ['monstats', 'monstats2', 'skills']:
        raw = (table_dir / 'json' / (name + '.json')).read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files']['json/' + name + '.json']['sha256']
        tables[name] = json.loads(raw)
    monster, model = tables['monstats']['deathsentry'], tables['monstats2']['deathsentry']
    skill = next(x for x in tables['skills'].values() if x.get('*Id') == 312)
    assert monster['Code'] == 'lg' and monster['aidel'] == 15 and not monster.get('SplEndGeneric') and not monster.get('SplGetModeChart')
    assert model['mS2'] == 1 and not model.get('S2mv') and model['BaseW'] == 'hth'
    assert skill['srvdofunc'] == 55 and skill['anim'] == 'SQ'

    # Retain complete verified entry/scheduler/action/end/neutral routines.
    # NPC312's skill-table SQ label does not replace the mode9 hardcoded in
    # the separately executed older AI104. This probe supplies that mode.
    base = (STAGE / 'wake-of-fire-clock-source-probe.cpp').read_text().split('int main()', 1)[0]
    relative = 'source/D2Game/src/AI/AiTactics.cpp'
    raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    source = raw.decode()
    match = re.search(r'^void __fastcall AITACTICS_Idle\([^;\n]*\)\s*\n\{', source, re.M)
    assert match
    end, depth = source.index('{', match.start()) + 1, 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    idle = source[match.start():end]
    base += '\nvoid D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int arg){assert(type==EVENTTYPE_AITHINK&&arg==0);nextAi=0;}\n' + idle + '\n'
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
for(int idle:{0,1}){D2GameStrc g;D2UnitStrc u;nextAi=0;AITACTICS_Idle(&g,&u,idle);assert(nextAi==101);}
printf("[");bool first=true;
for(int speed:{0,128,180,256,341,512})for(int initial:{0,10,25,100})for(int stun:{0,1})for(int start:{0,1}){
 D2GameStrc game;Anim animation;animation.pFrameFlags[0]=2;MonsterData data;D2UnitStrc actor;
 actor.pAnimData=&animation;actor.pMonsterData=&data;actor.stunned=stun;actor.wAnimSpeed=speed;actor.dwFrameCountPrecise=15*256;
 D2ModeChangeStrc change{&actor,9};nextAi=initial?100+initial:0;events.clear();used=true;usedSkill.flags=0;starts=dispatches=stops=0;startResult=start;
 int entered=sub_6FC645E0(&game,&change);assert(entered==1&&actor.dwAnimMode==9&&starts==1);
 int bonus=UNITS_GetFrameBonus(&actor);assert(!bonus);sub_6FCBCE70(&game,&actor);
 auto schedule=events;int shot=-1,end=-1;
 for(auto e:schedule){game.dwGameFrame=e.frame;
  if(e.type==EVENTTYPE_MODECHANGE){assert(e.arg==2);shot=e.frame-100;D2GAME_MONSTERS_AiFunction01_6FC65080(&game,&actor,e.arg,e.index);}
  else{assert(e.type==EVENTTYPE_ENDANIM);end=e.frame-100;D2GAME_MONSTERS_AiFunction02_6FC65150(&game,&actor,0,0);}
 }
 assert(actor.dwAnimMode==1&&dispatches==int(shot>=0)&&stops==dispatches);
 if(speed==256&&!stun&&!initial)assert(shot==1&&end==15&&nextAi==130);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",speed,initial,stun,start,entered,shot,end,nextAi-100,dispatches,stops);first=false;
}printf("]\n");}
'''
    path = STAGE / 'death-sentry-clock-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-Wno-multichar', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'death-sentry-clock-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
    rows = json.loads(run.stdout)
    clocks = {0: (-1, 1), 128: (1, 30), 180: (1, 22), 256: (1, 15), 341: (1, 12), 512: (1, 8)}
    assert len(rows) == 96
    for speed, initial, stun, start, entered, shot, end, ai, dispatches, stops in rows:
        assert (shot, end) == clocks[speed]
        assert ai == (initial if initial > end else end + (45 if stun else 15))
    fixture = STAGE / 'death-sentry-clock-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    hashes, routines = dict(parent['sourceHashes']), dict(parent['routineHashes'])
    hashes[relative] = hashlib.sha256(raw).hexdigest()
    routines['AITACTICS_Idle'] = hashlib.sha256(idle.encode()).hexdigest()
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, legacyCommit=tree['sha'],
                    parentEvidence=str(parent_path.relative_to(ROOT)), sourceHashes=hashes, routineHashes=routines,
                    pinnedInputHashes=pins, localAnimationEvidence=str(animation_path.relative_to(ROOT)), animation=animation,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=96),
                    zeroIdleControls=2,
                    established=['Older AI104 explicitly requests corpse mode9, despite current NPC312/MonStats SQ labels; lightning remains mode14.',
                                 'Complete direct S2 entry/scheduler/action/end/neutral bodies dispatch current LG marker2 once at+1 and finish at+15 at supplied speed256; with no future AI deadline next think is+30.',
                                 'A false start result is ignored on the direct route. Six supplied rates, four AI deadlines and stunned/normal neutral delays execute. Complete AITACTICS_Idle maps zero frames to one.'],
                    limits=['Source data/current animation plus complete older bodies do not certify the modern executable or full live animation-rate graph.',
                            'The inherited harness retains declared record/layout, mode change, registry/state, used non-channelled skill, start/do and event-list services. The skill service records dispatch rather than invoking native corpse damage.',
                            'AI104, current corpse filtering, mode14 lightning and final retirement must still be composed in the native controller.'])
    out = ROOT / 'tools/reference/death-sentry-clock-source-evidence.json'
    if check:
        assert json.loads(out.read_text()) == evidence
    else:
        out.write_text(json.dumps(evidence, indent=2) + '\n')
    print('Death direct S2:96complete source contexts, marker2/action+1/end+15/AI+30;zero-idle1frame;sanitizers clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
