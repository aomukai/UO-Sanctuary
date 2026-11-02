#!/usr/bin/env python3
"""Sacrifice ordered player contact with retained allocation and Poison Dagger consumers."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_sacrifice_allocation import configuration as allocation

def configuration(full=False):
 c=allocation(full);c['stage']=ROOT/'.local/class-skills/sacrifice-contact'
 c['extra_checks']+=('sacrifice-contact-check',)
 if not full:c['extra_checks']+=('poison-dagger-contact-check',)
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
