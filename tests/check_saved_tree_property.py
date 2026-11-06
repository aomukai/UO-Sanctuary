#!/usr/bin/env python3
"""Saved approved tree encoding through original pet items and native contact."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_rest_in_peace import configuration as prior
def configuration(retained=False):
    c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
    c['stage']=ROOT/'.local/class-skills'/('saved-tree-property-retained'if retained else'saved-tree-property')
    c['extra_families']+=('saved-tree-property',)
    c['extra_connected_checks']+=('saved-tree-property-check','saved-tree-property-reload-check')
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
