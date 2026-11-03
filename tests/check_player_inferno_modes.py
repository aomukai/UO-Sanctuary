#!/usr/bin/env python3
"""Shared supplied-intent fire/cold controllers and retained spell/save/input scopes."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_inferno_stream import configuration as retained
def configuration():
    c=retained();c['stage']=ROOT/'.local/class-skills/player-inferno-modes'
    c['extra_families']+=('inferno-mode','arctic-mode')
    c['extra_live_commands']+=('inferno-mode-live','arctic-mode-live')
    c['checked_live_commands']=('inferno-mode-live','arctic-mode-live')
    c['extra_manifest_names'].update({'inferno-mode':'inferno-mode-fixture.json','arctic-mode':'arctic-mode-fixture.json'})
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
