#!/usr/bin/env python3
"""Own Ice/mixed progressive order plus affected original/player consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-ice'
 c['extra_files'].update({'shadow-charge-animation-cases.json':ROOT/'.local/class-skills/shadow-charge-animation-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json','ice-damage-source-probe.json':ROOT/'.local/class-skills/ice-damage-source-probe.json','martial-cold-source-probe.json':ROOT/'.local/class-skills/martial-cold-source-probe.json'})
 c['extra_families']=('shadow-ice','shadow-charge','shadow-finisher','shadow-fists','shadow-thunder','ice-release')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/review_shadow_ice.py',ROOT/'tools/reference/shadow-ice-source-review.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
