#!/usr/bin/env python3
"""Player-only NPC damage-to-mana guard and unmodified Howltusk with retained combat."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_knockback import configuration as previous

def configuration(retained=False):
 c=previous(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
 c['stage']=ROOT/'.local/class-skills'/('npc-damage-to-mana-retained'if retained else'npc-damage-to-mana')
 c['extra_families']+=('npc-damage-to-mana',)
 c['extra_connected_checks']+=('npc-damage-to-mana-check','npc-damage-to-mana-reload-check')
 c['extra_inputs']+=(Path(__file__).resolve(),)
 c['extra_files']['npc-damage-to-mana-cases.json']=ROOT/'.local/class-skills/npc-damage-to-mana-source/cases.json'
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();group=p.add_mutually_exclusive_group();group.add_argument('--retained',action='store_true');group.add_argument('--iron',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));c=configuration(a.retained)
 if a.iron:c=dict(stage=ROOT/'.local/class-skills/npc-damage-to-mana-iron',extra_families=('iron-golem-cast',),extra_inputs=(Path(__file__).resolve(),))
 check(**c)
