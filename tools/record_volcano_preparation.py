#!/usr/bin/env python3
"""Record the bounded, uninstalled Volcano supplier/request-plan checkpoint."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/volcano'
BASELINE = 'cfbce5b'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    content = STAGE / 'content/Sanctuary.Content.dll'
    integration = STAGE / 'integration/Sanctuary.Integration.dll'
    proofs = {}
    for suffix in ['engine-evidence', 'factory-source-evidence', 'landing-source-evidence', 'damage-source-evidence', 'damage-evidence', 'pattern-evidence']:
        path = ROOT / f'tools/reference/volcano-{suffix}.json'
        proof = json.loads(path.read_text())
        assert proof['handlerEnabled'] is False and proof['installed'] is False
        if 'fixture' in proof:
            assert sha(ROOT / proof['fixture']['path']) == proof['fixture']['sha256']
        if 'contentAssemblySha256' in proof:
            assert sha(content) == proof['contentAssemblySha256']
        proofs[str(path.relative_to(ROOT))] = sha(path)
    audit_path = ROOT / 'tools/reference/named-item-runtime-audit.json'
    audit = json.loads(audit_path.read_text())
    before = json.loads(subprocess.check_output(['git', 'show', BASELINE + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audit['Definitions']) == 573 and audit['Definitions'] == before['Definitions']
    first = json.loads((ROOT / 'tools/reference/first-loop-affix-audit.json').read_text())
    assert sha(content) == audit['ContentAssemblySha256'] == first['ContentAssemblySha256']
    for table in [audit, first]:
        for path, digest in table['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
    assert len(audit['InputHashes']) == 591 and len(first['InputHashes']) == 21
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    volcano = next(row for row in status['skills'] if row['talentId'] == 'druid.volcano')
    assert volcano['implementation'] == 'pending' and volcano['complete'] is False
    assert sum(row['implementation'] == 'handler-present' for row in status['skills']) == 161
    python_log = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python_log and '\nOK\n' in python_log and '\nFAILED' not in python_log
    native_log = (STAGE / 'audit-refresh-native.log').read_text()
    for name in ['first-loop-catalogue-check', 'named-gear-check']:
        assert name + ': passed' in native_log and name + ': failed' not in native_log
    assert ' ERR]' not in native_log and ': failed' not in native_log
    for name in ['content', 'integration']:
        log = (ROOT / f'.local/class-skills/volcano-{name}-build.log').read_text()
        assert 'Build succeeded.' in log and '0 Warning(s)' in log and '0 Error(s)' in log
    inputs = [ROOT / f'tools/{name}.py' for name in ['record_volcano_preparation', 'check_volcano_damage', 'check_volcano_patterns',
              'probe_volcano_reference', 'probe_volcano_factory_reference', 'probe_volcano_landing_reference', 'probe_volcano_damage_reference']]
    inputs += [audit_path, ROOT / 'tools/reference/first-loop-affix-audit.json', content, integration,
               STAGE / 'python-tests.log', STAGE / 'audit-refresh-native.log']
    result = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
                  scope='Prepared actual-assembly endpoint suppliers and immutable emission/flight plans; no Volcano world actor, learned handler or class completion.',
                  installedHandlerCount=161, installedDruidHandlers=5, classComplete=False,
                  baselineCommit=BASELINE, proofHashes=proofs,
                  inputHashes={str(path.relative_to(ROOT)): sha(path) for path in inputs},
                  validation=dict(sourceLaunchEmitterChild=5730, sourceFactory=4536, sourceTerminalUpdate=523,
                                  sourceDamage=82620, actualContentEndpointSets=55080, actualPlanEmitter=906,
                                  actualPlanSequentialFrames=150, actualPlanFactory=216, priorFireBlastLifetime=39601,
                                  pythonTests=70, namedAuditInputs=591, firstLoopAuditInputs=21, unchangedNamedDecisions=573,
                                  nativeFamilies=['first-loop-catalogue-check', 'named-gear-check'],
                                  fullClassRegression=False, savedVolcanoActor=False, actualVolcanoReloads=0),
                  remaining=[
                      'Implement saved vent, mode0 debris and independent lava actors. Add root Collision1 projection before exporting the missile row.',
                      'Compose actual motion/room/terminal landing, physical/fire contact/defense/knockback and explicit modern NHD grouping; source proofs retain declared services.',
                      'Connect SC/right/quick learned casts,25mana once at release and100-frame local delay. Verify cancellation, natural timers, offline/old-facet aging and two actual reloads.',
                      'Run focused/full native regression and fresh audits before any installation; preserve saves/configuration and retain rollback.',
                      'Keep current-executable/client/event/geometry limits and final effects/audio/icons separate. Boulder terminal burning crater correction and Arctic Blast/channel remain individual follow-ups.'
                  ])
    out = ROOT / 'tools/reference/volcano-prepared-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Volcano preparation verified: source/supplier/plans,70Python tests and591/21fresh native audit inputs; no world actor/handler/installation. Installed coverage161/Druid5.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
