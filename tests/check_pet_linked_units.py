#!/usr/bin/env python3
"""Current linked lifecycle plus native ownership and original-demon reloads."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-linked-units',extra_checks=('pet-linked-units-check',),
          extra_families=('pet-command-target','pet-native-status'),
          extra_files={'pet-linked-units-cases.json':ROOT/'.local/class-skills/pet-linked-units-source/cases.json',
                       'pet-owner-iterator-cases.json':ROOT/'.local/class-skills/pet-owner-iterator-source/cases.json',
                       'pet-owner-types.json':ROOT/'tools/reference/d2r-named-equipment/json/pettype.json',
                       'pet-command-target-cases.json':ROOT/'.local/class-skills/pet-command-target-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_pet_linked_units.py',))
