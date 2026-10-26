#!/usr/bin/env python3
"""Run complete Tornado pulse, copied-area wrappers and generic collision/router bodies."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/prepare_tornado_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_fire_blast_landing_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/fire-blast-landing-engine-evidence.json').read_text())
    preflight = json.loads((ROOT / 'tools/reference/tornado-preflight.json').read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, routines = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        source = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        body = source[match.start():end]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    base = (STAGE / 'fire-blast-landing-source-probe.cpp').read_text().split('void reset(int kind)', 1)[0]

    def replace(before, after):
        nonlocal base
        assert base.count(before) == 1, before
        base = base.replace(before, after, 1)

    replace('struct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1;}', 'struct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1,nTotalFrames=75,nCurrentFrame=75;}')
    replace('struct D2SkillsTxt{int dwAuraRangeCalc=5;}', 'struct D2SkillsTxt{int dwAuraRangeCalc=3,dwCalc[4]={0,0,0,15},dwAuraFilter=42371;}')
    replace('wHitSubMissile[1]={386},dwHitPar[1]={0};}', 'wHitSubMissile[1]={386},dwHitPar[1]={0},dwParam[2]={0,0};}')
    replace('bool unt=false;', 'bool unt=false,justHit=false;')
    replace('std::vector<D2UnitStrc*> victims;', 'int formulaCalls=0,areaOriginX=0,areaPhase=-1;bool contact=false;std::vector<int> physicalValues;std::vector<D2UnitStrc*> victims;')
    replace('assert(id==251 && rank>0);return f;', 'assert(id==245 && rank>0);formulaCalls++;return f;')
    replace('return state==STATE_UNINTERRUPTABLE && p->unt;', 'return state==STATE_UNINTERRUPTABLE?p->unt:state==STATE_JUSTHIT&&p->justHit;')
    replace('fills++;d->dwFireDamage=p->fire;', 'fills++;d->dwPhysDamage=p->fire;d->dwFireDamage=0;')
    replace('commits++;resultFlags.push_back(d->wResultFlags);fireValues.push_back(d->dwFireDamage);', 'commits++;resultFlags.push_back(d->wResultFlags);physicalValues.push_back(d->dwPhysDamage);fireValues.push_back(d->dwFireDamage);')
    replace('areas++;filter=f;', 'areas++;areaOriginX=o->x;areaPhase=moves;filter=f;')
    replace('int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){moves++;return moveResult;}', 'int sub_6FCBC930(D2GameStrc*,D2UnitStrc* p){moves++;p->x+=2;return moveResult;}')
    replace('int MISSILE_GetCurrentFrame(D2UnitStrc* p){return p->frame;}', 'int MISSILE_GetCurrentFrame(D2UnitStrc* p){return p->md.nCurrentFrame;}')
    replace('p->frame=f;frameWrites++;', 'p->frame=p->md.nCurrentFrame=f;frameWrites++;')
    replace('int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int){return 4;}', 'int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int){return contact?0x800:0;}')
    mode = 'source/D2Game/src/MISSILES/MissMode.cpp'
    common = 'source/D2Common/src/Units/Missile.cpp'
    # Verify complete reused packet-copy/impact/dispatcher and routing bodies.
    for relative, name in [('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD10140'),
                           ('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD10200'),
                           ('source/D2Game/src/SKILLS/SkillAss.cpp', 'sub_6FCF5DE0'),
                           (mode, 'MISSMODE_HandleMissileCollision'), (mode, 'MISSMODE_SrvDmgHitHandler')]:
        body = routine(relative, name)
        assert base.count(body) == 1 and routines[name] == parent['compiledUnchangedRoutines'][name]
    base += routine(common, 'MISSILE_GetRemainingFrames') + '\n' + routine(mode, 'MISSMODE_SrvDo27_Tornado')
    base += r'''
void reset(){stru_6FD2E5F8[3]={findCallback,0x804};owner={};parent={};target={};other={};owner.dwUnitType=0;target.dwUnitType=other.dwUnitType=1;other.dwUnitId=20;parent.dwClassId=478;parent.dwMissileMode=3;parent.skill=245;parent.rank=1;parent.md={};parent.pMissileData=&parent.md;parent.pDynamicPath=&parent.path;parent.path.velocity=1536;parent.path.flags=64;parent.activation=75;row={};row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];row.wSrvHitFunc=0;row.nAlwaysExplode=0;row.nNextHit=1;row.nNextDelay=25;skill={};hasRecord=hasSkill=hasOwner=hasRoom=true;creates=fills=flags=areas=events=commits=defenses=moves=finds=frameWrites=ar=formulaCalls=areaOriginX=0;areaPhase=-1;moveResult=1;noAura=-1;areaRadius=filter=0;contact=false;victims={&target,&other};resultFlags.clear();physicalValues.clear();}
void emit(){printf(",%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",parent.md.nCurrentFrame,moves,areas,areaOriginX,areaPhase,filter,noAura,areaRadius,formulaCalls,fills,flags,defenses,events,commits,finds,parent.x);for(size_t i=0;i<resultFlags.size();i++)printf("%s[%d,%d]",i?",":"",resultFlags[i],physicalValues[i]);printf("]");}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("{\"pulses\":[");
for(int guard=0;guard<5;guard++)for(int elapsed=0;elapsed<=75;elapsed++)for(int config:{0,1,2})for(int movement:{1,2}){
reset();hasOwner=guard!=1;hasRecord=guard!=2;hasSkill=guard!=3;parent.md.nCurrentFrame=75-elapsed;moveResult=movement;
if(config==1){row.dwParam[0]=7;row.dwParam[1]=4;}if(config==2){skill.dwCalc[3]=skill.dwAuraRangeCalc=0;}
int result=MISSMODE_SrvDo27_Tornado(&game,guard==4?nullptr:&parent);
printf("%s[%d,%d,%d,%d,%d",first?"":",",guard,elapsed,config,movement,result);first=false;emit();printf("]");}
printf("],\"historyDefense\":[");first=true;
for(int elapsed:{0,1,15})for(int prior:{0,1,2})for(int defense:{0,1,2,4,8,16})for(bool unt:{false,true}){
reset();parent.md.nCurrentFrame=75-elapsed;contact=true;target.defense=defense;target.unt=unt;
if(prior==1){parent.md.nOwnerType=1;parent.md.dwOwnerGUID=10;}if(prior==2){target.justHit=true;}
int result=MISSMODE_SrvDo27_Tornado(&game,&parent);
printf("%s[%d,%d,%d,%d,%d",first?"":",",elapsed,prior,defense,unt,result);first=false;emit();printf("]");}
printf("],\"lifetime\":[");reset();first=true;
for(int frame=0;frame<75;frame++){int old=areas;int result=MISSMODE_SrvDo27_Tornado(&game,&parent);printf("%s[%d,%d,%d,%d,%d]",first?"":",",frame,result,parent.md.nCurrentFrame,areas-old,parent.x);first=false;}
printf("]}\n");}
'''
    path = STAGE / 'tornado-pulse-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'tornado-pulse-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    counts = {key: len(value) for key, value in data.items()}
    assert counts == dict(pulses=2280, historyDefense=108, lifetime=75)
    for guard, elapsed, config, movement, result, current, moves, areas, origin, phase, filter_, no_aura, radius, formulas, fills, flags, defenses, events, commits, finds, x, packets in data['pulses']:
        valid = guard not in (2, 3, 4)
        interval, range_ = [(15, 3), (7, 4), (1, 1)][config]
        pulse = valid and elapsed % interval == 0
        assert result == (2 if not valid or movement == 2 or elapsed >= 74 else 1)
        assert (current, moves, areas, formulas, fills, flags, defenses, events, commits, finds, x) == (75-elapsed-int(valid and movement != 2), int(valid), int(pulse), int(valid and config != 1)+int(pulse and config != 1), int(pulse), 0, 2*int(pulse), 2*int(pulse), 2*int(pulse), 0, 40+2*int(valid))
        assert (origin, phase, filter_, no_aura, radius, packets) == ((40, 0, 42371, 0, range_, [[5, 3141], [5, 3141]]) if pulse else (0, -1, 0, -1, 0, []))
    for elapsed, prior, defense, unt, result, current, moves, areas, origin, phase, filter_, no_aura, radius, formulas, fills, flags, defenses, events, commits, finds, x, packets in data['historyDefense']:
        pulse, direct = elapsed % 15 == 0, prior == 0
        assert (result, moves, areas, fills, flags, defenses, events, commits, finds) == (1, 1, int(pulse), int(pulse)+int(direct), int(direct), 2*int(pulse), int(pulse)*(1+int(defense == 0)), 2*int(pulse), 1)
        expected = {0: 5, 1: 20, 2: 100, 4: 132, 8: 4, 16: 4}[defense]
        assert packets == ([[expected, 3141], [5, 3141]] if pulse else [])
    assert sum(row[3] for row in data['lifetime']) == 5
    for frame, result, current, pulses, x in data['lifetime']:
        assert (result, current, pulses, x) == (2 if frame == 74 else 1, 74-frame, int(frame % 15 == 0), 42+frame*2)
    fixture = STAGE / 'tornado-pulse-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=preflight['legacyCommit'], handlerEnabled=False, installed=False,
                    pinnedInputHashes=preflight['pinnedInputHashes'], sourceHashes=hashes, compiledUnchangedRoutines=routines,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), caseCounts=counts),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete do27/elapsed getter, copied-area/impact wrappers and generic collision/router compose. Every periodic packet is filled with null defender, receives row flags5/2, and dispatches filter42371/radius3 before generic movement.75ordinary body calls make5pulses at elapsed0,15,30,45,60; separate elapsed75/terminal-motion controls preserve area-before-terminal behavior.',
                        'Positive row interval/radius overrides bypass their formulas; nonpositive formula outputs clamp to1. Missing owner does not independently stop do27; missing skill/row or missile returns2. Native owner lifecycle still requires an explicit policy.',
                        'Each area recipient gets an independent copy of the raw3141physical packet. Individual defenses change only that copy, including the older high WeaponBlock-bit truncation. Area wrappers commit both packets and emit successful-hit events only when allowed; the shared original remains flags5.',
                        'An existing LastCollide identity or supplied JUSTHIT gate suppresses the separate direct router while the periodic area still executes and commits. The periodic wrappers neither query nor write the generic NextHit/LastCollide identity. Exact modern periodic/direct overlap remains unverified.'
                    ], services=[
                        'Declared formula, row, owner, room/path advancement, actor enumeration, defense/stat/critical/event/health services remain bounded. The supplied motion shifts X by2; area origin is recorded before that shift. Full source area search/zero-coordinate resolution and geometry do not execute.',
                        'Raw physical3141 is a damage-reader sentinel; full Tornado creator/getters, actual owner stats and damage rolls require separate proof. Direct SetDamageFlags is a recording service; no native direct damage/knockback or whole event equivalence is claimed.',
                        'Current75-frame factory/path, source room/filter order, native actors and learned casting/persistence/current-executable/client acceptance remain separate. No Tornado handler or assets are enabled.'
                    ], next='Compose count1/current75-frame initializer/factory/path/final seed and source-zero physical endpoints, then reuse saved charged motion with distinct periodic-area and direct-contact consumers after Volcano installation.')
    out = ROOT / 'tools/reference/tornado-pulse-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Tornado:', counts, 'complete pulse/copy/defense/update cases pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
