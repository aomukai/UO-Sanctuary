#!/usr/bin/env python3
"""Shared remaining-aura save safety,retaining dedicated Holy Freeze and all aura consumers."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_offensive_aura import configuration as parent
def configuration(full=False):
    c=parent(full);c['stage']=ROOT/'.local/class-skills/paladin-aura-persistence'
    c['extra_checks']+=('paladin-aura-persistence-check',)
    assert 'holy-freeze' not in c['extra_families'];c['extra_families']+=('holy-freeze',)
    c['extra_manifest_names']['holy-freeze']='holy-freeze-checks.json'
    c['extra_wait_results'].update({'holy-freeze-check':'holy-freeze-result','holy-freeze-reload-check':'holy-freeze-reload-result'})
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/holy-freeze-engine-evidence.json')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
