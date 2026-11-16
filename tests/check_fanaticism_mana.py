#!/usr/bin/env python3
"""Prepared Fanaticism mana rule beside unchanged offensive aura consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/fanaticism-mana'
INPUTS = tuple('offensive-aura-' + aura + '-' + mode
               for aura in ('concentration', 'fanaticism') for mode in ('right', 'quick'))


def configuration():
    return dict(stage=STAGE,
        extra_families=('paladin-offense', 'fanaticism-mana'),
        extra_manifest_names={'paladin-offense': 'paladin-offense-checks.json'},
        extra_checks=('offensive-aura-check',),
        extra_wait_results={'paladin-offense-check': 'paladin-offense-result',
            'paladin-offense-reload-check': 'paladin-offense-reload-result',
            'fanaticism-mana-reload-check': 'fanaticism-mana-reload-check-result'},
        extra_connected_checks=('fanaticism-mana-reload-check',),
        extra_target_input_commands=INPUTS, extra_instant_input_commands=INPUTS,
        extra_connection_settle=.15,
        extra_inputs=(Path(__file__).resolve(), ROOT / 'tests/check_assassin_planned_acceptance.py',
            ROOT / 'tests/check_offensive_aura.py', ROOT / 'tools/reference/paladin-offense-evidence.json',
            ROOT / 'tools/reference/offensive-aura-callback-source-evidence.json'))


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(**configuration())
