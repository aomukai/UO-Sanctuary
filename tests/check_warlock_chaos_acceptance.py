#!/usr/bin/env python3
"""Compose existing Warlock Chaos/Sigil controls on accepted native artifacts."""
import argparse
import json
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/warlock-chaos-acceptance'
ROUTES = ('Miasma Bolt', 'Ring of Fire', 'Sigil: Lethargy', 'Sigil: Rancor',
          'Miasma Chain', 'Flame Wave', 'Sigil: Death', 'Enhanced Entropy',
          'Apocalypse', 'Abyss')
FAMILIES = ('miasma-packets', 'miasma-world', 'miasma-cast',
    'miasma-chain-world', 'miasma-chain-cast', 'flame-wave-world', 'flame-wave-cast',
    'abyss-world', 'abyss-cast', 'entropy-cloud', 'ring-profile', 'ring-world',
    'ring-cast', 'sigil-tracking', 'sigil-launch', 'sigil-actor', 'sigil-lethargy',
    'sigil-cast', 'rancor-mind', 'rancor-pulse', 'rancor-cast', 'sigil-family',
    'sigil-death', 'death-cast', 'corpse-town', 'apocalypse-ground',
    'apocalypse-potions', 'apocalypse-cast')
CONNECTED = ('miasma-world', 'miasma-cast', 'miasma-chain-world', 'miasma-chain-cast',
    'flame-wave-world', 'flame-wave-cast', 'abyss-world', 'abyss-cast',
    'ring-cast', 'sigil-actor', 'sigil-lethargy', 'sigil-cast', 'rancor-pulse',
    'rancor-cast', 'sigil-family', 'sigil-death', 'death-cast', 'apocalypse-cast')
INPUTS = tuple(family + '-' + mode for family in (
    'miasma-cast', 'miasma-chain-cast', 'flame-wave-cast', 'abyss-cast',
    'ring-cast', 'sigil-cast', 'rancor-cast', 'death-cast', 'apocalypse-cast')
    for mode in ('left', 'right', 'quick'))
CHECKS = ('miasma-chain-birth-check', 'flame-wave-birth-check')


def configuration():
    files = {'miasma-packets-source-probe.json': ROOT / '.local/class-skills/miasma-packets-source-probe.json',
        'ring-profile-cases.json': ROOT / '.local/class-skills/ring-profile-source/cases.json'}
    for name in ('sigil-tracking', 'sigil-launch', 'sigil-lethargy'):
        files[name + '-cases.json'] = ROOT / '.local/class-skills' / (name + '-source') / 'cases.json'
    names = tuple(family + suffix for family in CONNECTED
                  for suffix in ('-check', '-reload-check'))
    inputs = [Path(__file__).resolve(), ROOT / 'tests/check_assassin_planned_acceptance.py']
    inputs += [ROOT / 'tests' / ('check_' + name + '.py') for name in (
        'abyss_cast', 'miasma_cast', 'miasma_chain_cast', 'flame_wave_cast',
        'entropy_clouds', 'ring_cast', 'ring_profile', 'sigil_cast',
        'sigil_family', 'rancor_cast', 'death_cast', 'apocalypse_cast')]
    inputs += [ROOT / 'tools/reference' / name for name in (
        'miasma-bolt-world-rule-approval.json', 'miasma-chain-world-rule-approval.json',
        'flame-wave-world-rule-approval.json', 'abyss-world-rule-approval.json',
        'ring-world-approval.json', 'rancor-pulse-approval.json',
        'sigil-death-rule-approval.json', 'sigil-lifecycle-approval.json',
        'item-content-scope-decision.json')]
    for path in (*files.values(), *inputs):
        assert path.is_file(), path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    rows = [row for row in status['skills'] if row['characterClass'] == 'Warlock']
    assert len(rows) == 30
    assert all(next(row for row in rows if row['name'] == name)['implementation']
        == 'handler-present' for name in ROUTES)
    approval = json.loads((ROOT/'tools/reference/warlock-final-talents-approval.json').read_text())
    assert approval['approved'] and len(approval['approvedTalents']) == 4
    assert all(row['implementation'] == 'handler-present' for row in rows)
    assert status['contentScope']['charms'] is False and status['contentScope']['runewords'] is False
    return dict(stage=STAGE, extra_files=files, extra_families=FAMILIES,
        extra_manifest_names={'miasma-packets': 'miasma-packet-fixtures.json'},
        extra_checks=CHECKS, extra_connected_checks=names,
        extra_wait_results={name: name + '-result' for name in names},
        extra_connection_settle=.15, extra_target_input_commands=INPUTS,
        extra_inputs=tuple(inputs))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    config = configuration()
    if args.plan:
        print(json.dumps(dict(routes=ROUTES, fixtureFamilies=len(FAMILIES),
            currentInputCommands=len(INPUTS), connectedFamilies=len(CONNECTED),
            sharedChecks=len(CHECKS), cachedReferenceFiles=len(config['extra_files']),
            expectedMarkers=315, expectedConnections=135,
            saves='Original actors, captured clouds/sigils/fields, resources and no replay; three saves/two reloads'), indent=2))
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        with paced_clients():
            check(**config)
