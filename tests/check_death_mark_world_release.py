#!/usr/bin/env python3
"""Actual captured Death Mark transaction,recipient and original demons."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/death-mark-world-release',extra_families=('death-mark-debuff','death-mark-cast','death-mark-world-release'),
  extra_files={'death-mark-cast-cases.json':ROOT/'.local/class-skills/death-mark-cast-source/cases.json'},
  extra_connected_checks=('death-mark-world-release-check','death-mark-world-release-reload-check'),
  extra_wait_results={n:n+'-result'for n in ['death-mark-world-release-check','death-mark-world-release-reload-check']},
  extra_inputs=(ROOT/'tests/check_death_mark_world_release.py',ROOT/'tools/reference/death-mark-recipient-approval.json'))
