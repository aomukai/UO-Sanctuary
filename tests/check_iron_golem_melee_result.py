#!/usr/bin/env python3
"""Verify Iron Golem melee result flags with retained original/body/world/retaliation consumers."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/iron-golem-melee-result',extra_files={
        'melee-result-source-probe.json':ROOT/'.local/class-skills/melee-result-source-probe.json',
        'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
        'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
        'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json',
        'iron-golem-retaliation-source-probe.json':ROOT/'.local/class-skills/iron-golem-retaliation-source-probe.json'},
        extra_families=('iron-golem-birth-graph','iron-golem-body','iron-golem-world','iron-golem-retaliation','golem-roster'),
        extra_checks=('melee-result-check','necromancer-pet-ai-check','iron-golem-retaliation-expiry'),extra_live_commands=('iron-golem-world-live',),extra_inputs=(Path(__file__).resolve(),))
