#!/usr/bin/env python3
"""Current Ring launch plus retained Warlock ordinary timing and Shadow casts."""
import os,json
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/ring-of-fire-launch'
 c['extra_families']=('ring-of-fire-launch','warlock-cast-clock')
 c['extra_files']['ring-launch-current-cases.json']=ROOT/'.local/class-skills/ring-of-fire-launch-source/cases.json'
 prior=json.loads((ROOT/'tools/reference/warlock-cast-clock-data-evidence.json').read_text());c['extra_files']['warlock-shared-sc-cases.json']=ROOT/prior['sourceFixture']['path']
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/ring-of-fire-launch-current-evidence.json',ROOT/'tools/probe_ring_of_fire_launch.py')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
