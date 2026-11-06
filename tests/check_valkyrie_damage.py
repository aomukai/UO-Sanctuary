#!/usr/bin/env python3
"""Original Valkyrie damage packets with retained hand/resource consumers."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_weapons import configuration as prior
def configuration(retained=False):
 c=prior(retained);c['stage']=ROOT/'.local/class-skills'/('valkyrie-damage-retained'if retained else'valkyrie-damage')
 c['extra_families']+=('valkyrie-damage',)
 c['extra_files'].update({'npc-innate-skill-rank-cases.json':ROOT/'.local/class-skills/npc-innate-skill-rank-source/cases.json','valkyrie-damage-cases.json':ROOT/'.local/class-skills/valkyrie-damage-source/cases.json'})
 c['extra_check_timeouts'].update({'valkyrie-damage-check':240,'valkyrie-damage-reload-check':180})
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
