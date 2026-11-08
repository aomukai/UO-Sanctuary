#!/usr/bin/env python3
"""Current initializer/root composition plus original command/body reloads."""
import os
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-initialization',extra_families=('pet-command-target','pet-initialization'),
          extra_manifest_names={'pet-initialization':'pet-command-target-fixtures.json'},
          extra_files={'pet-initialization-cases.json':ROOT/'.local/class-skills/pet-initialization-source/cases.json',
                       'pet-command-target-cases.json':ROOT/'.local/class-skills/pet-command-target-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_pet_initialization.py',ROOT/'tools/reference/pet-initialization-source-evidence.json'))
