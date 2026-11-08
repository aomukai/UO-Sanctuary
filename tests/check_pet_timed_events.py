#!/usr/bin/env python3
"""Current timed event store/remover bound into original demon root checks."""
import os
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-timed-events',extra_families=('pet-command-target',),extra_checks=('pet-timed-events-check',),
          extra_files={'pet-timed-events-cases.json':ROOT/'.local/class-skills/pet-timed-events-source/cases.json',
                       'pet-owner-iterator-cases.json':ROOT/'.local/class-skills/pet-owner-iterator-source/cases.json',
                       'pet-owner-types.json':ROOT/'tools/reference/d2r-named-equipment/json/pettype.json',
                       'pet-command-target-cases.json':ROOT/'.local/class-skills/pet-command-target-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_pet_timed_events.py',ROOT/'tools/reference/pet-timed-events-source-evidence.json'))
