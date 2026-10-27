#!/usr/bin/env python3
"""Pin form resource channels and retain Sanctuary's approved whole maxima."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def review(check=False):
    path = ROOT / '.local/research/d2r-3.3.93847/itemstatcost-barbs-verified.txt'
    local = list(csv.DictReader(path.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    public = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((public / 'sources.json').read_text())
    cached_path = public / 'json/itemstatcost.json'
    assert sha(cached_path) == manifest['files']['json/itemstatcost.json']['sha256']
    cached = json.loads(cached_path.read_text())
    names = ['hitpoints', 'maxhp', 'stamina', 'maxstamina', 'item_maxhp_percent', 'skill_staminapercent']
    rows, compared = {}, 0
    for name in names:
        actual = next(row for row in local if row['Stat'] == name)
        prior = next(row for row in cached.values() if row['Stat'] == name)
        for field, value in prior.items():
            if field in actual and field != 'lineNumber':
                assert str(value) == actual[field], (name, field)
                compared += 1
        rows[name] = {key:value for key,value in actual.items() if key and value}
    assert all(rows[name]['ValShift'] == '8' for name in names[:4])
    assert rows['item_maxhp_percent']['op'] == '11' and rows['item_maxhp_percent']['op stat1'] == 'maxhp'
    assert rows['skill_staminapercent']['op'] == '1' and rows['skill_staminapercent']['op stat1'] == 'maxstamina'
    source_path = 'source/D2Common/src/D2StatList.cpp'
    raw = (BASE / 'd2moo-pinned-source' / source_path).read_bytes()
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == source_path)
    routines = {name:function(raw.decode(), name) for name in ['D2Common_11248', 'STATLIST_GetMaxLifeFromUnit', 'STATLIST_GetMaxStaminaFromUnit']}
    assert 'STATLIST_GetTotalStatValue_Layer0' in routines['D2Common_11248']
    resources = ROOT / 'src/Sanctuary.Content/ClassResources.cs'
    health = ROOT / 'src/Sanctuary.Content/SkillHealth.cs'
    stats = ROOT / 'src/Sanctuary.Content/ClassStats.cs'
    assert 'NativeMaximum=>(int)(Maximum256/256)' in resources.read_text()
    assert 'target.HitsMax*256' in health.read_text()
    assert 'return ClassResources.Parts(player,resource).NativeMaximum;' in stats.read_text()
    observations = ROOT / 'tools/reference/druid-form-removal-user-observations.json'
    assert json.loads(observations.read_text())['observed']['relativeLifeOnRemoval']
    foundation = ROOT / 'docs/balance-foundations.md'
    assert 'Approved native whole maxima remain a documented projection.' in foundation.read_text()
    result = dict(referenceVersion='3.3.93847', reviewOnly=True, exactD2RMaximaImplemented=False,
        wholeNativeMaximumProjectionApproved=True, requiresPlayerPrecisionRewrite=False,
        precedence=dict(path='docs/balance-foundations.md', section='Resource percentage operation correction', sha256=sha(foundation)),
        table=dict(path=str(path.relative_to(ROOT)), sha256=sha(path), publicSha256=sha(cached_path), publicCommit=manifest['commit'], corroboratedFields=compared, rows=rows),
        legacySource=dict(commit=tree['sha'], path=source_path, sha256=hashlib.sha256(raw).hexdigest(), routineHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in routines.items()}),
        nativeInputs={str(path.relative_to(ROOT)):sha(path) for path in [resources, health, stats]},
        removalObservationSha256=sha(observations),
        established=[
            'Current hitpoints/maxhp and stamina/maxstamina use ValShift8. Form life belongs to item_maxhp_percent op11/maxhp; wolf stamina belongs to skill_staminapercent op1/maxstamina. These are existing resource-percentage channels,not independent multiplicative maxima.',
            'The complete legacy max-life/stamina getters return the total raw stat through D2Common_11248without an integer-life shift. The current table precision agrees with retaining FP256 maxima.',
            'Current ClassResourceParts retains FP256 while calculating maximums,but NativeMaximum truncates before the native player getter. SkillHealth.Maximum256then reconstructs a managed player maximum from native integer HitsMax. This differs from exact D2Rmaxima,but the authoritative foundation explicitly approves whole native maxima and the1..65000bound.',
            'User observations corroborate proportional life entering and voluntarily leaving forms. Prepared saved form tests can establish correct percentage pools,proportional projected transitions and unchanged saved fractional injuries independently of this still-open maximum precision gap.',
        ], next=[
            'Retain the approved whole maximum projection and its existing storage/display contract. Source FP256calculation and saved fractional current pools still require verification,while exact D2Rmaxima are a documented divergence rather than a mandatory global rewrite.',
            'The initial a0837efreview wrongly classified this approved projection as an unapproved blocking precision gap. This correction follows the authoritative foundation and user precedence instruction; no runtime behavior or accepted assembly changes.',
            'Compose transformed basic-attack additional AR/SrcDam and modern animation/attack rates separately from the generic stat-pool hooks. Final body/effects/audio/icons remain independent.',
        ])
    target = ROOT / 'tools/reference/druid-form-resource-review.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print(f'Druid resource review:{compared}current statcost fields,FP256 parts/op11/op1;approved whole native maxima retained,no global precision rewrite required.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    review(parser.parse_args().check)
