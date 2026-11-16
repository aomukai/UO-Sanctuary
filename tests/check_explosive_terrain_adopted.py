#!/usr/bin/env python3
"""Approved explosive terrain with actual ground casts and retained spell scopes."""
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients
from check_explosive_terrain import configuration as prepared

STAGE = ROOT / '.local/class-skills/explosive-terrain-adopted'
WALL_INPUTS = tuple('explosive-terrain-' + spell + '-' + mode
                   for spell in ('fireball', 'glacial') for mode in ('right', 'quick'))


def configuration():
    config = prepared()
    config['stage'] = STAGE
    config['extra_target_input_commands'] += WALL_INPUTS
    config['extra_inputs'] += (Path(__file__).resolve(),
        ROOT / 'tools/reference/explosive-terrain-rule-approval.json')
    return config


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(**configuration())
