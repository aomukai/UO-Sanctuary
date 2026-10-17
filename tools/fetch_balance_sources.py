#!/usr/bin/env python3
"""Fetch the mechanical source tables pinned by the balance catalog manifest."""
import argparse
import hashlib
import json
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--destination', type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'data/Sanctuary/Balance/sources.json').read_text())
args.destination.mkdir(parents=True, exist_ok=True)
for name, record in manifest.items():
    path = args.destination / (name + '.txt')
    data = path.read_bytes() if path.exists() else urllib.request.urlopen(record['url'], timeout=30).read()
    if hashlib.sha256(data).hexdigest() != record['sha256']:
        raise SystemExit(f'{name}: source differs from the pinned SHA-256; no replacement was written')
    if not path.exists():
        path.write_bytes(data)
print(f'Verified {len(manifest)} pinned source tables in {args.destination}')
