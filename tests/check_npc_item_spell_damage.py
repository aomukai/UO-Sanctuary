#!/usr/bin/env python3
"""Native own-NPC proc spell capture with original contacts and saved items."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_contact import configuration as prior
def configuration(retained=False):
 c=prior(retained);c['stage']=ROOT/'.local/class-skills'/('npc-item-spell-damage-retained'if retained else'npc-item-spell-damage')
 c['extra_families']+=('npc-item-spell-damage',);c['extra_files']['npc-item-spell-damage-cases.json']=ROOT/'.local/class-skills/npc-item-spell-damage-source/cases.json';c['extra_inputs']+=(Path(__file__).resolve(),)
 # The numeric check observes the original saved contact bodies; it owns no
 # second roster or manifest and must not invent a duplicate save surface.
 c.setdefault('extra_manifest_names',{})['npc-item-spell-damage']='valkyrie-contact-fixtures.json'
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
