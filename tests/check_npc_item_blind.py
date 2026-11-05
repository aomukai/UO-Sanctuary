#!/usr/bin/env python3
"""Original learned blind equipment,actual curse/AI and two real reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/npc-item-blind',extra_families=('npc-item-blind',),extra_connected_checks=('npc-item-blind-check','npc-item-blind-reload-check'),extra_inputs=(Path(__file__).resolve(),),extra_files={'npc-item-blind-cases.json':ROOT/'.local/class-skills/npc-item-blind-source/cases.json'})
