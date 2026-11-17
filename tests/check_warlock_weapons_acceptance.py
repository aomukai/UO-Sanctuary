#!/usr/bin/env python3
"""Warlock weapon, Hex and Ward routes using existing native/world/input controls."""
import argparse
import json
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/warlock-weapons-acceptance'
ROUTES = ('Hex: Bane', 'Cleave', 'Echoing Strike', 'Hex: Purge', 'Blade Warp',
          'Psychic Ward', 'Eldritch Blast', 'Hex: Siphon', 'Mirrored Blades')
FAMILIES = ('bane-cast', 'siphon-cast', 'hex-group',
    'cleave-sweep', 'cleave-direction', 'cleave-action', 'cleave-cast',
    'echoing-strike-inputs', 'echoing-strike-world', 'echoing-strike-cast',
    'purge-cast', 'purge-reaction', 'weapon-landed', 'blade-warp', 'ward-cast',
    'eldritch-blast-world', 'eldritch-blast-cast', 'magic-arrow-cast',
    'mirrored-packets', 'mirrored-world', 'mirrored-cast')
CONNECTED = ('echoing-strike-world', 'echoing-strike-cast', 'purge-cast',
             'eldritch-blast-world', 'eldritch-blast-cast', 'mirrored-world', 'mirrored-cast')
INPUTS = ('bane-cast-right', 'bane-cast-quick', 'siphon-cast-right', 'siphon-cast-quick',
    'cleave-cast-left', 'cleave-cast-right', 'cleave-cast-quick',
    'echoing-strike-cast-left', 'echoing-strike-cast-right', 'echoing-strike-cast-quick',
    'purge-cast-right', 'purge-cast-quick',
    'blade-warp-left', 'blade-warp-right', 'blade-warp-quick',
    'ward-cast-right', 'ward-cast-quick',
    'eldritch-blast-cast-left', 'eldritch-blast-cast-right', 'eldritch-blast-cast-quick',
    'mirrored-cast-left', 'mirrored-cast-right', 'mirrored-cast-quick', 'mirrored-crossbow-quick')


def configuration():
    files = {
        'bane-zero-legacy.json': ROOT / '.local/class-skills/bane-damage-proposal/bane-damage-proposal-fixtures.json',
        'cleave-sweep-cases.json': ROOT / '.local/class-skills/cleave-sweep-source/cases.json',
        'cleave-direction-cases.json': ROOT / '.local/class-skills/cleave-direction-source/cases.json',
        'magic-arrow-action-source-probe.json': ROOT / '.local/class-skills/magic-arrow-action-source-probe.json',
    }
    names = tuple(family + suffix for family in CONNECTED
                  for suffix in ('-check', '-reload-check'))
    inputs = [Path(__file__).resolve(), ROOT / 'tests/check_assassin_planned_acceptance.py']
    inputs += [ROOT / 'tests' / ('check_' + name + '.py') for name in (
        'hex_bane_cast', 'hex_siphon_cast', 'hex_purge_cast', 'cleave_cast',
        'echoing_strike_cast', 'mirrored_cast', 'blade_warp',
        'psychic_ward_cast', 'eldritch_blast_cast')]
    inputs += [ROOT / 'tools/reference' / name for name in (
        'weapon-hex-groups-approval.json', 'hex-bane-damage-approval.json',
        'hex-purge-reaction-approval.json', 'cleave-action-approval.json',
        'echoing-strike-world-rule-approval.json', 'mirrored-world-rule-approval.json',
        'blade-warp-adaptation.json', 'psychic-ward-reaction-approval.json',
        'eldritch-blast-world-rule-approval.json')]
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
        extra_manifest_names={'magic-arrow-cast': 'magic-arrow-cast.json'},
        extra_connected_checks=names, extra_wait_results={name: name + '-result' for name in names},
        extra_connection_settle=.15, extra_target_input_commands=INPUTS,
        extra_ground_input_commands=('cleave-cast-right',),
        extra_pre_save_commands=('cleave-cast-presave-check',), extra_inputs=tuple(inputs))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    config = configuration()
    if args.plan:
        print(json.dumps(dict(routes=ROUTES, fixtureFamilies=len(FAMILIES),
            currentInputCommands=len(INPUTS), connectedFamilies=len(CONNECTED),
            cachedReferenceFiles=len(config['extra_files']),
            sharedRegression='Existing Magic Arrow weapon/missile path',
            saves='Original actors, gear, resources, held buffs and once-only Cleave save staging'), indent=2))
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        with paced_clients():
            check(**config)
