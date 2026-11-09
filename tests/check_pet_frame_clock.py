#!/usr/bin/env python3
"""Prepared live frame clock plus original-demon save/reload cohort."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-frame-clock',extra_families=('pet-command-target','pet-frame-clock'),
          extra_files={'pet-owner-iterator-cases.json':ROOT/'.local/class-skills/pet-owner-iterator-source/cases.json',
                       'pet-owner-types.json':ROOT/'tools/reference/d2r-named-equipment/json/pettype.json',
                       'pet-command-target-cases.json':ROOT/'.local/class-skills/pet-command-target-source/cases.json'},
          extra_wait_results={'pet-frame-clock-check':'pet-frame-clock-result','pet-frame-clock-reload-check':'pet-frame-clock-result'},
          extra_inputs=(ROOT/'tests/check_pet_frame_clock.py',))
