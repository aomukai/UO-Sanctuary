#!/usr/bin/env python3
"""Current owner survey and existing follow/search bindings in native host."""
import os
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/pet-owner-survey',extra_families=('pet-owner-survey',),
          extra_files={'pet-owner-survey-cases.json':ROOT/'.local/class-skills/pet-owner-survey-source/cases.json'},
          # A survey is transient per-think scratch,not a persistent actor.
          # Reuse the retained resource manifest for the harness's copy step.
          extra_manifest_names={'pet-owner-survey':'npc-resource-callbacks-fixtures.json'},
          extra_inputs=(ROOT/'tests/check_pet_owner_survey.py',ROOT/'tools/reference/pet-owner-survey-source-evidence.json'))
