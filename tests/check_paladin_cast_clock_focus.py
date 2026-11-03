#!/usr/bin/env python3
"""Focused three-boot Paladin clock/gear/input/reload diagnostic acceptance."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/paladin-cast-clock-focus',
        extra_files={'paladin-cast-clock-source-probe.json':ROOT/'.local/class-skills/paladin-cast-clock-source-probe.json',
            'holy-shield-cast-source-cases.json':ROOT/'.local/class-skills/holy-shield-cast-source/cases.json'},
        extra_checks=('paladin-cast-clock-check',),extra_families=('holy-shield-cast',),
        extra_manifest_names={'holy-shield-cast':'holy-shield-cast-checks.json'},save_command='holy-shield-cast-save',save_result='holy-shield-cast-save',
        extra_target_input_commands=('paladin-cast-clock-right','paladin-cast-clock-quick','holy-shield-cast-right','holy-shield-cast-quick'),extra_inputs=(Path(__file__).resolve(),))
