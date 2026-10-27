#!/usr/bin/env python3
"""Reuse the complete checked unarmed melee fill/critical composition for Fenris."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    parent = json.loads((ROOT / 'tools/reference/spirit-wolf-hit-source-evidence.json').read_text())
    cpppath = BASE / 'spirit-wolf-hit-source-probe.cpp'
    assert sha(cpppath) == parent['generatedSourceSha256']
    for name, digest in parent['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    monsters = ROOT / 'tools/reference/d2r-named-equipment/json/monstats.json'
    assert sha(monsters) == parent['pinnedMonsterHash']
    assert json.loads(monsters.read_text())['fenris']['Crit'] == 5
    cpp = cpppath.read_text().split('int main()', 1)[0]
    cpp += r'''
int main(){D2GameStrc game;bool first=true;int endpoints[4][2]={{7,12},{161,193},{0,0},{4200,4200}};printf("[");
for(auto& pair:endpoints)for(int percent:{-200,-90,-33,0,25,100,295,395,435})for(int typed:{0,1})for(int roll:{0,1})for(int critical:{0,4,5,99}){
D2UnitStrc a,d;a.dwUnitType=d.dwUnitType=UNIT_MONSTER;a.dwClassId=421;a.pInventory=nullptr;a.armed=false;
a.stats[STAT_MINDAMAGE]=pair[0];a.stats[STAT_MAXDAMAGE]=pair[1];a.stats[STAT_DAMAGEPERCENT]=percent;
if(typed){a.stats[STAT_FIREMINDAM]=2;a.stats[STAT_FIREMAXDAM]=6;a.stats[STAT_LIGHTMINDAM]=3;a.stats[STAT_LIGHTMAXDAM]=7;a.stats[STAT_COLDMINDAM]=5;a.stats[STAT_COLDMAXDAM]=8;a.stats[STAT_COLDLENGTH]=31;}
high=roll;criticalRoll=critical;hitClassResult=0;draws=events=hitclasses=overlays=0;bounds.clear();
D2DamageStrc damage;damage.wResultFlags=DAMAGERESULTFLAG_SUCCESSFULHIT;SUNITDMG_FillDamageValues(&game,&a,&d,&damage,0,128);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",pair[0],pair[1],percent,typed,roll,critical,damage.dwPhysDamage,damage.dwFireDamage,damage.dwLtngDamage,damage.dwColdDamage,damage.dwColdLen,events);first=false;
bool sep=false;for(int bound:bounds){printf("%s%d",sep?",":"",bound);sep=true;}printf("]]");
}printf("]\n");}
'''
    generated = BASE / 'dire-wolf-hit-source-probe.cpp'; generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'dire-wolf-hit-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-3000:]
    cases = json.loads(result.stdout); assert len(cases) == 576
    for minimum, maximum, percent, typed, high, critical, physical, fire, lightning, cold, length, events, bounds in cases:
        lo, hi = max(1, minimum)*256, max(2, maximum)*256
        if hi <= lo:
            hi = lo + 256
        def percentage(value, bonus):
            if value > 1048576:
                return bonus*(value//100)
            return int(value*bonus/100)
        lo += percentage(lo, max(-90, percent)); hi += percentage(hi, max(-90, percent))
        expected_bounds = [hi-lo] if hi>lo else []
        expected = [lo+(hi-lo-1 if high and hi>lo else 0),0,0,0]
        if typed:
            for i,(low,upper) in enumerate([(2,6),(3,7),(5,8)],1):
                expected[i]=low*256+((upper-low)*256-1 if high else 0)
                expected_bounds.append((upper-low)*256)
        expected_bounds.append(-1)
        if critical < 5:
            expected = [2*x for x in expected]
        assert [physical,fire,lightning,cold,length,events,bounds] == [*expected,31 if typed else 0,0,expected_bounds]
    fixture = BASE / 'dire-wolf-hit-source-probe.json'; fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=parent['legacyCommit'], sourceHashes=parent['sourceHashes'], unchangedRoutineHashes=parent['unchangedRoutineHashes'],
        reusedCompositionSha256=sha(cpppath), pinnedMonsterHash=sha(monsters), generatedSourceSha256=sha(generated),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture),cases=576),sanitizers='ASan+UBSan exit0 with empty stderr',
        established=['Complete ordinary monster fill and critical composition checks physical-first FP256 rolling, -90 percent floor, equal-endpoint widening, large-value percentage truncation, optional fire/lightning/cold in that order, exclusive bounds and Crit5 doubling of all channels.',
            'Rank1 and rank28 physical endpoints, zero-floor controls and4200/equal endpoints are supplied to the complete routine. Percent controls include295copied Grizzly,395with rage and435with an additional aura. Source monster Strength/weapon/item bonuses are zero; owner/native-skeleton attributes are not inherited.'],
        limits=['This is bounded packet/source evidence. Actual world hit/block/reach, physical mitigation, shared events/life commitment, rage/animation/state expiry and modern executable equivalence require native acceptance. Other typed stats, equipment/sunder, full HP/death/event ordering and critical visual/hit-class flags remain separate.'])
    path=ROOT/'tools/reference/dire-wolf-hit-source-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
    if check:
        assert path.read_text()==text
    else:
        path.write_text(text)
    print('Fenris physical/typed packet:576complete melee-fill/critical contexts pass ASan+UBSan.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
