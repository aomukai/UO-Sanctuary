#!/usr/bin/env python3
"""Focused candidate packet scaling and retained Ring launch; no deployment."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/missile-damage-scale'
 c['extra_families']=('missile-damage-scale','ring-of-fire-launch')
 c['extra_files']['missile-damage-scale-cases.json']=ROOT/'.local/class-skills/missile-damage-scale-source/cases.json'
 c['extra_files']['ring-launch-current-cases.json']=ROOT/'.local/class-skills/ring-of-fire-launch-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/missile-damage-scale-current-evidence.json',ROOT/'tools/probe_missile_damage_scale.py')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
