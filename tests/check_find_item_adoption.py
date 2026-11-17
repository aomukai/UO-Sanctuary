#!/usr/bin/env python3
"""Approved near-click Find Item, retained treasure/persistence and adopted Bash contacts."""
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(stage=ROOT / '.local/class-skills/find-item-adoption',
              extra_families=('find-item', 'barbarian-attacks', 'barbarian-contact'),
              extra_checks=('find-item-targeting-check',),
              extra_manifest_names={'find-item': 'find-item.json', 'barbarian-attacks': 'barbarian-attacks.json'},
              extra_connected_checks=('find-item-check', 'find-item-reload-check'),
              extra_wait_results={'find-item-check': 'find-item-result', 'find-item-reload-check': 'find-item-result',
                                  'barbarian-attacks-check': 'barbarian-attacks-result'},
              extra_target_input_commands=('find-item-ground-right', 'find-item-ground-quick'),
              extra_ground_input_commands=('find-item-ground-right', 'find-item-ground-quick'),
              extra_inputs=(Path(__file__).resolve(),), between_boot_delay=1.1)
