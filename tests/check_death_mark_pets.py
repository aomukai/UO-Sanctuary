#!/usr/bin/env python3
"""Current Death Mark pet callback,shared relocation and cast composition."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/death-mark-pets',extra_families=('death-mark-pets','death-mark-cast'),
  extra_files={n+'-cases.json':ROOT/'.local/class-skills'/(n+'-source')/'cases.json'for n in ['death-mark-pets','death-mark-cast']}|{'pet-path-targets-source-probe.json':ROOT/'.local/class-skills/pet-path-targets-source-probe.json'},
  extra_checks=('pet-path-targets-check',),
  extra_inputs=(Path(__file__).resolve(),ROOT/'tools/probe_death_mark_pets.py',ROOT/'tools/reference/death-mark-pets-current-evidence.json',ROOT/'tools/reference/death-mark-cast-current-evidence.json'))
