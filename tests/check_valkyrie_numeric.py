#!/usr/bin/env python3
"""Valkyrie full original numeric graph and live recipients,with saved controls."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_equipment import configuration as prior
def configuration(retained=False):
 c=prior(retained);c['stage']=ROOT/'.local/class-skills'/('valkyrie-numeric-retained'if retained else'valkyrie-numeric')
 c['extra_families']+=('valkyrie-numeric',)
 c['extra_files']['valkyrie-numeric-cases.json']=ROOT/'.local/class-skills/valkyrie-numeric-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
