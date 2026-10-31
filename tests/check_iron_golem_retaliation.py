#!/usr/bin/env python3
"""Run prepared Iron Golem flat-return plus retained world/body/source scopes."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/iron-golem-retaliation',
        extra_files={
            'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
            'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
            'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json',
            'iron-golem-retaliation-source-probe.json':ROOT/'.local/class-skills/iron-golem-retaliation-source-probe.json'},
        extra_families=('iron-golem-birth-graph','iron-golem-body','iron-golem-world','iron-golem-retaliation'),
        extra_checks=('necromancer-pet-ai-check','iron-golem-retaliation-expiry'),extra_live_commands=('iron-golem-world-live',),extra_inputs=(Path(__file__).resolve(),))
