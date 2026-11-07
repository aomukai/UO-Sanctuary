#!/usr/bin/env python3
"""Intrinsic Shadow Cloak and shared posted-buff/player-curse consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-cloak'
 c['extra_files']['cloak-source-probe.json']=ROOT/'.local/class-skills/cloak-source-probe.json'
 c['extra_families']=('shadow-cloak','shadow-buffs','shadow-venom','shadow-item-frame','cloak')
 c['extra_wait_results']={'cloak-check':'cloak-result','cloak-reload-check':'cloak-reload-result'}
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/cloak-engine-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
