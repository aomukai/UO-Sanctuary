#!/usr/bin/env python3
"""Current command lookup plus original controller/registry/survey composition."""
import os
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-command-target',extra_families=('pet-owner-survey','pet-command-target'),
          extra_manifest_names={'pet-owner-survey':'npc-resource-callbacks-fixtures.json'},
          extra_files={'pet-command-target-cases.json':ROOT/'.local/class-skills/pet-command-target-source/cases.json',
                       'pet-owner-survey-cases.json':ROOT/'.local/class-skills/pet-owner-survey-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_pet_command_target.py',ROOT/'tools/reference/pet-command-target-source-evidence.json'))
