#!/usr/bin/env python3
"""Ordinary Enchant input and original saved recipients with retained contracts."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT, check
from check_telekinesis_combat import configuration as telekinesis


def configuration(full=False):
    config = telekinesis(full)
    config['stage'] = ROOT / '.local/class-skills/sorceress-enchant-cast'
    config['extra_families'] += ('enchant-cast',)
    config['extra_target_input_commands'] += tuple('enchant-' + target + '-' + mode
        for target in ('self', 'ally', 'hostile') for mode in ('right', 'quick'))
    config['extra_files']['enchant-source-cases.json'] = ROOT / '.local/class-skills/enchant-source/cases.json'
    config['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tools/reference/enchant-source-evidence.json')
    return config


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
