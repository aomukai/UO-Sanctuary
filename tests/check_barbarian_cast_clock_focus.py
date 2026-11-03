#!/usr/bin/env python3
"""Focused Barbarian SC/dual gear/input with original Battle Orders reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/barbarian-cast-clock-focus',
        extra_files={'barbarian-cast-clock-source-probe.json':ROOT/'.local/class-skills/barbarian-cast-clock-source-probe.json',
            'battle-orders-source-probe.json':ROOT/'.local/class-skills/battle-orders-source-probe.json'},
        extra_checks=('barbarian-cast-clock-check',),extra_families=('battle-orders',),extra_manifest_names={'battle-orders':'battle-orders.json'},
        extra_wait_results={'battle-orders-check':'battle-orders-result','battle-orders-reload-check':'battle-orders-result'},
        extra_target_input_commands=('barbarian-cast-clock-right','barbarian-cast-clock-quick'),extra_inputs=(Path(__file__).resolve(),))
