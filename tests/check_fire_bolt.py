#!/usr/bin/env python3
"""Player Fire Bolt source-flight, real-input and two-reload controls."""
import os
from pathlib import Path
import sys

from check_iron_golem_birth import ROOT, check
from check_sorceress_acceptance import configuration as baseline


def configuration(full=False):
    config = baseline() if full else {}
    config['stage'] = ROOT / '.local/class-skills/sorceress-fire-bolt'
    config['extra_families'] = (*config.get('extra_families', ()), 'fire-bolt')
    config['extra_wait_results'] = {**config.get('extra_wait_results', {}),
        'fire-bolt-reload-check': 'fire-bolt-reload-result'}
    config['extra_connected_checks'] = ('fire-bolt-reload-check',)
    config['extra_target_input_commands'] = ('fire-bolt-right', 'fire-bolt-quick')
    config['extra_inputs'] = (*config.get('extra_inputs', ()), Path(__file__).resolve())
    return config


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
