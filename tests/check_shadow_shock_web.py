#!/usr/bin/env python3
"""Original Shadow fields plus retained player Shock Web source/native checks."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-shock-web'
 c['extra_files'].update({name:ROOT/'.local/class-skills'/name for name in ('shock-web-source-probe.json','shock-web-damage-source-probe.json','shock-web-field-source-probe.json')})
 c['extra_families']=('shadow-shock-web','shock-web')
 c['extra_wait_results']={'shock-web-check':'shock-web-result','shock-web-reload-check':'shock-web-reload-result'}
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shock-web-engine-evidence.json',ROOT/'tools/reference/shock-web-damage-engine-evidence.json',ROOT/'tools/reference/shock-web-field-engine-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
