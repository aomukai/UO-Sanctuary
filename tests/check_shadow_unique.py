#!/usr/bin/env python3
"""Original Shadow unique identity and affected approved slow consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_slow import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-unique'
 c['extra_families']=('shadow-unique','npc-item-slow')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-death-modifier-source-review.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
