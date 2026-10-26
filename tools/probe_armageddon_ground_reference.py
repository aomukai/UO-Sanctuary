#!/usr/bin/env python3
"""Compose Armageddon ground damage flags, rate-scaled MDR, health and modes."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_molten_boulder_impact_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/molten-boulder-impact-source-evidence.json').read_text())
    preflight = json.loads((ROOT / 'tools/reference/druid-weather-preflight.json').read_text())
    row = preflight['missiles']['armageddonfire']
    assert row['DamageRate'] == 41 and not any(row.get(k) for k in ['GetHit', 'SoftHit', 'KnockBack', 'NextHit', 'LastCollide'])
    cpp = (STAGE / 'molten-boulder-impact-source-probe.cpp').read_text().split('int main()', 1)[0]
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int raw:{1,40,56,256,1536})for(int rate:{0,41,1024})for(int mdr:{0,1,10})for(int resist:{-100,0,50,100})for(int defense:{0,1,2,4,8,16})for(int guard:{0,1}){
game={};actor={};target={};missile={};row={};mon2={};missileRow={};actor.dwUnitType=UNIT_PLAYER;actor.dwUnitId=77;actor.group=1;target.group=2;target.dwUnitId=99;actor.pGame=target.pGame=missile.pGame=&game;
game.pUnitList[0][77]=&actor;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=623;missile.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;missile.dwOwnerType=0;missile.dwOwnerGUID=77;missile.pSeed.nLowSeed=123;missile.pSeed.nHighSeed=666;
row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];target.stats[STAT_HITPOINTS]=25600;target.stats[STAT_FIRERESIST]=resist;target.stats[STAT_MAGIC_DAMAGE_REDUCTION]=mdr;missile.stats[STAT_DAMAGE_FRAMERATE]=rate;
mon2.dwModeFlags=gdwBitMasks[MONMODE_BLOCK]|gdwBitMasks[MONMODE_GETHIT]|gdwBitMasks[MONMODE_KNOCKBACK];blocked=defense;uninterruptible=guard;callbackCount=kills=modes=0;lastAttacker=nullptr;requested=-1;
D2DamageStrc damage{};damage.dwFireDamage=raw;MISSMODE_SetDamageFlags(&game,&missile,&target,&damage);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%llu,%d]",first?"":",",raw,rate,mdr,resist,defense,guard,target.stats[STAT_HITPOINTS],damage.dwFireDamage,damage.dwPiercePct,requested,(unsigned long long)missile.pSeed.lSeed,damage.wResultFlags);first=false;
}printf("]\n");}
'''
    path = STAGE / 'armageddon-ground-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'armageddon-ground-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 2160
    for raw, rate, mdr, resist, defense, guard, life, fire, recorded_rate, mode, seed, flags in data:
        reduction = mdr*256*(rate if rate > 0 else 1024)//1024
        damage = max(0, raw-reduction)*(100-resist)//100 if not defense else 0
        assert life == 25600-damage, (raw, rate, mdr, resist, defense, life, damage)
        assert recorded_rate == rate and mode == -1 and seed == ((666 << 32) | 123)
        assert flags & 4 == 0 and flags & 8 == 0, (defense, flags)
    fixture = STAGE / 'armageddon-ground-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=parent['legacyCommit'], handlerEnabled=False, installed=False,
                    pinnedInputHashes=preflight['pinnedInputHashes'], sourceHashes=parent['sourceHashes'],
                    compiledUnchangedRoutines={**parent['reusedRoutineHashes'], **parent['routineHashes']},
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Current field623 selects direct source SetDamageFlags with no GetHit/SoftHit/KnockBack bits. Full inherited mitigation/ExecuteEvents/ExecuteMissileDamage compose with actual native-health stat changes and monster mode requests, not a damage-commit recording stub.',
                        'DamageRate41 becomes packet dwPiercePct through STAT_DAMAGE_FRAMERATE; positive MDR is scaled by41/1024 in FP256 before resistance. Independent0/41/1024 rate, MDR0/1/10, tiny fractional fire and negative/zero/positive/immune resistance controls pass.',
                        'No ground mode change, get-hit/knockback flag or missile RNG advance is introduced. Declared blocked/avoidance masks suppress event health damage; these flags are supplied independently of the source physical-zero block-selection producer.'
                    ], services=[
                        'Inherited bounded row/stat/species/mode/owner, supplied block masks and empty item/event subscriber services remain. Payload raw1/40/56/256/1536 bypasses the separately proved damage creator/roll; this is delivery/mitigation evidence.',
                        'Full source field collision/filter/LastCollide/finder and modern geometry/timing/class/native item/health consumers still require separate acceptance. This grid does not claim ordinary physical shields can block a zero-physical fire packet.',
                        'Armageddon learned weather, native saved delayed impacts/fields/input/lifecycle/reloads and final effects/icons remain unimplemented. No handler or class certificate is enabled.'
                    ], next='Reuse current Boulder ground mitigation/health/defense helpers for field623 with do1 rather than do5; verify actual content endpoints/packets and field collision lifecycle before native Armageddon.')
    out = ROOT / 'tools/reference/armageddon-ground-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Armageddon:2160complete ground flag/rate/MDR/health/mode cases pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
