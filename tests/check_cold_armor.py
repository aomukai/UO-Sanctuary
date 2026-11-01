#!/usr/bin/env python3
"""Cold armor casting/Frozen reaction with retained Sorceress and shield groups."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT, check
from check_enchant_cast import configuration as enchant


def configuration(full=False):
    config = enchant(full); config['stage'] = ROOT / '.local/class-skills/sorceress-cold-armor'
    config['extra_families'] += ('cold-armor', 'bone-armor')
    config['extra_checks'] += ('psychic-ward-check',)
    config['extra_target_input_commands'] += tuple('cold-armor-' + a + '-' + mode for a in ('frozen','shiver','chilling') for mode in ('right','quick'))
    config['extra_files']['cold-armor-source-cases.json'] = ROOT / '.local/class-skills/cold-armor-source/cases.json'
    config['extra_files']['bone-armor-source-cases.json'] = ROOT / '.local/class-skills/bone-armor-source/cases.json'
    config['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tools/reference/cold-armor-source-evidence.json')
    return config


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
