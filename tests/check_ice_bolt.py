#!/usr/bin/env python3
"""Ice Bolt cold/contact/input/reload coverage with retained Fire Bolt checks."""
import os
from pathlib import Path
import sys

from check_iron_golem_birth import ROOT, check
from check_fire_bolt import configuration as fire


def configuration(full=False):
    config = fire(full)
    config['stage'] = ROOT / '.local/class-skills/sorceress-ice-bolt'
    config['extra_families'] += ('ice-bolt',)
    config['extra_wait_results']['ice-bolt-reload-check'] = 'ice-bolt-reload-result'
    config['extra_connected_checks'] += ('ice-bolt-reload-check',)
    config['extra_target_input_commands'] += ('ice-bolt-right', 'ice-bolt-quick')
    config['extra_files'] = {**config.get('extra_files', {}),
        'legacy-fire-bolt-fixture.json': ROOT / '.local/class-skills/sorceress-fire-bolt/fire-bolt-fixtures.json'}
    config['extra_inputs'] += (Path(__file__).resolve(),)
    return config


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
