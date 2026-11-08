#!/usr/bin/env python3
"""Prepared Sigil original world markers/UTC/roster/saves plus source controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/sigil-actor'
 c['extra_families']=('sigil-actor','sigil-tracking','sigil-launch')
 c['extra_files']['sigil-tracking-cases.json']=ROOT/'.local/class-skills/sigil-tracking-source/cases.json'
 c['extra_files']['sigil-launch-cases.json']=ROOT/'.local/class-skills/sigil-launch-source/cases.json'
 commands=('sigil-actor-check','sigil-actor-reload-check');c['extra_connected_checks']=commands
 c['extra_wait_results'].update({name:name+'-result'for name in commands})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/sigil-tracking-current-evidence.json',ROOT/'tools/reference/sigil-launch-current-evidence.json',ROOT/'tools/build_sigil_world_data.py')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
