#!/usr/bin/env python3
"""Retain the changed shared weapon/fill services on their original corpus."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_damage_fill import configuration as prior
def configuration():
 c=prior(False,True);c['stage']=ROOT/'.local/class-skills/valkyrie-damage-shared';c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
