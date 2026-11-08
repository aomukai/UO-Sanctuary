#!/usr/bin/env python3
"""Shared Sigil families,old-save migration and retained learned Lethargy input."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/sigil-family'
 c['extra_families']=('sigil-family','sigil-cast','sigil-lethargy','sigil-actor','sigil-tracking','sigil-launch')
 for name in ['sigil-lethargy','sigil-tracking','sigil-launch']:c['extra_files'][name+'-cases.json']=ROOT/'.local/class-skills'/(name+'-source')/'cases.json'
 commands=tuple(family+suffix for family in ['sigil-family','sigil-cast','sigil-lethargy','sigil-actor']for suffix in ['-check','-reload-check']);c['extra_connected_checks']=commands
 c['extra_wait_results'].update({name:name+'-result'for name in commands});c['extra_connection_settle']=0.05
 c['extra_target_input_commands']=('sigil-cast-left','sigil-cast-right','sigil-cast-quick')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/sigil-lethargy-source-evidence.json',ROOT/'tools/probe_sigil_lethargy.py')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
