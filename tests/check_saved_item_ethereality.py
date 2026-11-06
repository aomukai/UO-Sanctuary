#!/usr/bin/env python3
"""Saved forced ethereality through original pet equipment and native contact."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_saved_tree_property import configuration as prior
def configuration(retained=False):
    c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
    c['stage']=ROOT/'.local/class-skills'/('saved-item-ethereality-retained'if retained else'saved-item-ethereality')
    c['extra_families']+=('saved-item-ethereality',)
    c['extra_connected_checks']+=('saved-item-ethereality-check','saved-item-ethereality-reload-check')
    c['extra_files']['saved-item-ethereality-cases.json']=ROOT/'.local/class-skills/saved-item-ethereality-source/cases.json'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
