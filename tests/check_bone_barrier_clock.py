#!/usr/bin/env python3
"""Barrier AI clocks and retained maker/cast/birth/Wolf regression scopes."""
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    smoke='--smoke' in sys.argv
    check(stage=ROOT/'.local/class-skills/bone-barrier-clock',
        extra_files={
            'bone-barrier-retirement-source-cases.json':ROOT/'.local/class-skills/bone-barrier-retirement-source/cases.json',
            'bone-wall-maker-source-cases.json':ROOT/'.local/class-skills/bone-wall-maker-source/cases.json',
            'bone-barrier-cast-source-cases.json':ROOT/'.local/class-skills/bone-barrier-cast-source/cases.json',
            'bone-barrier-birth-source-cases.json':ROOT/'.local/class-skills/bone-barrier-birth-source/cases.json'},
        extra_families=('bone-barrier-clock',) if smoke else ('bone-barrier-clock','bone-wall-maker','bone-barrier-cast','bone-barrier-birth','spirit-wolf-casting','bone-barriers'),
        extra_wait_results={'bone-barrier-clock-check':'bone-barrier-clock-result','bone-barrier-clock-reload-check':'bone-barrier-clock-reload-result'} if smoke else {'bone-barrier-clock-check':'bone-barrier-clock-result','bone-barrier-clock-reload-check':'bone-barrier-clock-reload-result','bone-wall-maker-check':'bone-wall-maker-result','bone-wall-maker-reload-check':'bone-wall-maker-reload-result','bone-barriers-check':'bone-barriers-result'},
        extra_manifest_names={'bone-barriers':'bone-barrier-checks.json'},
        extra_target_input_commands=() if smoke else ('bone-wall-target-right','bone-prison-self-quick','spirit-wolf-right'),
        extra_ground_input_commands=() if smoke else ('spirit-wolf-right',),
        extra_input_timeouts={'bone-wall-target-right':30,'bone-prison-self-quick':30,'spirit-wolf-right':10},
        save_command='bone-barrier-clock-save',save_result='bone-barrier-clock-save',extra_inputs=(Path(__file__).resolve(),))
