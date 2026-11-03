#!/usr/bin/env python3
"""Focused three-boot Amazon clock/gear/input/reload diagnostic acceptance."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/amazon-cast-clock-focus',
        extra_files={'amazon-cast-clock-source-probe.json':ROOT/'.local/class-skills/amazon-cast-clock-source-probe.json'},
        extra_families=('amazon-cast-clock',),
        extra_target_input_commands=('amazon-cast-clock-right','amazon-cast-clock-quick'),extra_inputs=(Path(__file__).resolve(),))
