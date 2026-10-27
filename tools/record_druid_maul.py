#!/usr/bin/env python3
"""Bind learned Maul mechanics and actual input/reload evidence to its candidate."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/druid-maul'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    names = ['stats-inputs.json', 'stats-native-checks.log', 'maul-run.log', 'python-tests.log',
             'druid-maul-fixtures.json', 'druid-form-reaction-fixtures.json', 'druid-form-interruption-fixtures.json',
             'druid-form-casting-fixtures.json', 'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json',
             'attempt1-stats-native-checks.log', 'attempt1-stats-inputs.json', 'attempt1-maul-run.log',
             'attempt2-stats-native-checks.log', 'attempt2-stats-inputs.json', 'attempt2-maul-run.log',
             'attempt3-stats-native-checks.log', 'attempt3-stats-inputs.json', 'attempt3-maul-run.log', 'content-build-attempt1.log']
    artifacts = [STAGE / name for name in names]
    for kind in ['content', 'integration']:
        build = STAGE / (kind + '-build.log')
        text = build.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [build, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 119
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass3boots/2actual reloads' in (STAGE / 'maul-run.log').read_text()
    for name in ['druid-charge-model-check', 'druid-maul-check', 'druid-maul-live-result', 'druid-form-reaction-check',
                 'druid-form-interruption-check', 'druid-form-casting-check', 'shape-restriction-check', 'druid-form-attack-check']:
        assert log.count(name + ': passed') == 1, name
    for name in ['druid-maul-reload-check', 'druid-maul-expiry-stage-check', 'druid-form-reaction-reload-check',
                 'druid-form-interruption-reload-check', 'druid-form-casting-reload-check', 'druid-form-world-reload-check']:
        assert log.count(name + ': passed') == 2, name
    assert '1120native gain/cap/current-rank/expiry/channel comparisons' in log
    assert 'weapon-wear-before-gain' in log and 'history,respec,human/wolf/untrained/item-only exclusions pass' in log
    for kind in ['right', 'quick']:
        assert log.count('maul-' + kind + '-result: passed') == 3
        assert log.count('maul-' + kind + ': windup') == 3
    saved = json.loads((STAGE / 'druid-maul-fixtures.json').read_text())
    charge = saved['Charge']
    assert charge['Ability'] == 165 and charge['Count'] == 2 and charge['StoredRank'] == 1
    assert charge['DamagePercent'] == 60 and charge['StunFrames'] == 34 and charge['AttackRate'] == charge['OtherAnimationRate'] == 6
    assert charge['VelocityPercent'] == charge['LifeDrainMinimum'] == charge['LifeDrainMaximum'] == 0
    assert saved['NaturalDeadline'] < charge['UntilUtc'] and saved['PendingMana'] > 0
    assert saved['Form']['Form']['Ability'] == 164 and saved['Form']['Form']['LycanthropyRank'] == 20
    parent_raw, parent = frozen_receipt('fc00a5c', 'tools/reference/druid-form-reaction-native-evidence.json')
    assert parent['verified']['passMarkers'] == 112 and parent['verified']['fullClassMarkers'] == 1184
    source = ROOT / 'tools/reference/druid-charged-attacks-engine-evidence.json'
    assert json.loads(source.read_text())['contexts'] == 1120
    artifacts += [source]
    artifacts += [ROOT / '.local/class-skills' / ('druid-charged-attacks' + suffix)
                  for suffix in ['-source-probe.cpp', '-source-probe.json', '-compiler.log', '-sanitizer.log']]
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        audits.append(audit)
        artifacts.append(path)
    baseline = json.loads(subprocess.check_output(['git', 'show', 'fc00a5c:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    full_markers = full_saves = None
    if full_class:
        complete = inventory.validate_inventory(STAGE / 'class-inputs.json')
        assert complete['artifacts'] == inputs['artifacts'] and complete['copiedData'] == inputs['copiedData']
        text = (STAGE / 'class-native-checks.log').read_text()
        assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down') == 3
        assert 'including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        full_markers = text.count(': passed')
        full_saves = text.count('Writing world save snapshot done')
        assert full_markers == 1184 and full_saves == 35
        for name, expected in [('full-named-item-runtime-audit.json', audits[0]), ('full-first-loop-affix-audit.json', audits[1])]:
            path = STAGE / name
            assert json.loads(path.read_text()) == expected
            artifacts.append(path)
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(handlers) == 176 and sum(row['characterClass'] == 'Druid' for row in handlers) == 20 and status['completedClasses'] == []
    artifacts += [ROOT / name for name in ['tools/record_druid_maul.py', 'tests/check_druid_maul.py', 'tests/check_creeper_stats.py',
                                          'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    result = dict(referenceVersion='3.3.93847', installed=False, handlers=176, druidHandlers=20, completedClasses=[], goalComplete=False,
        currentExecutableParity=False,
        verified=dict(passMarkers=119, boots=3, actualReloads=2, chargeModelSourceComparisons=1120,
                      actualMaulInputs=6, originalFormAndInterruptionInputs=15, savedChargeCount=2,
                      originalDurationFrames=500, savedDeadlineHeldForInspection=True, naturalExpiryTimerMilliseconds=200,
                      preSaveOfflineExpiryMilliseconds=2000, betweenBootSeconds=5, pythonTests=70,
                      unchangedNamedDefinitions=573, inputFingerprints=[len(audit['InputHashes']) for audit in audits],
                      fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None,
                      fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenPriorAcceptance=dict(commit='fc00a5c', receipt='tools/reference/druid-form-reaction-native-evidence.json',
                                   sha256=hashlib.sha256(parent_raw).hexdigest(), passMarkers=112, fullClassMarkers=1184),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Learned bear-only Maul uses the shared form A1 clock/melee weapon scope, current3mana and source start56 hit/block/packet capture. Actual windup leaves health and charge unchanged; the action event commits the captured damage and shared weapon wear before do120gain. A deliberately changed release hit-roll cannot reroll the captured hit. Existing charge damage/stun/IAS apply once; the first fresh hit cannot borrow its future charge bonus.',
            'The shared source model compares1120contexts inside the native server using actual SkillValues. Current effective rank caps count and overwrites stored rank at gain; charge count evaluates the aura formulas.500frame duration refreshes on hit including physical immunity; misses and shield blocks do not gain/refresh. Allocation failure retains paid/committed combat. Low-mana Maul becomes ordinary Attack and grants no charge. Current row AttackNoMana1and the pinned older player dispatcher support that route; exact current-executable fallback remains unverified. The separately requested free-empty-swing/acquisition pass is still deferred.',
            'Saved owner/ability state uses immutable validation/atomic replacement and GenericPersistence177. Source group3 form removal clears charges and pending Maul; charge expiry retains the form. Natural200ms expiry, real gear rank2cap4to rank1cap3, Stop/out-and-back/weapon/range cancellation, death, class-history and respec cleanup pass. The history control uses an unequipped fresh actor without recent combat, respecting the existing class-switch rules.',
            'Six actual right/quick inputs across three boots/two reloads pay once and deliver one damaging charge. Original rank1bear/Lycan20/weapon and captured count2payload survive both reloads. The normal20second deadline is checked before the charge is held for restart inspection. Separate2second charge fixtures are verified still active immediately before save, then expire during the5second offline interval. Already-paid interrupted actions are not serialized/replayed/refunded.',
            'Initial compile lacked the corpse-hook namespace and was repaired. First block fixture selected a nonshield component; the corrected positive shield supplier passes. A later history fixture incorrectly attempted a restricted switch while equipped/recently in combat; it now uses a valid fresh unequipped control. Failed logs are retained; these fixture corrections did not relax production eligibility. Main build/configuration/client/saves remain preserved.',
        ], remaining=[
            'Legacy gain after losing the target/contact remains a current-D2Runcertainty. This route cancels lost contact without gaining; do not present that adapter as verified current behavior. Reentrant reflect/death/form changes, exact current low-mana/allocation/event/geometry/seed/cadence and player-form extraAR/reentry remain unverified. Unmapped native stun recipients, PvP and item-only/foreign-class grants are unsupported.',
            'Feral Rage still has no enabled handler. Its charge math is prepared, but live velocity/leech and learned action acceptance remain next. Other form gaps, incomplete classes and Warlock mechanics remain on the roadmap. Maul/form human Wrestle/contact sound/charge flash are identified placeholders; final orb/body/contact/audio and talent/left/right/quick icons remain separate asset work.',
        ])
    path = ROOT / 'tools/reference/druid-maul-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Maul:119focused/3boots/2reloads/6real inputs,1120charge comparisons,70Python;fullClass={full_markers};176handlers/Druid20,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
