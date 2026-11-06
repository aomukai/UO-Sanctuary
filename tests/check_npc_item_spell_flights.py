#!/usr/bin/env python3
"""Original NPC item spell emission and actual saved projectile regression."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_spell_damage import configuration as prior
def configuration(retained=False):
 c=prior(retained);c['stage']=ROOT/'.local/class-skills'/('npc-item-spell-flights-retained'if retained else'npc-item-spell-flights')
 c['extra_families']+=('npc-item-spell-flights',);c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
