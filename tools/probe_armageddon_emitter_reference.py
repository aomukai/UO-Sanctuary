#!/usr/bin/env python3
"""Compose complete do146 with actual source seed/reset/random wrappers."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_druid_weather_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/druid-weather-source-evidence.json').read_text())
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

    collision_path='source/D2Common/include/D2Collision.h'
    collision_raw=(STAGE/'d2moo-pinned-source'/collision_path).read_bytes()
    assert hashlib.sha1(b'blob '+str(len(collision_raw)).encode()+b'\0'+collision_raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==collision_path)
    hashes[collision_path]=hashlib.sha256(collision_raw).hexdigest()
    collision=collision_raw.decode()
    bits={name:int(re.search(r'\b'+name+r'\s*=\s*(0x[0-9A-Fa-f]+)',collision).group(1),16) for name in ['COLLIDE_DOOR','COLLIDE_MISSILE_BARRIER','COLLIDE_WALL']}
    assert bits['COLLIDE_DOOR']|bits['COLLIDE_MISSILE_BARRIER']|bits['COLLIDE_WALL']==0x805
    cpp = (STAGE / 'druid-weather-source-probe.cpp').read_text().split('void reset(int id)', 1)[0]
    body = routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo146_Unused')
    assert cpp.count(body.rstrip()) == 1 and routines['SKILLS_SrvDo146_Unused'] == parent['compiledUnchangedRoutines']['SKILLS_SrvDo146_Unused']
    cpp = cpp.replace('struct D2UnitStrc{', 'struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};\nstruct D2UnitStrc{', 1)
    cpp = cpp.replace('uint32_t pSeed=73;', 'D2SeedStrc pSeed{};', 1)
    for pattern in [r'uint32_t ITEMS_RollRandomNumber\(uint32_t\* p\)\{[^}]*\}',
                    r'void SEED_InitLowSeed\(uint32_t\* p,int v\)\{[^}]*\}',
                    r'int ITEMS_RollLimitedRandomNumber\(uint32_t\* p,int n\)\{[^}]*\}']:
        cpp, count = re.subn(pattern, '', cpp)
        assert count == 1, pattern
    insertion = cpp.index('D2SkillsTxt* SKILLS_GetSkillsTxtRecord(')
    rng = ''
    for relative, names in [
        ('source/D2Common/include/D2Seed.h', ['SEED_RollRandomNumber', 'SEED_RollLimitedRandomNumber']),
        ('source/D2Common/src/D2Seed.cpp', ['SEED_InitLowSeed']),
        ('source/D2Game/src/ITEMS/Items.cpp', ['ITEMS_RollRandomNumber', 'ITEMS_RollLimitedRandomNumber'])]:
        for name in names:
            rng += routine(relative, name).replace('0x6AC690C5i64', '0x6AC690C5ULL')
    cpp = cpp[:insertion] + rng + cpp[insertion:]
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int memory:{0,1,41,65535,123456,999999})for(int gate:{0,1,2})for(int success:{1,3,5,6})for(int rank:{1,21}){
actor={};used={};used.id=lookupId=249;used.param=memory;row={};row.nAuraState=145;row.dwAuraRangeCalc=8;row.dwParam[3]=6;room={};hasRow=hasHighest=hasRoom=stateOn=hasList=true;selection=577;
for(int phase=0;phase<5;phase++){
creates=notifies=createX=createY=attempts=0;barrierAt=gate==0?success:0;roomAt=gate==1?success:0;maskAt=gate==2?success:0;events.clear();deleted.clear();int before=used.param;
int result=SKILLS_SrvDo146_Unused(&game,&actor,249,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%llu,%d,%d,%d,%d]",first?"":",",memory,gate,success,rank,phase,before,result,used.param,(unsigned long long)actor.pSeed.lSeed,attempts,creates,createX,createY);first=false;
}}
printf("]\n");}
'''
    path = STAGE / 'armageddon-emitter-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'armageddon-emitter-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 720

    def draw(seed):
        seed = 0x6AC690C5*(seed & 0xFFFFFFFF)+(seed >> 32)
        return seed, seed & 0xFFFFFFFF

    for initial, gate, success, rank, phase, before, result, memory, seed, attempts, creates, x, y in data:
        expected, raw = draw((666 << 32) | before)
        assert memory == raw % 1000000
        for _ in range(min(success, 5)):
            expected, rx = draw(expected); expected, ry = draw(expected)
        assert seed == expected and attempts == min(success, 5) and creates == result == int(success <= 5)
        assert (x, y) == ((40+rx%17-8, 50+ry%17-8) if success <= 5 else (0, 0))
    fixture = STAGE / 'armageddon-emitter-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes=hashes, compiledUnchangedRoutines=routines, radialBarrierMask=0x805,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete state146 callback composes actual source low-seed reset, seed generator and Items random wrappers across720five-event-chain contexts with six initial memories, three placement gate families, first/third/fifth/failure-sixth acceptance and ranks1/21.',
                        'Each event resets the caster stream from saved memory with high666, stores its first draw modulo1000000 before coordinate draws, then consumes two inclusive17-way offsets per attempted site. Five-event chains preserve exact memory, coordinates and final seed.',
                        'At most5attempts occur; any accepted site triggers one declared null-return allocation and still returns success. Completely rejected placements create nothing while advancing saved memory and coordinate RNG.'
                    ], services=[
                        'Rows, state/highest-skill presence, radius, room/town/barrier/mask, missile allocation/client/event services remain declared parent fixtures. Pinned radial barrier0x805 includes doors/missile barrier/walls, not the missile-footprint0x40 layer. Global allocation/owner RNG interleaving and modern executable/geometry are not certified.',
                        'Native emitter comparisons, placement/natural actors/damage/input/resources/lifecycle/reloads and class acceptance remain separate. No native handler or final asset is certified by this source probe.'
                    ], next='Compare actual Armageddon emitter memory/coordinates/seed with these vectors using controlled placement gates, alongside separate real native terrain/emission acceptance.')
    out = ROOT / 'tools/reference/armageddon-emitter-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Armageddon:720complete source callback/actual RNG five-event-chain contexts pass.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
