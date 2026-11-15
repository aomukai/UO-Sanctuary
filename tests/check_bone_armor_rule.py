#!/usr/bin/env python3
"""Current Bone Armor breaking-hit/order policy and retained shield producers."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/bone-armor-rule',
        extra_files={'bone-armor-source-cases.json':ROOT/'.local/class-skills/bone-armor-source/cases.json',
            'cyclone-armor-source-probe.json':ROOT/'.local/class-skills/cyclone-armor-source-probe.json'},
        extra_families=('bone-armor','psychic-ward','cyclone-armor','bone-armor-rule'),
        extra_checks=('encounter-shields-check',),
        extra_target_input_commands=('bone-armor-right','bone-armor-quick'),
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bone-armor-source-evidence.json',ROOT/'tools/reference/bone-armor-current-followup.json'))
