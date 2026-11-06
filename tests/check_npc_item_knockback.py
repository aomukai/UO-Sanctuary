#!/usr/bin/env python3
"""Original NPC knockback with retained item/curse/contact acceptance."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_fear import configuration as previous

def configuration(retained=False):
 c=previous(retained) if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
 c['stage']=ROOT/'.local/class-skills'/('npc-item-knockback-retained'if retained else'npc-item-knockback')
 c['extra_families']+=('npc-item-knockback',)
 c['extra_connected_checks']+=('npc-item-knockback-check','npc-item-knockback-reload-check')
 c['extra_inputs']+=(Path(__file__).resolve(),)
 c['extra_files']['npc-item-knockback-cases.json']=ROOT/'.local/class-skills/npc-item-knockback-source/cases.json'
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
