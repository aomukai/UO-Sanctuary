#!/usr/bin/env python3
"""Safe-facet/legacy parking and actual wardrobe/Revive lifecycle regression."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    base=configuration('check_safe_area_minions.py')
    base['stage']=ROOT/'.local/class-skills/parking-boundaries'
    base['extra_families']=('parking-boundary','full-wardrobe','revive-facet',*base['extra_families'])
    base['extra_live_commands']=('parking-boundary-live',*base['extra_live_commands'])
    base['extra_manifest_names'].update({'full-wardrobe':'full-wardrobe-checks.json'})
    base['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tests/check_safe_area_minions.py')
    check(**base)
