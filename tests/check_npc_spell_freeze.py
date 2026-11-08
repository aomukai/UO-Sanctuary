#!/usr/bin/env python3
"""Owned spell-freeze receiver with affected original NPC cold and Shadow consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/npc-spell-freeze'
 c['extra_families']=('npc-spell-freeze','shadow-charge','shadow-master-world')
 c['extra_files']['shadow-charge-animation-cases.json']=ROOT/'.local/class-skills/shadow-charge-animation-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/npc-freeze-hit-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
