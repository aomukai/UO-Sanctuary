#!/usr/bin/env python3
"""Own travelling Fury plus retained player flight, own packet and melee receiver."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-fury-missiles'
 c['extra_families']=('shadow-fury-missiles','shadow-fury-packets','blade-fury-missile','npc-iron-maiden')
 c['extra_manifest_names']={'blade-fury-missile':'blade-fury-missile-fixture.json'}
 c['extra_files'].update({'shadow-fury-missile-cases.json':ROOT/'.local/class-skills/shadow-fury-missiles-source/cases.json','shadow-fury-packet-cases.json':ROOT/'.local/class-skills/shadow-fury-packets-source/cases.json','blade-fury-damage-source-probe.json':ROOT/'.local/class-skills/blade-fury-damage-source-probe.json'})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-fury-missiles-source-evidence.json',ROOT/'tools/probe_shadow_fury_missiles.py',ROOT/'tools/reference/shadow-fury-packets-source-evidence.json',ROOT/'tools/reference/assassin-trap-attack-engine-evidence.json',ROOT/'tools/reference/weapon-missile-impact-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
