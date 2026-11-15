#!/usr/bin/env python3
"""Approved normal learned barriers with retained collision,clock and legacy saves."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/bone-barrier-world-cast',
        extra_families=('bone-barrier-retirement','bone-barrier-cast','bone-barrier-birth','bone-wall-maker','bone-barrier-clock','bone-barriers'),
        extra_files={f'bone-{name}-source-cases.json':ROOT/f'.local/class-skills/bone-{name}-source/cases.json'
            for name in ('barrier-cast','barrier-birth','wall-maker','barrier-retirement')},
        extra_wait_results={'bone-wall-maker-check':'bone-wall-maker-result','bone-wall-maker-reload-check':'bone-wall-maker-reload-result',
            'bone-barrier-clock-check':'bone-barrier-clock-result','bone-barrier-clock-reload-check':'bone-barrier-clock-reload-result','bone-barriers-check':'bone-barriers-result'},
        extra_manifest_names={'bone-barriers':'bone-barrier-checks.json'},
        save_command='bone-barrier-retirement-save',save_result='bone-barrier-retirement-save',
        extra_target_input_commands=('barrier-retirement-prison-right','barrier-retirement-wall-quick'),
        extra_input_timeouts={'barrier-retirement-prison-right':10,'barrier-retirement-wall-quick':35},
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bone-barrier-world-rule-approval.json'))
