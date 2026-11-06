#!/usr/bin/env python3
"""Original NPC life/demon life recovery and player-only mana with retained item scopes."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_damage_to_mana import configuration as previous

def configuration(retained=False):
 c=previous(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
 c['stage']=ROOT/'.local/class-skills'/('npc-kill-recovery-retained'if retained else'npc-kill-recovery')
 c['extra_families']+=('npc-kill-recovery',)
 c['extra_connected_checks']+=('npc-kill-recovery-check','npc-kill-recovery-reload-check')
 c['extra_inputs']+=(Path(__file__).resolve(),)
 c['extra_files']['npc-kill-recovery-cases.json']=ROOT/'.local/class-skills/npc-kill-recovery-source/cases.json'
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group();g.add_argument('--retained',action='store_true');g.add_argument('--iron',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));c=configuration(a.retained)
 if a.iron:c=dict(stage=ROOT/'.local/class-skills/npc-kill-recovery-iron',extra_families=('iron-golem-cast',),extra_inputs=(Path(__file__).resolve(),))
 check(**c)
