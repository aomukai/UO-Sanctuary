#!/usr/bin/env python3
"""Full ordinary drain allocation and actual ordered HP/resource/stun receiver."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/npc-flat-drains',extra_families=('npc-flat-drains',),extra_inputs=(Path(__file__).resolve(),),extra_files={'npc-flat-drains-cases.json':ROOT/'.local/class-skills/npc-flat-drains-source/cases.json'})
