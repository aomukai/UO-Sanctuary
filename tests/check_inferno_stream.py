#!/usr/bin/env python3
"""Prepared Inferno host and approved Warlock bodies with retained Sorceress inputs."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_sorceress_nova import configuration as retained
if __name__=='__main__':
    c=retained();c['stage']=ROOT/'.local/class-skills/inferno-stream'
    c['extra_families']+=('inferno-stream','inferno-primitives','goatman-body')
    c['extra_wait_results'].update({'inferno-primitives-check':'inferno-primitives-result','inferno-primitives-reload-check':'inferno-primitives-reload-result'})
    c['extra_manifest_names']={'inferno-primitives':'inferno-primitives.json'}
    c['extra_checks']+=('player-inferno-check','player-inferno-sequence-check','arctic-mode-rate-check')
    for name in ('inferno-stream-damage','inferno-stream-factory','player-inferno-channel','player-inferno-sequence','player-inferno-rate'):
        c['extra_files'][name+'-source-probe.json']=ROOT/'.local/class-skills'/(name+'-source-probe.json')
    c['extra_inputs']+=(Path(__file__).resolve(),)
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
