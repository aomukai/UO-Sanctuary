#!/usr/bin/env python3
"""Reuse complete sentry damage routines with pinned Wake maker/wave records."""
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
    # Reuse the already source-verified ABI and explicit services. Verify the
    # parent first, then ensure every copied production body is unchanged.
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_sentry_damage_reference.py'), '--check'],
                   check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/sentry-damage-source-evidence.json').read_text())
    creator = json.loads((ROOT / 'tools/reference/bow-launch-engine-evidence.json').read_text())
    base = (STAGE / 'sentry-damage-source-probe.cpp').read_text().split('int main()', 1)[0]
    base = re.sub(r'\b261\b', '262', base)
    base = re.sub(r'\b306\b', '281', base)
    base = base.replace('lightHard', 'infernoHard')
    old = 'return id==262?(fireHard+infernoHard)*9:0;'
    assert old in base
    base = base.replace(old, 'return id==262?(fireHard+infernoHard)*10:0;')
    # Defaults are supplied from current data in the harness for each call.
    names = parent['blockHashes'] | creator['blockHashes']
    for name, digest in names.items():
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', base, re.M)
        assert match, name
        end, depth = base.index('{', match.start()) + 1, 1
        while depth:
            depth += (base[end] == '{') - (base[end] == '}')
            end += 1
        assert hashlib.sha256(base[match.start():end].encode()).hexdigest() == digest, name

    tables, pins = {}, {}
    for name in ['skills', 'missiles']:
        path = ROOT / f'tools/reference/d2r-named-equipment/json/{name}.json'
        manifest = json.loads((path.parent.parent / 'sources.json').read_text())
        pins[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        tables[name] = json.loads(path.read_bytes())
    skill = tables['skills']['262']
    assert not skill.get('SrcDam') and skill['HitShift'] == 8 and skill['EType'] == 'fire'
    assert skill['Param8'] == 10 and skill['EDmgSymPerCalc'] == "(skill('Fire Trauma'.blvl) + skill('Inferno Sentry'.blvl)) * par8"
    for name in ['wake of destruction maker', 'wake of destruction']:
        row = tables['missiles'][name]
        assert row['Skill'] == skill['skill'] and not row.get('MissileSkill') and not row.get('SrcDamage') and not row.get('SrcMissDmg')
    fields = json.loads((STAGE / 'bow-launch-source-probe.json').read_text())['fields']
    cpp = base + r'''
D2UnitStrc origin;
bool first=true;
void emit(int rank,int fire,int inferno,int mastery,int noise,int overrideSkill,int mid,int hasOrigin){
 owner={};weapon={};missile={};origin={};skill={};actorSkill={};row={};missileData={};
 owner.dwUnitType=UNIT_MONSTER;owner.pInventory=nullptr;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=mid;missileData.nSkill=281;
 row.nSkill=262;row.dwMissileFlags=overrideSkill?gdwBitMasks[MISSILESFLAGINDEX_MISSILESKILL]:0;
 skill.nEType=ELEMTYPE_FIRE;actorSkill.nEType=ELEMTYPE_NONE;actorSkill.dwEMin=actorSkill.dwEMax=0;
'''
    for field, value in [('dwEMin', skill['EMin']), ('dwEMax', skill['EMax']), ('nToHitShift', skill['HitShift'])]:
        cpp += f'skill.{field}={value};\n'
    for i in range(5):
        cpp += f"skill.dwEMinLev[{i}]={skill['EMinLev'+str(i+1)]};skill.dwEMaxLev[{i}]={skill['EMaxLev'+str(i+1)]};\n"
    cpp += r'''
 fireHard=fire;infernoHard=inferno;critCalls=0;critical=true;
 for(int& stat:owner.stats)stat=noise?37:0;
 for(int& stat:origin.stats)stat=777;
 for(int& stat:weapon.stats)stat=999;
 owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,hasOrigin?&origin:nullptr,&missile,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",rank,fire,inferno,mastery,noise,overrideSkill,mid,hasOrigin);first=false;
'''
    for i, field in enumerate(fields):
        cpp += f'printf("{"," if i else ""}%d",d.{field});\n'
    cpp += r'''
 printf("],%d]",critCalls);
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
 for(int rank:{1,3,8,9,16,17,20,22,23,28,40,255})for(int fire:{0,1,20})for(int inferno:{0,4,20})for(int mastery:{-50,0,25,100})for(int noise:{0,1})for(int overrideSkill:{0,1})for(int mid:{517,518})for(int origin:{0,1})emit(rank,fire,inferno,mastery,noise,overrideSkill,mid,origin);
 for(int rank=1;rank<=255;rank++)emit(rank,20,20,25,0,0,518,1);
 printf("]\n");}
'''
    source = STAGE / 'wake-of-fire-damage-source-probe.cpp'
    source.write_text(cpp)
    binary = source.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(source), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'wake-of-fire-damage-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    data = json.loads(result.stdout)
    assert len(data) == 7167

    def trunc(value, divisor=100):
        return abs(value) // divisor * (1 if value >= 0 else -1)

    for rank, fire, inferno, mastery, noise, override, mid, has_origin, values, critical_calls in data:
        expected = []
        for lane in ['EMin', 'EMax']:
            amount, left = skill[lane], rank - 1
            for i, size in enumerate([7, 8, 6, 6, 2**30], 1):
                count = min(left, size)
                amount += count * skill[f'{lane}Lev{i}']
                left -= count
            amount <<= skill['HitShift']
            amount += trunc(amount * (fire + inferno) * 10)
            amount += trunc(amount * mastery)
            expected.append(0 if override else amount)
        actual = dict(zip(fields, values))
        assert [actual['nFireMinDamage'], actual['nFireMaxDamage']] == expected
        assert critical_calls == 0
        assert all(value == 0 for field, value in actual.items() if field not in ['nFireMinDamage', 'nFireMaxDamage'])
    fixture = STAGE / 'wake-of-fire-damage-source-probe.json'
    fixture.write_text(json.dumps(dict(fields=fields, cases=data), separators=(',', ':')) + '\n')
    evidence = dict(
        referenceVersion='3.3.93847', talentId='assassin.wakeoffire', handlerEnabled=False,
        legacyCommit=parent['legacyCommit'], sourceHashes=parent['sourceHashes'],
        routineHashes=names, pinnedInputHashes=pins,
        parentEvidence='tools/reference/sentry-damage-source-evidence.json',
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),
                     completeDamageCases=len(data), allRankGoldens=255),
        established=[
            'Complete shared creator and elemental getters select player skill262 for maker517 and wave518, despite stored actor skill281. Explicit MissileSkill override redirects to actor281 and produces no typed fire.',
            'Both endpoints use current five-tier damage, fixed-point shift8 and 10 percent per hard Fire Blast/Inferno Sentry point. Inherited fire mastery is applied once after synergy, separately to each endpoint with source truncation.',
            'Source damage0 excludes weapon physical, item typed/poison/drain and critical rolls. A supplied origin with noisy stats does not inject parent missile damage; nested wave creation recalculates from the actor owner rather than double applying the maker packet.',
            'All 255 effective ranks have endpoint goldens. Representative tier boundaries, positive/negative mastery, hard synergies, owner damage noise, maker/wave selection, origin presence and override controls execute the unchanged complete routines.'
        ], services=[
            'Owner stat reads, hard-skill/formula lookup, ratio helper, physical-zero getters and cached row access retain explicit services from the sentry probe. Source bodies are hashed unchanged against pinned parent evidence.',
            'This does not execute the full missile factory, inherited stat-list capture, stat cleanup, target defenses, DamageRate205/NextHit/LastCollide, collision geometry, actor firing/retirement clocks or current executable behavior.'
        ], sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    path = ROOT / 'tools/reference/wake-of-fire-damage-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered, 'Wake damage evidence stale'
    else:
        path.write_text(rendered)
    print('Wake maker/wave damage: 7,167 complete creator/getter cases including 255 rank goldens; sanitizers clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
