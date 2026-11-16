#!/usr/bin/env python3
"""Prepared terrain proposal with unchanged Fire Ball/Glacial learned regressions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/explosive-terrain'


def configuration():
    return dict(stage=STAGE,
        extra_families=('fire-ball', 'glacial-spike', 'explosive-terrain'),
        extra_connected_checks=('fire-ball-reload-check', 'glacial-spike-reload-check'),
        extra_wait_results={'fire-ball-reload-check': 'fire-ball-reload-result',
                            'glacial-spike-reload-check': 'glacial-spike-reload-result'},
        extra_target_input_commands=('fire-ball-right', 'fire-ball-quick',
                                     'glacial-spike-right', 'glacial-spike-quick'),
        extra_connection_settle=.15,
        extra_inputs=(Path(__file__).resolve(), ROOT / 'tests/check_assassin_planned_acceptance.py',
            ROOT / 'tests/check_fire_ball.py', ROOT / 'tests/check_glacial_spike.py'))


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(**configuration())
