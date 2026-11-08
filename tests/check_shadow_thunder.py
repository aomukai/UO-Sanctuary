#!/usr/bin/env python3
"""Own Thunder in shared missile pools and affected original/player consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-thunder'
 c['extra_files'].update({'shadow-charge-animation-cases.json':ROOT/'.local/class-skills/shadow-charge-animation-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json','missile-acceleration-source-probe.json':ROOT/'.local/class-skills/missile-acceleration-source-probe.json','martial-lightning-source-probe.json':ROOT/'.local/class-skills/martial-lightning-source-probe.json','thunder-damage-source-probe.json':ROOT/'.local/class-skills/thunder-damage-source-probe.json'})
 c['extra_families']=('shadow-thunder','shadow-fists','shadow-finisher','shadow-phoenix','thunder-release')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/review_shadow_thunder.py',ROOT/'tools/reference/shadow-thunder-source-review.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
