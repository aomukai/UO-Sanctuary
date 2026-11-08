#!/usr/bin/env python3
"""Own NPC charge backend with retained player lifecycle and original melee/action consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-charge-actions'
 c['extra_files'].update({'shadow-charge-animation-cases.json':ROOT/'.local/class-skills/shadow-charge-animation-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json'})
 c['extra_families']=('shadow-charge','shadow-ground','shadow-actions','tiger-strike')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-charge-animation-source-evidence.json',ROOT/'tools/reference/tiger-strike-source-evidence.json',ROOT/'tools/reference/cobra-strike-source-evidence.json',ROOT/'tools/reference/phoenix-builder-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
