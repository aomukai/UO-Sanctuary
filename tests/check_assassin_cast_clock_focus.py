#!/usr/bin/env python3
"""Focused Assassin SC/FCR and original Blade Shield source/pulse/state reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/assassin-cast-clock-focus',
        extra_files={'assassin-cast-clock-source-probe.json':ROOT/'.local/class-skills/assassin-cast-clock-source-probe.json',
            'blade-shield-damage-source-probe.json':ROOT/'.local/class-skills/blade-shield-damage-source-probe.json'},
        extra_checks=('assassin-cast-clock-check',),extra_families=('blade-shield',),
        extra_wait_results={'blade-shield-check':'blade-shield-result','blade-shield-reload-check':'blade-shield-result'},
        extra_target_input_commands=('assassin-cast-clock-right','assassin-cast-clock-quick'),extra_inputs=(Path(__file__).resolve(),))
