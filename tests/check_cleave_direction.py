#!/usr/bin/env python3
"""Current direction wrapper and retained Cleave sweep/source gear checks."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/cleave-direction',extra_families=('cleave-sweep','cleave-direction'),
          extra_files={'cleave-sweep-cases.json':ROOT/'.local/class-skills/cleave-sweep-source/cases.json','cleave-direction-cases.json':ROOT/'.local/class-skills/cleave-direction-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_cleave_direction.py',ROOT/'tools/reference/cleave-direction-current-evidence.json'))
