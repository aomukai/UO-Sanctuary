#!/usr/bin/env python3
"""Current Sorceress skills, approved held Inferno and shared SC/FCR checks.

Reuse cached reference fixtures and existing save staging. This does not
resolve the separately documented explosive-projectile terrain adapter.
"""
import argparse
import json
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_inferno_held_input import configuration as inferno
from check_sorceress_bindings import configuration as spells


def configuration():
    result = spells(True)
    channel = inferno(True)
    result['stage'] = ROOT / '.local/class-skills/sorceress-planned-acceptance'
    for key in ('extra_families', 'extra_checks', 'extra_live_commands',
                'extra_target_input_commands', 'extra_ground_input_commands',
                'extra_connected_checks', 'extra_inputs', 'checked_live_commands'):
        result[key] = tuple(dict.fromkeys((*result.get(key, ()), *channel.get(key, ()))))
    for key in ('extra_files', 'extra_wait_results', 'extra_manifest_names'):
        for name, value in channel.get(key, {}).items():
            assert name not in result[key] or result[key][name] == value, (key, name)
            result[key][name] = value
    result['extra_wire_callbacks'] = channel['extra_wire_callbacks']
    result['connected_save'] = True
    # This existing command authenticates Inferno's future-epoch press. The
    # Teleport command then stages its unpaid action and performs one save.
    result['extra_pre_save_commands'] = ('inferno-held-input-save-stage-check',)
    assert result['save_command'] == result['save_result'] == 'player-teleport-save'
    result['extra_checks'] += ('sorceress-cast-clock-check',)
    result['extra_files']['sorceress-cast-clock-source-probe.json'] = ROOT / '.local/class-skills/sorceress-cast-clock-source-probe.json'
    result['extra_target_input_commands'] += ('sorceress-cast-clock-right', 'sorceress-cast-clock-quick')
    result['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tools/reference/item-content-scope-decision.json')
    for path in result['extra_files'].values():
        assert path.is_file(), path
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    config = configuration()
    if args.plan:
        print(json.dumps({key: len(config[key]) for key in (
            'extra_families', 'extra_checks', 'extra_live_commands',
            'extra_target_input_commands', 'extra_files', 'extra_connected_checks')}, indent=2))
        print('Existing Teleport save plus connected Inferno staging; no runtime changes or receipt written.')
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        check(**config)
