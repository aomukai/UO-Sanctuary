#!/usr/bin/env python3
"""Current event dispatch and actual original-root cancellation/reload cohort."""
import os
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-event-dispatch',extra_families=('pet-command-target',),extra_checks=('pet-event-dispatch-check',),
          extra_files={'pet-event-dispatch-cases.json':ROOT/'.local/class-skills/pet-event-dispatch-source/cases.json',
                       'pet-owner-iterator-cases.json':ROOT/'.local/class-skills/pet-owner-iterator-source/cases.json',
                       'pet-owner-types.json':ROOT/'tools/reference/d2r-named-equipment/json/pettype.json',
                       'pet-command-target-cases.json':ROOT/'.local/class-skills/pet-command-target-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_pet_event_dispatch.py',ROOT/'tools/reference/pet-event-dispatch-source-evidence.json'))
