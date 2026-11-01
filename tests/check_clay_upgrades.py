#!/usr/bin/env python3
"""Approved Clay damage/Stone Skin/Magic Resistant with warrior controls with retained Chain/Static/buff/player/pet controls."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/clay-upgrades',extra_files={'iron-golem-retaliation-source-probe.json':ROOT/'.local/class-skills/iron-golem-retaliation-source-probe.json','mage-meteor-source-probe.json':ROOT/'.local/class-skills/mage-meteor-source-probe.json','freeze-state-source-probe.json':ROOT/'.local/class-skills/freeze-state-source-probe.json',
        'mage-birth-source-cases.json':ROOT/'.local/class-skills/mage-birth-source/cases.json',
        'mage-missile-source-cases.json':ROOT/'.local/class-skills/mage-missiles-source/cases.json',
        **{name+'-source-probe.json':ROOT/'.local/class-skills'/(name+'-source-probe.json') for name in ('sentry-target','sentry-unit-path','charged-bolt-path')},
        'fire-golem-birth-source-cases.json':ROOT/'.local/class-skills/fire-golem-birth-source/cases.json',
        'fire-golem-absorption-source-cases.json':ROOT/'.local/class-skills/fire-golem-absorption-source/cases.json',
        'blood-golem-birth-independent-source-cases.json':ROOT/'.local/class-skills/blood-golem-birth-independent-source/cases.json',
        'blood-golem-healing-source-cases.json':ROOT/'.local/class-skills/blood-golem-healing-source/healing-cases.json',
        'blood-golem-incoming-source-cases.json':ROOT/'.local/class-skills/blood-golem-healing-source/incoming-cases.json',
        'clay-golem-birth-source-cases.json':ROOT/'.local/class-skills/clay-golem-birth-source/cases.json',
        'clay-golem-slow-source-cases.json':ROOT/'.local/class-skills/clay-golem-slow-source/cases.json',
        'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
        'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
        'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json',
        'skeleton-birth-source-cases.json':ROOT/'.local/class-skills/skeleton-birth-source/cases.json',
        'skeleton-roster-source-cases.json':ROOT/'.local/class-skills/skeleton-roster-source/cases.json'},
        extra_families=('clay-upgrades','skeleton-upgrades','iron-golem-retaliation','paladin-thorns','mage-historical','mage-elements','mage-meteor','fire-ground','mage-glacial','mage-chain','chain-lightning','mage-static','mage-pet-buff','sorceress-skills','enchant','mage-world','mage-birth','mage-missile','sentry-unit-path','fire-golem-world','fire-golem-birth','fire-golem-combat','fire-golem-absorption','fire-golem-aura','fire-golem-death','blood-golem-world','blood-golem-birth','blood-golem-combat','blood-golem-contact','clay-golem-world','clay-golem-contact','clay-golem-combat','clay-golem-slow','clay-golem-birth','golem-roster','iron-golem-birth-graph','iron-golem-body','iron-golem-world','skeleton-contact','skeleton-world','skeleton-birth','skeleton-roster','skeleton-combat','summon-difficulty'),
        extra_checks=('freeze-state-check','sentry-target-check','necromancer-mage-targets-check','necromancer-pet-ai-check','necromancer-pet-targets-check'),
        extra_live_commands=('clay-upgrades-live','skeleton-upgrades-live','mage-historical-live','mage-elements-live','mage-meteor-live','mage-glacial-live','glacial-player-live','mage-chain-live','mage-static-live','mage-world-live','mage-missile-live','fire-golem-world-live','fire-golem-combat-live','fire-golem-aura-live','fire-golem-death-live','blood-golem-world-live','blood-golem-combat-live','clay-golem-world-live','clay-golem-combat-live','iron-golem-world-live','skeleton-world-live','skeleton-combat-live'),
        extra_ground_input_commands=('clay-golem-world-right','clay-golem-world-quick'),
        extra_wait_results={'paladin-thorns-check':'paladin-thorns-result','paladin-thorns-reload-check':'paladin-thorns-reload-result','fire-ground-check':'fire-ground-result','fire-ground-reload-check':'fire-ground-reload-result','chain-lightning-check':'chain-lightning-result','chain-lightning-reload-check':'chain-lightning-reload-result','sorceress-skills-check':'sorceress-skills-result'},
        save_command='fire-golem-death-save',save_result='fire-golem-death-save',
        extra_manifest_names={'paladin-thorns':'paladin-thorns-checks.json','fire-ground':'fire-ground-checks.json','chain-lightning':'chain-lightning-checks.json','sentry-unit-path':'sentry-unit-path-fixture.json','sorceress-skills':'sorceress-skills-checks.json','enchant':'enchant-checks.json'},
        extra_target_input_commands=('mage-world-right','mage-world-quick','fire-golem-birth-right','fire-golem-birth-quick','blood-golem-birth-right','blood-golem-birth-quick','clay-golem-world-right','clay-golem-world-quick','skeleton-birth-right','skeleton-birth-quick'),extra_inputs=(Path(__file__).resolve(),))
