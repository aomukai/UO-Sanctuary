#!/usr/bin/env python3
"""Retain changed learned melee consumers and their original input routes."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_vengeance_contact import configuration as paladin
from check_dragon_tail import configuration as tail
def configuration(dragon=False):
 c=tail(False)if dragon else paladin(False);c['stage']=ROOT/'.local/class-skills'/('valkyrie-defense-tail'if dragon else'valkyrie-defense-combat');c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--tail',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.tail))
