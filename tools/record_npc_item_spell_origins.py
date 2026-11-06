#!/usr/bin/env python3
"""Bind complete outgoing NPC item-origin traces and retained native consumers."""
import argparse
from collections import Counter
from pathlib import Path

import skill_checkpoint_evidence as e
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'npc-item-spell-origins'
WIDE = BASE / 'npc-item-spell-origins-retained'
PARENT = 'e414e09586966a011d05b0ced01450236fac0e6e'


def record(check=False):
    born = e.inventory(STAGE, WIDE, STAGE / 'source-snapshot-2.json')
    prior = e.load(ROOT / 'tools/reference/npc-item-chain-native-evidence.json')
    extra = Counter({'npc-item-spell-origins-check': 3})
    for previous, stage, count in [(BASE/'npc-item-chain', STAGE, 78), (BASE/'npc-item-chain-retained', WIDE, 386)]:
        log = previous / 'birth-native-checks.log'
        assert e.sha(log) == prior['files'][str(log.relative_to(ROOT))]
        current = e.cohort(stage, markers(log) + extra, count)
        assert current.count('NPC item origins:14112 complete source contexts;shared executor,own NPC factory coordinates/AR,all64 ring directions,allocation failure/return and actual Chain handle budget;no player mana/RNG,callbacks or world allocations claimed') == 3
        assert 'pass across3boots/2reloads' in (stage/'native-run-2.log').read_text()
        failed = stage / 'failed-manifest-driver-1'
        assert 'FileNotFoundError' in (failed/'native-run-1.log').read_text()
        assert 'npc-item-spell-origins-fixtures.json' in (failed/'native-run-1.log').read_text()
        assert ': failed' not in (failed/'birth-native-checks.log').read_text()
        assert e.load(failed/'birth-inputs.json')['artifacts'] == born['artifacts']
        saved = e.load(stage/'npc-item-chain-fixtures.json')
        assert saved['Generation'] == 2 and len(saved['Flights']) == len(saved['Held']) == 2 and len(saved['Items']) == 7
        assert {f['OriginalNpc']['Ordinal'] for f in saved['Flights']} == {0, 1}
        assert {f['Frame'] for f in saved['Flights']} == {1, 2}
    for name in ('content-build-1.log', 'integration-build-2.log'):
        assert all(t in (STAGE/name).read_text() for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    failed = STAGE / 'failed-fixture-build-1'
    assert 'error CS0029' in (failed/'integration-build-1.log').read_text()
    before = e.load(failed/'source-snapshot-1.json')['inputs']
    after = e.load(STAGE/'source-snapshot-2.json')['inputs']
    assert before['copiedData'] == after['copiedData'] and before['projectInputs'] == after['projectInputs']
    assert {p for p,h in before['sourceInputs'].items() if after['sourceInputs'][p] != h} == {'tests/Sanctuary.Integration/NpcItemSpellOriginsChecks.cs'}
    sourcepath = ROOT / 'tools/reference/npc-item-spell-origins-source-evidence.json'
    source = e.load(sourcepath)
    assert source['cases'] == 14112 and source['completeRoutines'] == len(source['routineHashes']) == 7
    for group in ('inputs', 'currentTable', 'artifacts'):
        for p,h in source[group].items():
            assert e.sha(ROOT/p) == h, p
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    commands = e.load(STAGE/'verification-commands.json')
    assert len(commands) == 7 and all(c['exitCode'] == 0 for c in commands)
    fingerprints = e.audits(STAGE, PARENT, born['artifacts'])
    protected = preservation()
    assert protected == e.load(STAGE/'protection-check.json')
    client = e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json')
    assert len(client['patchHashes']) == 41
    for group in ('patchHashes', 'sharedInputs'):
        for p,h in client[group].items():
            assert e.sha(ROOT/p) == h, p
    status = e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 202 and not status['completedClasses']
    assert next(s for s in status['skills'] if s['talentId'] == 'amazon.valkyrie')['implementation'] == 'blocked'
    files = e.stage_files(STAGE, WIDE) + [Path(__file__).resolve(), sourcepath,
        ROOT/'tests/check_npc_item_spell_origins.py', ROOT/'src/Sanctuary.Content/NpcItemSpellOrigins.cs',
        ROOT/'src/Sanctuary.Content/ItemSkillExecution.cs', ROOT/'tests/Sanctuary.Integration/NpcItemSpellOriginsChecks.cs']
    for stage, sub in [(STAGE,'failed-fixture-build-1'), (STAGE,'failed-manifest-driver-1'), (WIDE,'failed-manifest-driver-1')]:
        files.extend(p for p in (stage/sub).rglob('*') if p.is_file())
    files += [ROOT/'docs'/name for name in ['npc-item-spell-origins-checkpoint.md', 'class-skills-resume-checkpoint.md',
        'class-skill-roadmap.md', 'skill-mechanical-reference.md', 'skill-effects.md', 'skill-icons.md']]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, installed=False, handlers=202, amazonHandlers=28,
        completedClasses=[], learnedValkyrie=False, originalProcDispatch=False, currentExecutableParity=False,
        sourceSnapshot=2, contentBuild=1, integrationBuild=2, focusedRun=2, retainedRun=2, auditRun=1,
        focusedMarkers=78, retainedMarkers=386, sourceCases=14112, sourceRoutines=7,
        bootsPerCohort=3, reloadsPerCohort=2, savesPerCohort=3, originComparisonsPerCohort=42336,
        pythonTests=71, verificationCommands=7, auditFingerprints=fingerprints, protection=protected,
        clientPatchesUnchanged=41, artifacts=born['artifacts'], copiedData=born['copiedData'],
        scope=[
            'A synchronous explicit NPC item factory frame reuses the common SkillExecution and current metadata. Fire Bolt and ordinary/alternate Nova/Frost Nova/Chain do bodies retain target/path/zero coordinates, own supplied AR, server-do state and real allocation-handle budget ordering.',
            'Seven complete older bodies pass14112source contexts;each native cohort repeats all contexts on all three boots. Failed direct/ring allocations retain source return/continuation semantics;failed Chain roots do not receive a budget. Invalid rank/flag and unadmitted do17 refuse before allocations.',
            'All75focused/383retained parent markers remain,including original item/resources/wounds/RNG and partially moved root/child flight persistence. The new origin test is stateless;existing world fixtures still run two actual reloads and three saves.'],
        limits=[
            'The factory allocator and actual-handle budget writer are explicit host services. This stage does not bind them to live projectile creation,shared game seed,full item-graph callback history/order or item wrapper identity restoration. No original proc admission or learned cast is enabled.',
            'Prepared ordinary flight launchers retain their actor origins. Outgoing proc origins and partial ring allocation failure must be bound explicitly rather than routing these new requests blindly through complete ordinary batches. Charged Bolt do17,nonzero NPC shields,current executable callback/traversal/NHD and source-removal behavior remain separate.',
            'One test callback-parameter compile error and two stateless-fixture manifest driver failures are archived. The corrected Python configuration registers a stateless check on every boot;accepted C# production Content1 is unchanged by both fixture repairs. Final VFX/audio/icons,AI/startup/initializer/world placement/payment/caps/replacement/client acceptance remain open.'])
    e.write('npc-item-spell-origins-native-evidence.json', result, files, check)
    print('NPC item origins:14112source contexts/7routines,78focused/386retained markers,two real reloads;202handlers,world/proc/cast still gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
