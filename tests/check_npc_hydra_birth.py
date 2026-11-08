#!/usr/bin/env python3
"""Original NPC Hydra prepared capture/start/item frame plus prior item-spell host."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/npc-hydra-birth'
 c['extra_families']=('npc-hydra-birth','npc-item-spell-flights')
 c['extra_files']['hydra-creation-cases.json']=ROOT/'.local/class-skills/hydra-creation-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/hydra-creation-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
