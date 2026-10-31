#!/usr/bin/env python3
"""Barrier targets/allocation/payment and shared placement regression in disposable worlds."""
import sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    smoke='--smoke' in sys.argv
    check(stage=ROOT/'.local/class-skills/bone-barrier-cast',
        extra_files={'bone-barrier-cast-source-cases.json':ROOT/'.local/class-skills/bone-barrier-cast-source/cases.json','bone-barrier-birth-source-cases.json':ROOT/'.local/class-skills/bone-barrier-birth-source/cases.json'},
        extra_families=('bone-barrier-cast','bone-barrier-birth','spirit-wolf-casting') if smoke else ('bone-barrier-cast','bone-barrier-birth','spirit-wolf-casting','bone-barriers'),
        extra_wait_results={} if smoke else {'bone-barriers-check':'bone-barriers-result'},
        extra_manifest_names={'bone-barriers':'bone-barrier-checks.json'},
        extra_target_input_commands=() if smoke else ('bone-wall-target-right','bone-prison-self-quick','spirit-wolf-right'),
        extra_ground_input_commands=() if smoke else ('spirit-wolf-right',),
        extra_input_timeouts={'bone-wall-target-right':30,'bone-prison-self-quick':30,'spirit-wolf-right':10},
        save_command='bone-barrier-birth-save',save_result='bone-barrier-birth-save',extra_inputs=(Path(__file__).resolve(),))
