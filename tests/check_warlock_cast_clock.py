#!/usr/bin/env python3
"""Warlock ordinary SC timing plus retained Necromancer and learned Shadow consumers."""
import os,json
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/warlock-cast-clock'
 c['extra_families']=('warlock-cast-clock','shadow-cast')
 evidence=json.loads((ROOT/'tools/reference/warlock-cast-clock-data-evidence.json').read_text())
 c['extra_files']['warlock-shared-sc-cases.json']=ROOT/evidence['sourceFixture']['path']
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/build_warlock_cast_clock.py',ROOT/'tools/reference/warlock-cast-clock-data-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
