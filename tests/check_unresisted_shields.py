#!/usr/bin/env python3
"""Native armor-ignore shield producer with existing source/input/save controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/unresisted-shields'


def configuration():
    return dict(stage=STAGE,
        extra_families=('energy-shield', 'bone-armor-rule', 'unresisted-shields'),
        extra_checks=('encounter-shields-check',),
        extra_target_input_commands=('energy-shield-right', 'energy-shield-quick'),
        extra_files={
            'energy-shield-source-cases.json': ROOT / '.local/class-skills/energy-shield-source-preparation/cases.json',
            'energy-shield-cast-source-cases.json': ROOT / '.local/class-skills/energy-shield-cast-source/cases.json'},
        extra_inputs=(Path(__file__).resolve(), ROOT / 'tests/check_energy_shield.py',
            ROOT / 'tests/check_assassin_planned_acceptance.py',
            ROOT / 'tools/prepare_unresisted_shield_native.py',
            ROOT / 'patches/ModernUO/0075-typed-unresisted-shield-packet.patch',
            ROOT / 'tools/reference/energy-shield-source-evidence.json',
            ROOT / 'tools/reference/energy-shield-cast-source-evidence.json',
            ROOT / 'tools/reference/bone-armor-rule-approval.json'))


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(**configuration())
