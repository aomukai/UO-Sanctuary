#!/usr/bin/env python3
"""Own Venom list/real NPC melee/poison delivery with retained consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-venom'
 c['extra_families']=('shadow-venom','shadow-buffs','shadow-ground','venom')
 c['extra_files']['venom-damage-source-probe.json']=ROOT/'.local/class-skills/venom-damage-source-probe.json'
 c['extra_wait_results']={'venom-check':'venom-result','venom-reload-check':'venom-reload-result'}
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/venom-buff-engine-evidence.json',ROOT/'tools/reference/venom-damage-engine-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
