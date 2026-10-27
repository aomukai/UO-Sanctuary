#!/usr/bin/env python3
"""Separate current form evidence from the accepted legacy lifecycle projection."""
import argparse
import hashlib
import json
from pathlib import Path

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def review(check=False):
    preflight_path = ROOT / 'tools/reference/druid-forms-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    forms = preflight['currentRows']['skills']
    for name in ['Wearwolf', 'Wearbear']:
        assert forms[name]['Param12'] == '1'
        assert forms[name]['*Param12 Description'] == 'Allow Instant Direct Transformation'
    relative = 'source/D2Common/src/Units/Units.cpp'
    raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == preflight['legacyCommit']
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
    names = ['UNITS_UpdateAnimRateAndVelocity', 'UNITS_CanAnimModeUseAttackRate', 'UNITS_UpdateAttackAnimRateAndVelocity', 'UNITS_IsSeqAnimSpeedModulatedByFCR', 'UNITS_UpdateCastAnimRateAndVelocity', 'UNITS_UpdateBlockAnimRateAndVelocity', 'UNITS_UpdateGetHitAnimRateAndVelocity', 'UNITS_UpdateOtherAnimRateAndVelocity']
    bodies = {name:function(raw.decode(), name) for name in names}
    attack = bodies['UNITS_UpdateAttackAnimRateAndVelocity']
    assert 'nRate > 175' in attack and 'nRate = 175;' in attack
    assert 'STATEMASK_MELEEONLY' in attack and 'D2Common_11043(pUnit)' in attack
    dispatch = bodies['UNITS_UpdateAnimRateAndVelocity']
    assert dispatch.index('D2COMMON_11013_ConvertMode') < dispatch.index('UNITS_IsSeqAnimSpeedModulatedByFCR') < dispatch.index('UNITS_CanAnimModeUseAttackRate')
    druid = (BASE / 'd2moo-pinned-source/source/D2Game/src/SKILLS/SkillDruid.cpp').read_bytes()
    assert hashlib.sha256(druid).hexdigest() == preflight['sourceHashes']['source/D2Game/src/SKILLS/SkillDruid.cpp']
    transform = function(druid.decode(), 'SKILLS_SrvDo116_Wearwolf_Wearbear')
    assert 'sub_6FD11C90' in transform and 'Param12' not in transform
    data = dict(referenceVersion='3.3.93847', reviewOnly=True, currentExecutableParity=False,
        currentPreflightSha256=hashlib.sha256(preflight_path.read_bytes()).hexdigest(),
        currentFields={name:{key:forms[name][key] for key in ['Param12', '*Param12 Description']} for name in ['Wearwolf', 'Wearbear']},
        userCrossFormObservation=dict(source='Direct user reply,2026-10-28', observed=False,
            verbatim="i have no idea, i never learned both at the same time", conclusion='No cross-form observation available; no respec requested and no global block.'),
        legacyCommit=tree['sha'], sourceFile=dict(path=relative, sha256=hashlib.sha256(raw).hexdigest()),
        routineHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in bodies.items()},
        officialHistoricalReference=dict(title='Diablo II: Resurrected Patch 2.4 | Ladder Now Live',
            url='https://news.blizzard.com/en-gb/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',
            patchLive='2022-04-14', retrieved='2026-10-28', releaseNotesNotPtr=True,
            paraphrasedFacts=[
                'Released2.4uses only the new attack-speed calculation for both forms; earlier PTR faster-of-two behavior is not the final release rule.',
                'Both form attack-speed caps increased from75to150percent. Werebear attacks and skills became uninterruptible.',
                'Cyclone Armor and Hurricane became castable in both forms; Maul added3percent attack speed per charge.',
            ]),
        conclusions=[
            'Current Param12value1and its direct-transformation label are primary table evidence,not an observed cross-form transition. The complete legacy do116has no corresponding option and clears either active group3form before returning0. Do not silently treat the accepted legacy cross-form comparison as confirmed3.3.93847behavior.',
            'The legacy attack writer clamps combined rate at175and uses a melee-only alternate base-speed service. Released2.4changes make unchanged reuse of that writer for live player forms unsuitable as a current mechanical reference. Its full routine hashes are retained for comparison,not acceptance.',
            'The350native model cases remain bounded legacy player-state/stat projections. Their150unshifts include50cross-form cases; only same-form free unshift is user-confirmed. No learned route or live stat/rate consumer was enabled. Keep the immutable accepted package unchanged and apply modern corrections in the next candidate.',
            'Existing ClassResources percentage buckets,BattleOrders proportional-resource changes,ClassStats AR/defense and saved buff profiles remain reuse candidates. Confirmed proportional form-entry life does not independently establish unshift/expiry/forced-removal rounding.',
        ], next=[
            'Implement saved form/stat/resource projection and verify unaffected entry/same-form unshift/expiry paths while retaining explicit unresolved modern behavior. Do not stall the whole Druid roadmap.',
            'Establish direct cross-form eligibility/payment/timing using current executable evidence or an actual pinned-build observation; the Param12description alone is insufficient.',
            'Establish modern attack-rate composition and form-specific animation/restriction consumers. Historical patch notes identify known changes but do not certify every3.3.93847formula or later revision.',
        ])
    path = ROOT / 'tools/reference/druid-forms-compatibility-review.json'
    rendered = json.dumps(data, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid form compatibility:current Param12gap,8complete legacy rate/dispatch hashes,official released2.4changes; no modern cross-form/rate acceptance claimed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    review(parser.parse_args().check)
