#!/usr/bin/env python3
"""Approved new demon startup and unchanged legacy prepared-startup cohort."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-birth-startup',extra_families=('pet-birth-startup','pet-startup-proposal'),
          extra_inputs=(ROOT/'tests/check_pet_birth_startup.py',ROOT/'tools/reference/pet-startup-approval.json'))
