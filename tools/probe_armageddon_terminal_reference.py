#!/usr/bin/env python3
"""Execute complete Armageddon terminal callback, router and stationary lifetime."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_fire_blast_landing_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/fire-blast-landing-engine-evidence.json').read_text())
    preflight = json.loads((ROOT / 'tools/reference/druid-weather-preflight.json').read_text())
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
        return body + '\n'

    cpp = (STAGE / 'fire-blast-landing-source-probe.cpp').read_text().split('void reset(int kind)', 1)[0]

    def replace(before, after):
        nonlocal cpp
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)

    # Execute unchanged reused bodies, replacing only the previously unselected callback stub.
    for relative, names in [
        ('source/D2Game/src/SKILLS/Skills.cpp', ['D2GAME_CreateMissile_6FD115E0', 'sub_6FD10140', 'sub_6FD10200']),
        ('source/D2Game/src/SKILLS/SkillAss.cpp', ['sub_6FCF5DE0']),
        ('source/D2Game/src/MISSILES/MissMode.cpp', ['MISSMODE_SrvDmgHitHandler', 'MISSMODE_HandleMissileCollision', 'MISSMODE_SrvDo01_BasicMissile'])]:
        for name in names:
            body = routine(relative, name)
            assert cpp.count(body.rstrip()) == 1 and routines[name] == parent['compiledUnchangedRoutines'][name]
    replace('int32_t MISSMODE_SrvHit56_ArmageddonControl(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return 3;}',
            routine('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvHit56_ArmageddonControl'))
    replace('assert(id==251 && rank>0);return f;', 'assert(id==249 && rank>0);return f;')
    replace('fills++;d->dwFireDamage=p->fire;', 'fills++;d->dwPhysDamage=159;d->dwFireDamage=p->fire;')
    replace('std::vector<int> resultFlags,fireValues;', 'std::vector<int> resultFlags,fireValues,physicalValues;int areaX=0,areaY=0;')
    replace('commits++;resultFlags.push_back(d->wResultFlags);', 'commits++;physicalValues.push_back(d->dwPhysDamage);resultFlags.push_back(d->wResultFlags);')
    replace('areas++;filter=f;', 'areas++;areaX=x;areaY=y;filter=f;')
    cpp += r'''
void reset(){owner={};parent={};child={};target={};other={};owner.dwUnitType=0;target.dwUnitType=other.dwUnitType=1;other.dwUnitId=20;
parent.dwClassId=577;parent.skill=249;parent.frame=parent.activation=25;parent.dwMissileMode=0;parent.path.velocity=0;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;
row={};row.wSrvHitFunc=56;row.wHitSubMissile[0]=623;row.dwHitPar[0]=4;row.nAlwaysExplode=row.nCollideKill=0;row.dwMissileFlags=0;
hasRecord=hasSkill=hasOwner=hasRoom=factory=true;creates=fills=flags=areas=events=commits=defenses=moves=finds=frameWrites=ar=0;
filter=areaRadius=areaX=areaY=0;noAura=-1;moveResult=1;victims={&target,&other};resultFlags.clear();fireValues.clear();physicalValues.clear();captured={};skill.dwAuraRangeCalc=8;}
void emit(){printf(",%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",creates,fills,areas,filter,noAura,areaRadius,events,commits,defenses,captured.dwFlags,captured.nMissile,captured.nSkill,captured.nSkillLevel,captured.nX,captured.nY,areaX,areaY,flags,moves,finds,parent.frame);printf(",[");for(size_t i=0;i<resultFlags.size();i++)printf("%s[%d,%d,%d]",i?",":"",resultFlags[i],physicalValues[i],fireValues[i]);printf("]");}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("{\"callbacks\":[");bool first=true;
for(int guard=0;guard<5;guard++)for(int radius:{-1,0,4})for(int fallback:{0,8})for(int sub:{-1,623})for(int victim:{0,1})for(int rank:{1,21})for(int allocate:{0,1}){
reset();hasRecord=guard!=1;hasOwner=guard!=2;hasSkill=guard!=3;row.dwHitPar[0]=radius;skill.dwAuraRangeCalc=fallback;row.wHitSubMissile[0]=sub;parent.rank=rank;factory=allocate;
int result=MISSMODE_SrvHit56_ArmageddonControl(&game,guard==4?nullptr:&parent,victim?&target:nullptr);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d",first?"":",",guard,radius,fallback,sub,victim,rank,allocate,result);first=false;emit();printf("]");}
printf("],\"routes\":[");first=true;
for(int victim:{0,1})for(int ending:{0,1})for(int prior:{0,1})for(int defense:{0,1,2,4,8,16}){
reset();if(prior){parent.md.nOwnerType=1;parent.md.dwOwnerGUID=10;}target.defense=defense;
int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,ending);
printf("%s[%d,%d,%d,%d,%d",first?"":",",victim,ending,prior,defense,result);first=false;emit();printf("]");}
printf("],\"lifetime\":[");first=true;reset();for(int frame=0;frame<25;frame++){
int before=commits;int result=MISSMODE_SrvDo01_BasicMissile(&game,&parent);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",frame,result,parent.frame,creates,fills,commits-before,moves,finds);first=false;}
printf("]}\n");}
'''
    path = STAGE / 'armageddon-terminal-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'armageddon-terminal-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)

    def verify(tail, active, filled, child, radius=4, rank=1, defense=0):
        creates, fills, areas, filter_, no_aura, range_, events, commits, defenses, flags, mid, sid, level, x, y, ax, ay, dmgflags, moves, finds, frame, packets = tail
        assert [creates, fills, areas, events, commits, defenses, dmgflags, moves, finds, frame] == [int(child), int(filled), int(active), (1+int(defense==0))*int(active), 2*int(active), 2*int(active), 0, 0, 0, 25]
        assert [filter_, no_aura, range_, ax, ay] == ([0x8583, 0, radius, 40, 70] if active else [0, -1, 0, 0, 0])
        assert [flags, mid, sid, level, x, y] == ([1, 623, 249, rank, 40, 70] if child else [0, 0, 0, 0, 0, 0])
        assert packets == ([[{0:5,1:20,2:100,4:132,8:4,16:4}[defense],159,3141],[5,159,3141]] if active else [])

    for guard, radius, fallback, sub, victim, rank, allocate, result, *tail in data['callbacks']:
        filled = guard not in (1, 2, 4)
        active = filled and (radius > 0 or guard != 3)
        assert result == 1
        verify(tail, active, filled, active and sub >= 0, radius if radius > 0 else max(fallback, 1), rank)
    for victim, ending, prior, defense, result, *tail in data['routes']:
        # Mode0 rejects unit contacts before hit56; only terminal null-unit routing dispatches it.
        active = not victim and ending
        assert result == (2 if active else 1)
        verify(tail, active, active, active, defense=defense)
    for frame, result, current, creates, fills, commits, moves, finds in data['lifetime']:
        terminal = frame == 24
        assert [result, current, creates, fills, commits, moves, finds] == [2 if terminal else 1, 24-frame, int(terminal), int(terminal), 2*int(terminal), 0, 0]
    counts = {key: len(rows) for key, rows in data.items()}
    assert counts == dict(callbacks=480, routes=48, lifetime=25)
    fixture = STAGE / 'armageddon-terminal-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    pinnedInputHashes=preflight['pinnedInputHashes'], sourceHashes=hashes, compiledUnchangedRoutines=routines,
                    inheritedServices='tools/reference/fire-blast-landing-engine-evidence.json',
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), caseCounts=counts),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete hit56, shared coordinate factory, hit router, basic update and copied-area/impact wrappers execute. Current control577 is stationary/no unit collision, decrements25frames and invokes one terminal area/child callback on its25th body call.',
                        'Hit56 requires missile/row/owner, fills one captured physical/fire packet and uses positive radius4 without requesting a skill record. Nonpositive radius falls back to max(owner aura formula,1); absent skill then suppresses area/child after the fill.',
                        'Owner-sourced terminal area at impact coordinates uses shared default0x8583 and bCheckMonAuraFlag0. Each declared recipient receives its own159physical/3141fire copy and individual defense flags. Current mode0 rejects unit contacts before hit56; terminal null-unit dispatch is independent of supplied prior ids.',
                        'Unlike Fire Blast, a direct hit56 invocation does not reject a supplied non-null victim; current mode0 routing never dispatches that artificial contact. A configured child623 is requested through flags1 coordinate factory with owner, captured rank and impact coordinates. Callback success/notification do not depend on child allocation success.'
                    ], services=[
                        'Legacy row/formula/owner/room/path/collision/allocator and packet-reader services remain declared bounded fixtures; physical159/fire3141 are sentinels, not the skill damage curve. Actual source damage captures are separate.',
                        'Area enumeration and individual defense/stat/critical/event/health are declared services. Full packet-copy/impact/router bodies execute; source room/filter/search geometry and native health/lifecycle require separate acceptance.',
                        'Generic current-row factory/seed/capture, field623 Collision1/CollideType8/37frames/DamageRate41, native casting/weather/actors/input/reloads and modern executable/client remain pending. No Armageddon handler or final assets are enabled.'
                    ], next='Compose control/child generic factory and field collision/DamageRate41 with existing Firestorm ground fields, then native Armageddon after Hurricane exact-package installation.')
    out = ROOT / 'tools/reference/armageddon-terminal-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Armageddon terminal:', counts, 'complete callback/router/lifetime cases pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
