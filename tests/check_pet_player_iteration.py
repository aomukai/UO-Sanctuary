#!/usr/bin/env python3
"""Current player broadcast enumeration/registry layout and original reloads."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-player-iteration',extra_checks=('pet-player-iteration-check',),
          extra_families=('pet-command-target','pet-native-status'),
          extra_files={'pet-player-iteration-cases.json':ROOT/'.local/class-skills/pet-player-iteration-source/cases.json',
                       'pet-owner-iterator-cases.json':ROOT/'.local/class-skills/pet-owner-iterator-source/cases.json',
                       'pet-owner-types.json':ROOT/'tools/reference/d2r-named-equipment/json/pettype.json',
                       'pet-command-target-cases.json':ROOT/'.local/class-skills/pet-command-target-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_pet_player_iteration.py',))
