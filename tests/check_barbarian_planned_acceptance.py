#!/usr/bin/env python3
"""Current Barbarian class, selected hands, cast clock and natural potion release."""
import argparse
import json
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_barbarian_current import configuration as current


def configuration():
    config = current()
    config['stage'] = ROOT / '.local/class-skills/barbarian-planned-acceptance'
    config['extra_families'] += ('weapon-hands',)
    config['extra_manifest_names']['weapon-hands'] = 'weapon-hands.json'
    config['extra_target_input_commands'] += ('barbarian-cast-clock-right', 'barbarian-cast-clock-quick')
    config['extra_live_commands'] = ('find-potion-live',)
    config['checked_live_commands'] = ('find-potion-live',)
    config['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tools/reference/item-content-scope-decision.json')
    assert 'barbarian-cast-clock-check' in config['extra_checks']
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
            'extra_families', 'extra_checks', 'extra_target_input_commands',
            'extra_live_commands', 'extra_files')}, indent=2))
        print('Existing save/snapshot callbacks and approved rules; cached references only; no receipt written.')
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        check(**config)
