#!/usr/bin/env python3
"""Failed Enchant release payment with retained Energy Shield/class contracts."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_energy_shield import configuration as energy

def configuration(full=False):
    c=energy(full);c['stage']=ROOT/'.local/class-skills/sorceress-buff-payment'
    c['extra_families']+=('buff-payment',)
    c['extra_target_input_commands']+=tuple('buff-payment-'+target+'-'+mode for target in ('self','ally','hostile') for mode in ('right','quick'))
    c['extra_files']['buff-payment-source-cases.json']=ROOT/'.local/class-skills/buff-payment-source/cases.json'
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/buff-payment-source-evidence.json')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
