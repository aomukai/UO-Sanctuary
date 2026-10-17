"""Download Classic Client data from the publisher using a pinned Linux helper."""
import hashlib
import importlib.util
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = ROOT / ".tools/uo-patcher"
COMMIT = "e7e890334705032f66b2ed99eee08e5cace6db93"
SOURCE_SHA256 = "33a37aa2c70bd16de3425acce6af6926087b9d7c020f2bc411053bd384db1a56"


def main():
    if not REPOSITORY.exists():
        REPOSITORY.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(["git", "clone", "https://github.com/andrezaiats/uo-patcher.git",
                        str(REPOSITORY)], check=True)
        subprocess.run(["git", "checkout", "--detach", COMMIT], cwd=REPOSITORY, check=True)
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPOSITORY, text=True).strip()
    source = REPOSITORY / "uo_patcher.py"
    if commit != COMMIT or hashlib.sha256(source.read_bytes()).hexdigest() != SOURCE_SHA256:
        raise RuntimeError("The asset downloader does not match the inspected source pin.")

    spec = importlib.util.spec_from_file_location("uo_patcher", source)
    patcher = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(patcher)
    parse_product = patcher.parse_prod
    collect_files = patcher.collect_files
    loose_files = {}
    pack_entries = {}

    def game_product(url):
        product = parse_product(url)
        product["stages"] = [stage for stage in product["stages"] if stage["name"] != "notes"]
        return product

    def remember_files(*args):
        loose, packed = collect_files(*args)
        loose_files.update((entry["name"], entry) for entry in loose)
        pack_entries.update(((entry["pack_name"], entry["ph"], entry["sh"]), entry) for entry in packed)
        return loose, packed

    patcher.parse_prod = game_product
    patcher.collect_files = remember_files
    destination = ROOT / ".local/uo-data"
    destination.mkdir(parents=True, exist_ok=True)
    scratch_parent = Path(os.environ.get("SANCTUARY_ASSET_SCRATCH", "/dev/shm"))
    if not scratch_parent.is_dir() or shutil.disk_usage(scratch_parent).free < 5 * 1024**3:
        raise RuntimeError("Asset staging needs 5 GiB free. Set SANCTUARY_ASSET_SCRATCH to a suitable folder.")

    with tempfile.TemporaryDirectory(prefix="sanctuary-uo-", dir=scratch_parent) as scratch:
        scratch = Path(scratch)
        # exFAT is slow for thousands of chunks; retain only finished archives there.
        for parent, directories, filenames in os.walk(destination):
            directories[:] = [name for name in directories if name != ".pack_staging"]
            relative = Path(parent).relative_to(destination)
            (scratch / relative).mkdir(parents=True, exist_ok=True)
            for name in filenames:
                if not name.endswith(".tmp"):
                    (scratch / relative / name).symlink_to(Path(parent) / name)
        patcher.run_patcher_download(scratch, workers=32, retries=3)
        patcher.run_patch(scratch, workers=32, retries=3)
        if patcher.verify_unpacked(scratch, list(loose_files.values())):
            raise RuntimeError("Game installation has missing or incomplete files.")
        if patcher.verify_archives(scratch, list(pack_entries.values())):
            raise RuntimeError("Game installation has missing or incomplete archive entries.")
        for parent, directories, filenames in os.walk(scratch):
            directories[:] = [name for name in directories if name != ".pack_staging"]
            relative = Path(parent).relative_to(scratch)
            (destination / relative).mkdir(parents=True, exist_ok=True)
            for name in filenames:
                source_file = Path(parent) / name
                if not source_file.is_symlink():
                    target = destination / relative / name
                    temporary = target.with_name(target.name + ".installing")
                    shutil.copy2(source_file, temporary)
                    temporary.replace(target)
    print(f"Verified game data installed: {destination}")


if __name__ == "__main__":
    main()
