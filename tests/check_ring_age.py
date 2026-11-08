#!/usr/bin/env python3
"""Approved Ring elapsed-age packet/HP/save composition; no old scaler cohort rerun."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/ring-age',extra_families=('ring-profile',),
  extra_files={'ring-profile-cases.json':ROOT/'.local/class-skills/ring-profile-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_ring_age.py',ROOT/'tools/reference/ring-profile-current-evidence.json',ROOT/'tools/reference/ring-age-approval.json'))
