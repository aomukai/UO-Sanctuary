#!/usr/bin/env python3
"""Original pet RIP through shared kill,corpse and persistence services."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_slow import configuration as prior
def configuration(retained=False):
    c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
    c['stage']=ROOT/'.local/class-skills'/('npc-rest-in-peace-retained'if retained else'npc-rest-in-peace')
    c['extra_families']+=('npc-rest-in-peace',)
    c['extra_connected_checks']+=('npc-rest-in-peace-check','npc-rest-in-peace-reload-check')
    c['extra_files']['npc-rest-in-peace-cases.json']=ROOT/'.local/class-skills/npc-rest-in-peace-source/cases.json'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
