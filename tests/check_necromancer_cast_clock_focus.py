#!/usr/bin/env python3
"""Focused three-boot Necromancer clock/gear/input/reload diagnostic acceptance."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/necromancer-cast-clock-focus',
        extra_files={'necromancer-cast-clock-source-probe.json':ROOT/'.local/class-skills/necromancer-cast-clock-source-probe.json'},
        extra_checks=('necromancer-cast-clock-check',),
        extra_target_input_commands=('necromancer-cast-clock-right','necromancer-cast-clock-quick'),extra_inputs=(Path(__file__).resolve(),))
