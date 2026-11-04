#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_revive_equipped.py');config['stage']=ROOT/'.local/class-skills/npc-prevent-heal'
    config['extra_families']=(*config['extra_families'],'npc-prevent-heal')
    config.setdefault('extra_files',{}).update({f'npc-prevent-heal-{name}-cases.json':ROOT/f'.local/class-skills/npc-prevent-heal-source/{name}-cases.json' for name in ('classic','ubers')})
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_revive_equipped.py')
    check(**config)
