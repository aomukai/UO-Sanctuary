#!/usr/bin/env python3
"""Bind saved Grizzly body acceptance; optional full regression is a separate scope."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/grizzly-body'
FROZEN = '67ad211'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name):
    return subprocess.check_output(['git', 'show', FROZEN + ':' + name], cwd=ROOT)


def validate_inventory(path):
    inputs = json.loads(path.read_text())
    expected = {str(p.relative_to(ROOT)) for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / directory).glob('*.cs')}
    assert {p for p in inputs['sourceInputs'] if p.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
    return inputs


def record(check=False, full_class=False):
    inputs = validate_inventory(STAGE / 'stats-inputs.json')
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'body-run.log', 'python-tests.log', 'grizzly-body-fixtures.json']]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        build = path.read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 235
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('grizzly-body-check: passed') == 1 and log.count('grizzly-body-reload-check: passed') == 2
    assert log.count('grizzly-ai-check: passed') == 1 and log.count('grizzly-stats-check: passed') == 1
    assert '2016native ordered-service/owner-memory comparisons' in log
    assert 'Prepared Grizzly saved body:937.5Normal/812.5Nightmare-Hell life,234/203FP birth recovery' in log
    for name in ['dire-wolf-body', 'dire-wolf-world', 'dire-wolf-casting', 'spirit-wolf-body', 'spirit-wolf-world', 'spirit-wolf-casting', 'barbs-casting', 'wolverine-casting', 'oak-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for name in ['dire-wolf', 'spirit-wolf', 'barbs', 'oak', 'wolverine']:
        for kind in ['right', 'quick']:
            assert log.count(name + '-' + kind + '-result: passed') == 3, name
    saved = json.loads((STAGE / 'grizzly-body-fixtures.json').read_text())
    birth = saved['State']['Snapshot']['Birth']
    assert saved['State']['Owner'] == saved['Owner'] and saved['Grizzly'] > 0
    assert birth['Life256'] == 240000 and birth['Regeneration256'] == 234 and birth['ArmorPercent'] == 10 and birth['PhysicalSunder'] == 0
    assert saved['Maximum256'] == 324000 and saved['Regeneration256'] == 395 and saved['Life256'] == 27009 + 2 * 395
    assert saved['State']['Profile'] == 'prepared-life-and-ownership-v1' and 'Controller' not in saved['State']
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    frozen_path = 'tools/reference/grizzly-ai-native-evidence.json'
    frozen = json.loads(historical(frozen_path))
    for name, digest in frozen['artifacts'].items():
        raw = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest() == digest, name
    assert frozen['verified']['nativeAiComparisons'] == 2016 and frozen['verified']['inputFingerprints'] == [647, 21]
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest, source
        audits.append(audit)
        artifacts += [path, ROOT / 'tools/reference' / name]
    baseline = json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    full_markers, full_saves = None, None
    if full_class:
        complete = validate_inventory(STAGE / 'class-inputs.json')
        assert complete['artifacts'] == inputs['artifacts'] and complete['copiedData'] == inputs['copiedData']
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log and full_log.count('Shutting down') == 3
        assert 'including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        assert full_log.count('named-gear-check: passed') == 1 and full_log.count('named-gear-reload-check: passed') == 2
        assert full_log.count('first-loop-catalogue-check: passed') == 1
        full_markers, full_saves = full_log.count(': passed'), full_log.count('Writing world save snapshot done')
        assert full_markers == 1184 and full_saves >= 3
        for name, expected in [('full-named-item-runtime-audit.json', audits[0]), ('full-first-loop-affix-audit.json', audits[1])]:
            path = STAGE / name
            assert json.loads(path.read_text()) == expected, name
            artifacts.append(path)
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 171 and sum(x['characterClass'] == 'Druid' for x in handlers) == 15 and status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summongrizzly')['implementation'] != 'handler-present'
    effects = json.loads((ROOT / 'data/Sanctuary/Balance/skill-effects.json').read_text())
    assert 'Grizzly [placeholder]' in next(x for x in effects if x['talentId'] == 'druid.summongrizzly')['placeholder']
    review_path = ROOT / 'tools/reference/grizzly-bearsmite-flow-review.json'
    review = json.loads(review_path.read_text())
    assert review['sourceMonsterModes']['MONMODE_SEQUENCE'] == 14 and review['sourceMonsterModes']['MONMODE_BLOCK'] == 6
    newer = ROOT / review['supplementaryStart']['path']
    assert sha(newer) == review['supplementaryStart']['sha256']
    for name, digest in review['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / name) == digest, name
    observation=ROOT/review['userObservation']['path']
    assert sha(observation)==review['userObservation']['sha256']
    artifacts += [review_path, newer, observation]
    artifacts += [ROOT / name for name in ['tools/record_grizzly_body.py', 'tests/check_grizzly_body.py', 'tests/check_creeper_stats.py', 'tests/check_class_skills.py', 'tools/build_class_skills.py', 'tools/review_grizzly_bearsmite.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, handlerEnabled=False, handlers=171, druidHandlers=15, completedClasses=[], goalComplete=False,
        verified=dict(passMarkers=235, boots=3, actualReloads=2, nativeAiComparisons=2016, nativeBirthComparisons=55152, nativeOrdinaryModeComparisons=6144,
            fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves,
            pythonTests=70, namedDefinitions=573, inputFingerprints=[len(x['InputHashes']) for x in audits]),
        frozenAiAcceptance=dict(commit=FROZEN, receipt=frozen_path, sha256=hashlib.sha256(historical(frozen_path)).hexdigest()),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Saved vulnerable Grizzly backend reuses SkeletonGuardian serialized chain,exact life/health,owner history,wardrobe,park/resume,difficulty and source regeneration/defense consumers. It has no native melee or automatic AI timer. Default UO0xD4/silent presentation is explicitly Grizzly [placeholder].',
            'Current model/animation data are imported separately:footprint2,MeleeRng2,source GH5frames/speed256. Shared GH duration formula is retained;five is the animation metadata,not a claim of five server-frame recovery. Source identity removes inherited skeleton undead/poison/bleed rules and keeps normal Grizzly chill/freeze eligibility.',
            'Native fractional life/recovery,armor+Shout+InnerSight ordering,BOlife/regen callbacks,real gear immutability,physical/fire/poison/chill/freeze/GH,atomic malformed restore,offline/history/orphan,wolf/bear coexistence and owner-facet Normal/Nightmare/Hell injury ratios pass. Legal elevated destination-facet test space is supplied;no illegal-placement warp is invented.',
            'Unbuffed exact-life summons now opt into the existing SkillCombat fractional incoming receiver. New native test verifies10fire damage at5resistance loses2432FP,not nine whole life. Other mitigation logic is reused. Full-class verification scope is separately recorded only after it completes.',
            'Both actual body reloads retain source owner/birth,parked state,BOmaximum324000FP and395FP regeneration;life27009advances by one explicit395FP active frame per reload to27799. Offline frames do not replay.648/21fresh input fingerprints preserve573named decisions;all70Python tests pass.',
            'Complete-body BearSmite review pins real sequence14and block6,records newer start32unchanged,and explicitly retains current Stun Length label versus reconstructed post-allocation physical-field ambiguity. No new BearSmite implementation is inferred. Earlier AI prose648fingerprints was a typo:its receipt verified647/21;this package actually has648/21.',
        ], remaining=[
            'Before full mode-stream/world integration,correct the previously unused beast-helper BLOCK3mapping to actual6and add header-backed GH/block/sequence/dead controls. Prior ordinary4/5/7/8compared cases are unaffected. Sequence12wording in earlier notes was wrong:12is dead,14is sequence.',
            'No automatic Grizzly AI112world/movement,ordinary hit,BearSmite326sequence/combat,learned40mana/cap1/coexistence/input or rollout is claimed. User confirms knockback without observed stuns;do not add a stun. Keep the hidden post-allocation flat-field/health semantics explicit instead of inventing250extra damage.',
            'Explicit owner-death and lethal Grizzly death/return chronology,full modern mode/first-think/busy/event/seed/geometry/path,equipment/nonzero-sunder/item-only and graphical playtesting remain acceptance work. Body-only preparation uses explicit recovery frames and shared legal resume placement,not automatic cross-facet movement.',
            'Final B7effects/audio and independent talent/right/quick icons remain separately pending. Installed main remains101013bwith original build/saves/config/client preserved. New source/full candidate evidence does not install the package or complete a class.',
        ])
    target = ROOT / 'tools/reference/grizzly-body-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print(f'Prepared Grizzly body:235markers/3boots/2reloads,70Python tests,fullClass={full_markers};171handlers/Druid15,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
