#!/usr/bin/env python3
"""Approved intrinsic Fury action with retained own/player missile and Master world consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-fury-cast'
 c['extra_families']=('shadow-fury-cast','shadow-fury-clock','shadow-fury-packets','shadow-fury-missiles','shadow-master-world','blade-fury-missile')
 c['extra_manifest_names']={'blade-fury-missile':'blade-fury-missile-fixture.json'}
 c['extra_files'].update({'shadow-fury-clock-cases.json':ROOT/'.local/class-skills/shadow-fury-clock-source/cases.json','shadow-fury-action-cases.json':ROOT/'.local/class-skills/shadow-fury-action-source/cases.json','shadow-fury-packet-cases.json':ROOT/'.local/class-skills/shadow-fury-packets-source/cases.json','shadow-fury-missile-cases.json':ROOT/'.local/class-skills/shadow-fury-missiles-source/cases.json','blade-fury-damage-source-probe.json':ROOT/'.local/class-skills/blade-fury-damage-source-probe.json'})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-fury-action-source-evidence.json',ROOT/'tools/reference/shadow-fury-callback-source-evidence.json',ROOT/'docs/shadow-fury-action-proposal.md',ROOT/'docs/shadow-missile-critical-proposal.md')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
