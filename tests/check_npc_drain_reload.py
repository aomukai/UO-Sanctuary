#!/usr/bin/env python3
"""Reproduce the longer shared drain/native-Revive reload cohort with diagnostics."""
from pathlib import Path
from check_npc_freeze_hit_retained import settings
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    c=settings();c['stage']=ROOT/'.local/class-skills/npc-drain-reload'
    names=list(c['extra_families']);names.insert(names.index('revive-event'),'npc-flat-drains');c['extra_families']=tuple(names)
    c['extra_files']['npc-flat-drains-cases.json']=ROOT/'.local/class-skills/npc-flat-drains-source/cases.json'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_checks']+=('npc-flat-drains-timer-check',)
    c['extra_wait_results']['npc-flat-drains-timer-check']='npc-flat-drains-timer-result'
    check(**c)
