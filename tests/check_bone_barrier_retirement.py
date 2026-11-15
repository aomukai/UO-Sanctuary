#!/usr/bin/env python3
"""Prepared barrier death composition plus retained birth/cast/maker mechanics."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/bone-barrier-retirement',
        extra_families=('bone-barrier-retirement','bone-barrier-cast','bone-barrier-birth','bone-wall-maker'),
        extra_files={f'bone-{name}-source-cases.json':ROOT/f'.local/class-skills/bone-{name}-source/cases.json'
            for name in ('barrier-cast','barrier-birth','wall-maker')},
        extra_wait_results={'bone-wall-maker-check':'bone-wall-maker-result','bone-wall-maker-reload-check':'bone-wall-maker-reload-result'},
        save_command='bone-barrier-retirement-save',save_result='bone-barrier-retirement-save',
        extra_target_input_commands=('barrier-retirement-prison-right','barrier-retirement-wall-quick'),
        extra_input_timeouts={'barrier-retirement-prison-right':10,'barrier-retirement-wall-quick':35},
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bone-barrier-retirement-source-evidence.json',
            ROOT/'tools/reference/bone-barrier-hidedead-reference-evidence.json'))
