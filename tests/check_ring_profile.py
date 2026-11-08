#!/usr/bin/env python3
"""Focused Ring primary binding/free-flight plan and reused scaler regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/ring-profile',extra_families=('ring-profile','missile-damage-scale'),
  extra_files={'ring-profile-cases.json':ROOT/'.local/class-skills/ring-profile-source/cases.json',
               'missile-damage-scale-cases.json':ROOT/'.local/class-skills/missile-damage-scale-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_ring_profile.py',ROOT/'tools/reference/ring-profile-current-evidence.json'))
