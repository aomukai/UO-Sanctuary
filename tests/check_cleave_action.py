#!/usr/bin/env python3
"""Complete prepared Cleave candidate with a connected natural action per boot."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/cleave-action-proposal',extra_families=('cleave-sweep','cleave-direction','cleave-action'),
          extra_files={'cleave-sweep-cases.json':ROOT/'.local/class-skills/cleave-sweep-source/cases.json','cleave-direction-cases.json':ROOT/'.local/class-skills/cleave-direction-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_cleave_action.py',),extra_live_commands=('cleave-action-live',))
