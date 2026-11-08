#!/usr/bin/env python3
"""Master's complete target/tactic source policy and actual own action boundary."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-master-targets';c['extra_families']=()
 c['extra_checks']=('shadow-master-target-check',)
 c['extra_files']['shadow-master-target-cases.json']=ROOT/'.local/class-skills/shadow-master-target-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-master-target-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
