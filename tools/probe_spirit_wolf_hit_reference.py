#!/usr/bin/env python3
"""Reuse complete melee fill and monster critical code for supplied wolf packets."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}

    def routine(relative, name):
        raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest(); text = raw.decode()
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M); assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}'); end += 1
        body = text[match.start():end]; blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    pin = ROOT / 'tools/reference/d2r-named-equipment/json/monstats.json'
    manifest = json.loads((pin.parent.parent / 'sources.json').read_text())
    assert hashlib.sha256(pin.read_bytes()).hexdigest() == manifest['files']['json/monstats.json']['sha256']
    wolf = json.loads(pin.read_text())['spiritwolf']
    assert wolf['Crit'] == 5 and wolf['Align'] == 1 and not wolf.get('A1MinD') and not wolf.get('A1MaxD')
    cpp = (BASE / 'blade-shield-damage-source-probe.cpp').read_text().split('int main()', 1)[0]
    cpp = cpp.replace('#include <map>', '#include <map>\n#include <vector>')
    cpp = cpp.replace('struct D2MonStatsTxt{int wMonType=0;}', 'struct D2MonStatsTxt{int wMonType=0,nCrit=5;}')
    cpp = cpp.replace('int high=0,criticalRoll=99,draws=0,events=0;', 'int high=0,criticalRoll=99,draws=0,events=0,hitclasses=0,overlays=0,hitClassResult=0;std::vector<int>bounds;')
    cpp = cpp.replace('int ITEMS_RollLimitedRandomNumber(int*,int bound){draws++;', 'int ITEMS_RollLimitedRandomNumber(int*,int bound){bounds.push_back(bound);draws++;')
    cpp = cpp.replace('uint32_t ITEMS_RollRandomNumber(int*){draws++;', 'uint32_t ITEMS_RollRandomNumber(int*){bounds.push_back(-1);draws++;')
    cpp = cpp.replace('int STATLIST_GetUnitAlignment(D2UnitStrc*){return 0;}', 'int STATLIST_GetUnitAlignment(D2UnitStrc*){return UNIT_ALIGNMENT_GOOD;}')
    old = 'void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc* a,D2UnitStrc*,D2DamageStrc*){assert(a->dwUnitType==UNIT_PLAYER);}'
    assert cpp.count(old) == 1
    critical = routine('source/D2Game/src/MONSTER/MonsterMode.cpp', 'D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70')
    cpp = cpp.replace(old, 'int SUNITDMG_SetHitClass(D2DamageStrc*,unsigned n){assert(n==0x10);hitclasses++;return hitClassResult;}\nvoid UNITS_SetOverlay(D2UnitStrc*,int n,int){assert(n==54);overlays++;}\n' + critical)
    for name in ['SUNITDMG_ApplyDamageBonuses', 'SUNITDMG_RollDamageValueInRange', 'SUNITDMG_FillDamageValues']:
        assert cpp.count(routine('source/D2Game/src/UNIT/SUnitDmg.cpp', name)) == 1, name
    assert cpp.count(routine('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CalculatePercentage')) == 1
    cpp += r'''
int main(){D2GameStrc game;bool first=true;int endpoints[3][3]={{2,6,31},{343,359,875},{5,5,31}};printf("[");
for(int cold=0;cold<3;cold++)for(int minimum:{0,5})for(int percent:{0,100})for(int mastery:{0,100})for(int roll:{0,1})for(int critical:{0,4,5,99})for(int skip:{0,1})for(int marked:{0,1})for(int present:{0,1}){
D2UnitStrc a,d;a.dwUnitType=d.dwUnitType=UNIT_MONSTER;a.dwClassId=420;a.pInventory=nullptr;a.armed=false;
a.stats[STAT_MINDAMAGE]=minimum;a.stats[STAT_MAXDAMAGE]=minimum?minimum+7:0;a.stats[STAT_DAMAGEPERCENT]=percent;
a.stats[STAT_COLDMINDAM]=endpoints[cold][0];a.stats[STAT_COLDMAXDAM]=endpoints[cold][1];a.stats[STAT_COLDLENGTH]=endpoints[cold][2];a.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;
high=roll;criticalRoll=critical;hitClassResult=marked;draws=events=hitclasses=overlays=0;bounds.clear();
D2DamageStrc damage;damage.dwHitFlags=skip?DAMAGEHITFLAG_1:0;damage.wResultFlags=DAMAGERESULTFLAG_SUCCESSFULHIT;
SUNITDMG_FillDamageValues(&game,&a,present?&d:nullptr,&damage,0,128);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",cold,minimum,percent,mastery,roll,critical,skip,marked,present,damage.dwPhysDamage,damage.dwColdDamage,damage.dwColdLen,draws,hitclasses,overlays,events);first=false;
bool firstBound=true;for(int n:bounds){printf("%s%d",firstBound?"":",",n);firstBound=false;}printf("]]");
}printf("]\n");}
'''
    path = BASE / 'spirit-wolf-hit-source-probe.cpp'; path.write_text(cpp); binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'spirit-wolf-hit-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    cases = json.loads(result.stdout); assert len(cases) == 1536
    endpoints = [(2, 6, 31), (343, 359, 875), (5, 5, 31)]
    for cold, minimum, percent, mastery, high, critical, skip, marked, present, physical, ice, frames, draws, classes, overlays, events, bounds in cases:
        expected_bounds = []
        if not present:
            assert [physical, ice, frames, draws, classes, overlays, events, bounds] == [0, 0, 0, 0, 0, 0, 0, []]
            continue
        expected_physical = 0
        if not skip:
            lo = max(minimum, 1) * 256; hi = max(minimum + 7 if minimum else 0, 2) * 256
            lo += lo * percent // 100; hi += hi * percent // 100
            expected_physical = lo + (hi - lo - 1 if high else 0); expected_bounds.append(hi-lo)
        low, maximum, length = endpoints[cold]; lo, hi = low * 256, maximum * 256
        lo += lo * mastery // 100; hi += hi * mastery // 100
        expected_ice = lo
        if hi > lo:
            expected_ice += hi-lo-1 if high else 0; expected_bounds.append(hi-lo)
        expected_bounds.append(-1)
        doubled = critical < 5
        if doubled:
            expected_physical *= 2; expected_ice *= 2
        assert [physical, ice, frames, draws, classes, overlays, events, bounds] == [expected_physical, expected_ice, length, len(expected_bounds), int(doubled), int(doubled and not marked), 0, expected_bounds]
    fixture = BASE / 'spirit-wolf-hit-source-probe.json'; fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes=hashes, unchangedRoutineHashes=blocks, pinnedMonsterHash=hashlib.sha256(pin.read_bytes()).hexdigest(),
                    generatedSourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(cases)),
                    sanitizers='ASan/UBSan exit0 with empty runtime stderr.',
                    established=[
                        'Complete supplied ordinary monster melee fill rolls physical before cold in FP256, with exclusive upper bounds, then invokes the complete monster critical helper. Current Crit5 accepts rolls0..4 and doubles both physical and cold, leaving cold duration unchanged. Equal cold endpoints consume no cold roll.',
                        'In the inspected older body, unarmed raw physical0/0 is floored to1/2whole life before rolling. Explicit skip-physical flag1 controls remove that lane while retaining cold. This is a source distinction, not evidence the current executable retains the hidden physical floor.',
                        'Physical percentage controls affect the physical lane; actor cold mastery controls affect the cold lane. Source cold endpoints2/6/31 and user-correlated343/359/875 are supplied whole copied stats. No owner weapon/item stats, player melee event or hireling behavior is injected.',
                        'Source intrinsic critical requests hit class0x10 and optionally overlay54. Hit-class return/overlay services are declared and do not certify native effect rendering or all result-flag semantics.'
                    ], limits=[
                        'This record establishes source packet/critical components only. Bounded native cold-melee acceptance is recorded separately in spirit-wolf-world-native-evidence.json; full current result/block/mode/event parity, physical floor/critical timing and summon equipment stat transport remain separate.',
                        'Current physical-floor observation is inconclusive: the user cannot distinguish tiny physical damage from Hell monster health/regeneration. Do not record this as either proven cold-only damage or proven modern physical damage. Per-monster resistances remain authoritative; no blanket Hell physical multiplier is inferred.',
                        'The selected inventory/stat/classification/roll/critical metadata services are bounded. Good-aligned non-hireling NPCs with valid source row and supplied stats are covered; PvP, item events, nonzero physical/elemental conversion, extra typed stats and modern global RNG are not established.'
                    ])
    evidence['userExpectation'] = 'User believes Spirit Wolves deal100percent cold damage; the attempted physical-floor observation remains inconclusive.'
    path = ROOT / 'tools/reference/spirit-wolf-hit-source-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Spirit Wolf hit:1536 complete monster melee-fill/critical contexts; modern physical floor remains uncertain.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
