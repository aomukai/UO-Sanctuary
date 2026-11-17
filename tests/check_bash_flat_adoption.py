#!/usr/bin/env python3
"""Qualify adopted Bash damage and retained four-skill ordinary contacts."""
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(stage=ROOT / '.local/class-skills/bash-flat-adoption',
              extra_families=('barbarian-attacks', 'barbarian-contact'),
              extra_manifest_names={'barbarian-attacks': 'barbarian-attacks.json'},
              extra_wait_results={'barbarian-attacks-check': 'barbarian-attacks-result'},
              extra_target_input_commands=tuple(f'barbarian-contact-{skill}-{mode}'
                  for skill in ('bash', 'stun', 'concentrate', 'berserk') for mode in ('right', 'quick')),
              extra_inputs=(Path(__file__).resolve(),), between_boot_delay=1.1)
