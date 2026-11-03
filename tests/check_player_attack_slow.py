#!/usr/bin/env python3
"""Player combined slowing with the retained Assassin sentry/Fury cohorts."""
import os
from pathlib import Path
from check_blade_fury_reentry import configuration as previous
from check_iron_golem_birth import ROOT,check

def configuration():
    c=previous();c['stage']=ROOT/'.local/class-skills/player-attack-slow'
    c['extra_files']['player-attack-slow-probe.json']=ROOT/'.local/class-skills/player-attack-slow-probe.json'
    c['extra_files']['weapon-sequence-rate-probe.json']=ROOT/'.local/class-skills/weapon-sequence-rate-probe.json'
    c['extra_checks']=(*c['extra_checks'],'player-attack-slow-check','weapon-sequence-rate-check')
    c['extra_inputs']=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/player-attack-slow-source-evidence.json')
    return c

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
