#!/usr/bin/env python3
"""Live original demon Death Mark relocation,commands,control managers and saves."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/death-mark-world',extra_families=('goatman-body','pet-native-placement','death-mark-world','death-mark-pets','death-mark-cast'),
  extra_files={n+'-cases.json':ROOT/'.local/class-skills'/(n+'-source')/'cases.json'for n in ['death-mark-pets','death-mark-cast']}|{'pet-path-targets-source-probe.json':ROOT/'.local/class-skills/pet-path-targets-source-probe.json'}|{n:ROOT/'.local/class-skills'/n for n in ['goatman-formula-source-probe.json','goatman-creation-source-probe.json','goatman-tooltip-observation.json']},
  extra_checks=('pet-path-targets-check',),
  extra_connected_checks=('death-mark-world-check','death-mark-world-reload-check'),
  extra_wait_results={n:n+'-result'for n in ['death-mark-world-check','death-mark-world-reload-check']},
  extra_inputs=(Path(__file__).resolve(),ROOT/'tools/probe_death_mark_pets.py',ROOT/'tools/reference/death-mark-pets-current-evidence.json',ROOT/'tools/reference/death-mark-cast-current-evidence.json'))
