#!/usr/bin/env python3
"""Current Paladin mechanics, bindings and SC/Holy Shield save composition.

Retain the existing Paladin cohort without rerunning the accepted Sorceress
suite. The existing Holy Shield coordinator retains its Teleport save fixture.
"""
import argparse
import json
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_paladin_aura_persistence import configuration as auras


def configuration():
    config = auras(False)
    config['stage'] = ROOT / '.local/class-skills/paladin-planned-acceptance'
    config['extra_families'] += ('holy-shield', 'paladin-binding')
    config['extra_checks'] += ('paladin-cast-clock-check',)
    config['extra_wait_results'].update({
        'holy-shield-check': 'holy-shield-result',
        'holy-shield-reload-check': 'holy-shield-reload-result',
    })
    config['extra_manifest_names'].update({
        'holy-shield': 'holy-shield-checks.json',
        'paladin-binding': 'paladin-binding-fixtures.json',
    })
    config['extra_files'].update({
        'paladin-binding-reference.json': ROOT / 'tools/reference/paladin-binding-reference.json',
        'paladin-cast-clock-source-probe.json': ROOT / '.local/class-skills/paladin-cast-clock-source-probe.json',
    })
    config['extra_target_input_commands'] += ('paladin-cast-clock-right', 'paladin-cast-clock-quick')
    config['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tools/reference/item-content-scope-decision.json')
    assert 'player-teleport' in config['extra_families']
    assert config['save_command'] == config['save_result'] == 'holy-shield-cast-save'
    for key in ('extra_families', 'extra_checks', 'extra_target_input_commands'):
        assert len(config[key]) == len(set(config[key])), key
    for path in config['extra_files'].values():
        assert path.is_file(), path
    return config


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    config = configuration()
    if args.plan:
        print(json.dumps({key: len(config[key]) for key in (
            'extra_families', 'extra_checks', 'extra_target_input_commands', 'extra_files')}, indent=2))
        print('Cached references and existing Holy Shield/Teleport save coordinator; no receipt written.')
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        check(**config)
