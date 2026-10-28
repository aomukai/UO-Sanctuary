#!/usr/bin/env python3
"""Bind an isolated Hunger draft without enabling it in the canonical build."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/druid-hunger-next'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs_path = STAGE / 'prepared-inputs.json'
    inputs = json.loads(inputs_path.read_text())
    for relative, digest in inputs['preparedSources'].items():
        assert sha(STAGE / relative) == digest, relative
    for relative, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data' / relative.removeprefix('Data/')) == digest, relative
    artifacts = [inputs_path]
    for kind in ['content', 'integration']:
        assembly = STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')
        assert sha(assembly) == inputs['artifacts']['Assemblies/' + assembly.name]
        build = STAGE / (kind + '-build.log')
        text = build.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [assembly, build]
    logpath = STAGE / 'prepared-native-checks.log'
    log = logpath.read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 19
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('druid-hunger-model-check: passed') == log.count('druid-hunger-check: passed') == 1
    assert log.count('druid-hunger-reload-check: passed') == 2 and log.count('druid-hunger-snapshot-check: passed') == 3
    assert '34560source cases and28800S3author/source/native schedules pass' in log
    for family in ['hunger-right', 'hunger-quick']:
        assert log.count(family + '-result: passed') == log.count(family + ': windup') == 3
    runner = STAGE / 'prepared-runner.py'
    run_log = STAGE / 'prepared-run.log'
    assert 'Isolated Hunger draft:19markers/3boots/2reloads' in run_log.read_text()
    artifacts += [logpath, runner, run_log, STAGE / 'prepared-hunger-fixtures.json']
    for prefix in ['druid-hunger', 'druid-s3-clock']:
        evidence = ROOT / 'tools/reference' / (prefix + '-engine-evidence.json')
        source = json.loads(evidence.read_text())
        assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
        artifacts += [evidence, ROOT / source['fixture']['path']]
    for attempt in range(1, 5):
        for name in ['prepared-native-checks.log', 'prepared-run.log', 'prepared-inputs.json']:
            path = STAGE / ('attempt' + str(attempt) + '-' + name)
            if path.exists():
                artifacts.append(path)
    # Capture only differences from the frozen canonical predecessor. The
    # resulting ordinary patch can be reviewed/applied after its regression.
    patch_parts, differences = [], {}
    for name in sorted(inputs['preparedSources']):
        relative = name.removeprefix('prepared-source/')
        draft = STAGE / name
        canonical = ROOT / relative
        old = canonical.read_text() if canonical.exists() else ''
        new = draft.read_text()
        if old == new:
            continue
        if canonical.exists():
            committed = subprocess.check_output(['git', 'show', '0d32e8e:' + relative], cwd=ROOT).decode()
            assert committed == old, 'Canonical predecessor changed: ' + relative
        differences[relative] = sha(draft)
        patch_parts += difflib.unified_diff(old.splitlines(keepends=True), new.splitlines(keepends=True),
            fromfile='a/' + relative if canonical.exists() else '/dev/null', tofile='b/' + relative)
    assert 'src/Sanctuary.Content/DruidHunger.cs' in differences and 'tests/Sanctuary.Integration/DruidHungerChecks.cs' in differences
    patch = ROOT / 'tools/reference/druid-hunger-prepared.patch'
    text = ''.join(patch_parts)
    if check:
        assert patch.read_text() == text
    else:
        patch.write_text(text)
    subprocess.run(['git', 'apply', '--check', str(patch)], cwd=ROOT, check=True)
    result = dict(referenceVersion='3.3.93847', canonicalParent='0d32e8e', installed=False, canonicalHandlerEnabled=False,
        isolatedDraftHandlerEnabled=True, canonicalHandlers=178, canonicalDruidHandlers=22, completedClasses=[], currentExecutableParity=False,
        verified=dict(markers=19, boots=3, actualReloads=2, actualHungerInputs=6, sourceContexts=34560, sourceRoutines=8, nativeS3Contexts=28800,
            zeroWarningBuilds=2, fullRegression=False, canonicalPythonAcceptance=False),
        preparedInputsSha256=sha(inputs_path), preparedSourceChanges=differences,
        patch=dict(path=str(patch.relative_to(ROOT)), sha256=sha(patch)),
        artifacts={str(path.relative_to(ROOT)):sha(path) for path in sorted(artifacts)}, runnerSource=runner.read_text(),
        established=[
            'The isolated learned Hunger draft passes both forms with release-time contact/results, S3timing, post-critical signed physical reduction, retained nonphysical components, real Highlord critical/soft rank, additive physical-only life/mana drain, full-mana post-leech payment, old Feral/Maul charge preservation, physical immunity, shield block and cancellation controls.',
            'Retained failed attempts distinguish fixture/integration mistakes from accepted behavior: the first clock fixture supplied already-slowed SIAS instead of the captured unslowed Werewolf rate; the route guard initially used an empty string where absent catalogue fields return0; reload then exposed a one-point mana tick because the fixture regeneration hold ran before default initialization. The accepted fixture reapplies its hold at late Initialize,matching the retained form fixtures. Exact resource assertions remain unchanged. A high-rank declared diminishing-formula service was also corrected to retain its100percent cap before native comparison.',
            'Six real wolf-right/bear-quick casts pay three mana at action, not at start. Fractional owner/target life,mana,form,original charge and bindings survive two actual reloads without a pending packet replay. Source/native model compares34560bounded Hunger cases and28800S3schedules. This isolated scope does not establish canonical focused/full acceptance or installation.',
        ], remaining=[
            'Keep canonical Fire Claws source/data/DLLs frozen until its running full regression is terminal and accepted. Apply this checked patch as a new candidate only afterward, update generated status/effect/icon inventories, build both assemblies and run retained focused/Python/full suites against that candidate.',
            'Hunger no-start/do122 differs from Fire Claws start58: contact and weapon packet capture happen at release. Old outer handler consumes after do; exact modern payment/minimum-mana quirks,lost-target/lost-mana,reentrant events,seed/geometry/reentry and client remain uncertain. The draft refuses lost-mana release and cancels lost contact explicitly. Ordinary low-mana Attack follows the existing table/older-dispatcher adapter, not a modern executable certificate.',
            'Item-only/foreign classes/PvP/unmapped recipients remain unsupported. Final S3bite/body/contact/leech/audio and talent/left/right/quick icons remain separate placeholders. Main installed build/client/configuration/saves remain preserved.',
        ])
    path = ROOT / 'tools/reference/druid-hunger-preparation.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Isolated Hunger accepted:19markers/3boots/2reloads/6real inputs; checked unapplied patch, canonical178/Druid22 unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
