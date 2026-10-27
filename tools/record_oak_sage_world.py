#!/usr/bin/env python3
"""Bind Oak's actual saved body and life aura, including shared-resource regression."""
import argparse
import json
from pathlib import Path
from record_oak_sage_stats import record as numeric_record, sha

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/oak-sage'


def record(check=False):
    numeric_record(check)
    numeric = json.loads((ROOT / 'tools/reference/oak-sage-native-evidence.json').read_text())
    log = (STAGE / 'stats-native-checks.log').read_text()
    for prefix in ['oak-sage-body', 'battle-orders']:
        assert log.count(prefix + '-check: passed') == 1 and log.count(prefix + '-reload-check: passed') == 2
    assert '25magic' in log and 'additive BO' in log
    saved = json.loads((STAGE / 'oak-sage-body-fixtures.json').read_text())
    assert saved['Body']['Owner'] == saved['Owner'] and saved['Body']['Profile'] == 'source-birth-no-movement-v1'
    assert saved['Body']['Birth']['Rank'] == saved['Body']['Birth']['AuraRank'] == 1
    assert saved['Body']['Birth']['Life256'] == 15360 and saved['Body']['Birth']['Regeneration256'] == 11
    assert (saved['Life'], saved['Maximum'], saved['Regeneration']) == (6435, 19968, 14)
    assert saved['Self']['Recipient'] == saved['Self']['Creator'] == saved['Self']['Supplier'] == saved['Oak']
    assert saved['Self']['Control'] and not saved['Recipient']['Control']
    assert saved['Recipient']['Recipient'] == saved['Owner'] and saved['Recipient']['Percent'] == saved['Self']['Percent'] == 30
    engine = json.loads((ROOT / 'tools/reference/oak-aura-engine-evidence.json').read_text())
    assert engine['fixture']['cases'] == 440
    assert sha(ROOT / engine['fixture']['path']) == engine['fixture']['sha256']
    assert sha(ROOT / '.local/class-skills/oak-aura-source-probe.cpp') == engine['fixture']['generatedSourceSha256']
    for name, digest in engine['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / name) == digest
    for name, digest in engine['reusedEvidenceHashes'].items():
        assert sha(ROOT / 'tools/reference' / name) == digest
    full = (STAGE / 'class-native-checks.log').read_text()
    assert ' ERR]' not in full and ': failed' not in full
    assert 'Native class skills and projectile checks passed, including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
    assert full.count('Shutting down') == 3
    full_audits = {}
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / ('full-' + name)
        full_audits[name] = path.is_file()
        if path.is_file():
            assert json.loads(path.read_text()) == json.loads((STAGE / name).read_text())
        elif name == 'named-item-runtime-audit.json':
            assert full.count('named-gear-check: passed') == full.count('named-gear-reload-check: passed') == 0
        else:
            assert full.count('first-loop-catalogue-check: passed') == 0
    artifacts = [ROOT / 'tools/reference' / name for name in ['oak-sage-native-evidence.json', 'oak-aura-engine-evidence.json']]
    artifacts += [STAGE / name for name in ['oak-sage-body-fixtures.json', 'class-native-checks.log', 'full-run.log']]
    artifacts += [STAGE / ('full-' + name) for name, exists in full_audits.items() if exists]
    artifacts += [ROOT / name for name in ['tests/check_class_skills.py', 'tools/probe_oak_aura_reference.py', 'tools/record_oak_sage_world.py']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        profile='source-birth-no-movement-v1',
        scope='Actual saved vulnerable Oak body and NPC life aura in a disposable world; Totem movement, shared cap and learned input remain unfinished.',
        verified=dict(**numeric['verified'], auraSourceContexts=440, actualOakBoots=3, actualOakReloads=2,
            retainedBattleOrdersReloads=2, fullClassBoots=3, fullClassReloads=2, fullClassPassMarkers=full.count(': passed'),
            fullClassNamedAuditRepeated=full_audits['named-item-runtime-audit.json'],
            fullClassFirstLoopAuditRepeated=full_audits['first-loop-affix-audit.json']),
        artifacts={str(path.relative_to(ROOT)): sha(path) for path in artifacts},
        testedAssemblies=numeric['testedAssemblies'],
        established=[
            'Prepared body is vulnerable, nonattacking, saved with owner/history and source rank/level/difficulty. Normal poison170and Nightmare/Hell200retain the raw100passive plus base; magic25uses the existing typed damage consumer. Size2, no native GH, drain/chill metadata and ordinary aura eligibility are explicit source mappings.',
            'NPC298self/control161and recipient149leases are separate, highest rank per state with equal-rank creator retention and supplier refresh. Five-frame periodic refresh and six-frame expiry use current aura values; town recipients, hostile/dead/outside targets and isAtt0vines are excluded. LOS is not added to0x10103.',
            'Oak joins the additive maximum-life percentage pool with Battle Orders and item life percent. Mana/stamina remain unchanged by Oak. Existing exact-life wolf/creeper/decoy/conversion and ordinary creature maximum paths share the life-only contribution. Current life and monster regeneration use the existing resource callback on application/removal/expiry.',
            'Actual injury, rank/expiry/source state, overlapping self/recipient lists, boundary radius, town-self behavior, mutable owner gear versus immutable birth, offline no-healing, wardrobe/facet/orphan/death and two real Oak reloads pass. The fixture only holds future inspection clocks and disables native healing on its named player; production recovery is untouched.',
            'The shared resource changes additionally pass the full retained class suite with three boots/two reloads. Optional named-gear commands were not enabled in this full run; the same package has a fresh573definition named audit from the focused run. The first-loop export is repeated in the full run and compared exactly. This does not certify a completed class or current D2R executable parity.'
        ], adapters=[
            'Native2source subtiles per tile,16height tolerance,map-wide rooms,Sanctuary ally policy and deterministic serial/state enumeration retain existing geometry/allegiance adapters; modern event/global-RNG/equal-deadline order is unverified.',
            'Aura resumption schedules a new five-frame period without replaying missed pulses. The current executable resume phase and whole-frame recovery versus aura ordering are not established.',
            'The body is temporarily stationary and silent UO wisp0x3A,named Oak Sage [placeholder]. No final aura overlay/audio is emitted.'
        ], remaining=[
            'Implement Totem AI/movement/escape/follow/teleport and active owner-facet transport; preserve shared source motion/path/history services and identify unsupported geometry/path cases.',
            'Shared totem cap/replacement, successful allocation/payment transaction, learned SC/FCR cancellation/right/quick input, connected natural actor/aura clock checks, graphical client acceptance and preserved installation.',
            'Staffmod-only permission,Battle Command/exceptional summon gear and complete incoming state/PvP/unmapped recipient acceptance remain outside this prepared route. Exact modern AI/event/timing/geometry is not certified.',
            'Final XWbody/emergence/movement/hurt/death,oak_sage recipient/control overlays and summon/spirit audio remain in the effects register; talent/right/quick icons remain independent.'
        ])
    output = ROOT / 'tools/reference/oak-sage-world-native-evidence.json'; rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Oak saved body/life aura:440source contexts,3boots/2actual Oak reloads,{full.count(": passed")}full-class markers; learned input/movement remain unavailable.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
