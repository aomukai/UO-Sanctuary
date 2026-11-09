#!/usr/bin/env python3
"""Learned approved Cleave with real left/right/quick packets and action regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/cleave-cast',extra_families=('cleave-sweep','cleave-direction','cleave-action','cleave-cast'),
          extra_files={'cleave-sweep-cases.json':ROOT/'.local/class-skills/cleave-sweep-source/cases.json','cleave-direction-cases.json':ROOT/'.local/class-skills/cleave-direction-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_cleave_cast.py',ROOT/'tools/reference/cleave-action-approval.json'),
          extra_target_input_commands=('cleave-cast-left','cleave-cast-right','cleave-cast-quick'),extra_ground_input_commands=('cleave-cast-right',),
          extra_pre_save_commands=('cleave-cast-presave-check',))
