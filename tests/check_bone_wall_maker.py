#!/usr/bin/env python3
"""Independent Wall makers, source contracts and retained barrier/Wolf scopes."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/bone-wall-maker',
        extra_files={
            'bone-wall-maker-source-cases.json':ROOT/'.local/class-skills/bone-wall-maker-source/cases.json',
            'bone-barrier-cast-source-cases.json':ROOT/'.local/class-skills/bone-barrier-cast-source/cases.json',
            'bone-barrier-birth-source-cases.json':ROOT/'.local/class-skills/bone-barrier-birth-source/cases.json'},
        extra_families=('bone-wall-maker','bone-barrier-cast','bone-barrier-birth','spirit-wolf-casting','bone-barriers'),
        extra_wait_results={'bone-wall-maker-check':'bone-wall-maker-result','bone-wall-maker-reload-check':'bone-wall-maker-reload-result','bone-barriers-check':'bone-barriers-result'},
        extra_manifest_names={'bone-barriers':'bone-barrier-checks.json'},
        extra_target_input_commands=('bone-wall-target-right','bone-prison-self-quick','spirit-wolf-right'),
        extra_ground_input_commands=('spirit-wolf-right',),
        extra_input_timeouts={'bone-wall-target-right':30,'bone-prison-self-quick':30,'spirit-wolf-right':10},
        save_command='bone-wall-maker-save',save_result='bone-wall-maker-save',extra_inputs=(Path(__file__).resolve(),))
