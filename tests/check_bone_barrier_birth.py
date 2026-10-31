#!/usr/bin/env python3
"""Exact barrier birth/resources plus shared recovery and existing collision controls."""
import sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    smoke='--smoke' in sys.argv
    check(stage=ROOT/'.local/class-skills/bone-barrier-birth',
        extra_files={
            'bone-barrier-birth-source-cases.json':ROOT/'.local/class-skills/bone-barrier-birth-source/cases.json',
            'npc-equipment-regeneration-source-cases.json':ROOT/'.local/class-skills/npc-equipment-regeneration-source-cases.json'},
        extra_families=('bone-barrier-birth','npc-equipment-regeneration') if smoke else ('bone-barrier-birth','bone-barriers','npc-equipment-regeneration'),
        extra_wait_results={} if smoke else {'bone-barriers-check':'bone-barriers-result'},
        extra_manifest_names={'bone-barriers':'bone-barrier-checks.json'},
        extra_target_input_commands=() if smoke else ('bone-wall-birth-right','bone-prison-birth-quick'),
        extra_input_timeouts={'bone-wall-birth-right':30,'bone-prison-birth-quick':30},
        save_command='bone-barrier-birth-save',save_result='bone-barrier-birth-save',
        extra_inputs=(Path(__file__).resolve(),))
