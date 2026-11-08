#!/usr/bin/env python3
"""Current life percent and real original health-to-initializer/scoring binding."""
import os
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-life-percent',extra_families=('pet-command-target','pet-life-percent'),
          extra_files={'pet-life-percent-cases.json':ROOT/'.local/class-skills/pet-life-percent-source/cases.json',
                       'pet-command-target-cases.json':ROOT/'.local/class-skills/pet-command-target-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_pet_life_percent.py',ROOT/'tools/reference/pet-life-percent-source-evidence.json'))
