#!/usr/bin/env python3
"""Bind the shared startup kernel to its complete older source controls."""
import argparse
import json
from pathlib import Path
import skill_checkpoint_evidence as e


def review(check=False):
    inputs = {}
    def pin(path):
        inputs[str(path.relative_to(e.ROOT))] = e.sha(path)
        return e.load(path) if path.suffix == '.json' else None
    source = pin(e.ROOT/'tools/reference/skill-startup-source-evidence.json')
    assert source['cases'] == 6796 and source['startFunctionCount'] == 91
    assert source['sanitizers'] == 'address,undefined:clean'
    assert not source['currentExecutableParity'] and not source['liveNpcControllerEnabled']
    for p,h in source['inputs'].items():
        assert e.sha(e.ROOT/p) == h
        pin(e.ROOT/p)
    for p in ['src/Sanctuary.Content/SkillStartup.cs', 'src/Sanctuary.Content/SkillExecution.cs',
              'tests/Sanctuary.Integration/SkillStartupChecks.cs', 'tests/check_skill_startup.py',
              'tools/probe_skill_startup.py']:
        pin(e.ROOT/p)
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', inputs=inputs, sourceContexts=6796,
        completeOlderStartupWrapper=True, completeOlderStartDispatcher=True,
        currentExecutableParityVerified=False, npcWorldAdapterImplemented=False,
        handlerEnabled=False, handlers=203,
        scope=[
            'Selected-skill startup preserves the source targetable-only enemy/pet/ally short-circuit, targetable/corpse cleanup, effective-versus-item-owned rank, zero-charge rejection, ally rejection and town cancellation.',
            'Direct startup preserves room/record/player-mana order, item flag bypass, all five source collision masks, zero-coordinate and invalid-LOS behavior, signed start index bounds, absent-start success, callback result, payment and periodic-event deletion order.',
            'Services are mandatory and abstract. Missing nested bodies propagate refusal without subsequent payment/event deletion. This kernel complements the existing complete do dispatcher; it supplies neither an invented successful callback nor a player-cast surrogate for NPCs.'
        ], limits=[
            'Both routines are executed from the complete pinned older source with declared nested services. This does not establish current 3.3.93847 executable parity or implement those permission, collision, rank, mana, effect or event services.',
            'No live NPC/Shadow adapter calls this kernel yet. Mode/sequence/action events, effect/combat hosts, AI105/106 and learned Shadow casting remain gated.',
            'The three-boot native test compares kernel results and ordered services; its saved digest only proves stable test inputs. Retained suites separately exercise real original NPC bodies, items and resource saves.',
            'No new playable skill, public input, effect, sound or icon is enabled. Existing build/saves and parked Shadow appearances remain unchanged.'
        ])
    path = e.ROOT/'tools/reference/skill-startup-review-evidence.json'
    rendered = json.dumps(result, indent=2)+'\n'
    if check:
        assert path.read_text() == rendered, 'Startup review is stale'
    else:
        path.write_text(rendered)
    print('Shared startup kernel reviewed: 6796 complete older-source contexts; nested NPC hosts and learned Shadows remain gated.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    review(p.parse_args().check)
