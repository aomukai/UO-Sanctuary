#!/usr/bin/env python3
"""Master AI106 world bridge with affected Warrior and original action regressions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-master-world'
 c['extra_families']=('shadow-master-world','shadow-ground','shadow-actions')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-master-ai-source-evidence.json',ROOT/'tools/reference/shadow-master-target-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
