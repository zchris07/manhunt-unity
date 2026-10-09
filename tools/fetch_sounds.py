"""Fetches the CC0 sound packs this port uses and copies the clips it plays into Assets/Vision/Resources/Audio/Fx.

Every source is CC0 (public domain); see CREDITS.md. Downloads go to a cache folder (default: tools/.sound_cache) and are
checked against the SHA-256 recorded here the first time they are fetched (pass --record to print new hashes).

Usage: python tools/fetch_sounds.py [--cache DIR] [--record]
"""
import argparse
import hashlib
import io
import os
import shutil
import sys
import urllib.request
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Vision', 'Resources', 'Audio', 'Fx')

# (key, url, sha256 or None until recorded)
PACKS = [
    ('tabasco', 'https://opengameart.org/sites/default/files/sounds.zip', None),
    ('shotgun', 'https://opengameart.org/sites/default/files/shotgunsounds.zip', None),
    ('gunreload', 'https://opengameart.org/sites/default/files/gunreload1.wav', None),
    ('clipload', 'https://opengameart.org/sites/default/files/clipload1.wav', None),
    ('swishes', 'https://opengameart.org/sites/default/files/swishes.zip', None),
    ('steps', 'https://opengameart.org/sites/default/files/%5Bkdd%5DDifferentSteps_0.zip', None),
    ('rpg', 'https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip', None),
    ('impact', 'https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip', None),
    ('sfx100', 'https://opengameart.org/sites/default/files/100-CC0-SFX_0.zip', None),
]

HASHES_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'sound_hashes.txt')


def load_hashes():
    h = {}
    if os.path.exists(HASHES_FILE):
        for line in open(HASHES_FILE, encoding='utf-8'):
            parts = line.split()
            if len(parts) == 2:
                h[parts[0]] = parts[1]
    return h


def fetch(key, url, cache, hashes, record):
    path = os.path.join(cache, key + os.path.splitext(url)[1].split('%')[0])
    if not os.path.exists(path):
        print('fetching', url)
        req = urllib.request.Request(url, headers={'User-Agent': 'manhunt-unity sound fetcher'})
        with urllib.request.urlopen(req, timeout=120) as r, open(path, 'wb') as f:
            shutil.copyfileobj(r, f)
    data = open(path, 'rb').read()
    digest = hashlib.sha256(data).hexdigest()
    if key in hashes and hashes[key] != digest:
        sys.exit(f'{key}: hash mismatch ({digest}); delete {path} or check the source')
    if key not in hashes:
        hashes[key] = digest
        record = True
    return data, record


def members(data):
    z = zipfile.ZipFile(io.BytesIO(data))
    return {os.path.basename(n).lower(): n for n in z.namelist()
            if not n.endswith('/') and '__MACOSX' not in n and not os.path.basename(n).startswith('._')}, z


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--cache', default=os.path.join(ROOT, 'tools', '.sound_cache'))
    ap.add_argument('--record', action='store_true')
    ap.add_argument('--out', default=OUT)
    a = ap.parse_args()
    out = a.out
    os.makedirs(a.cache, exist_ok=True)
    os.makedirs(out, exist_ok=True)
    hashes = load_hashes()
    record = a.record
    blobs = {}
    for key, url, _ in PACKS:
        blobs[key], record = fetch(key, url, a.cache, hashes, record)

    def put(name, data):
        with open(os.path.join(out, name), 'wb') as f:
            f.write(data)
        print('  ', name, len(data))

    # Single files.
    put('gun_reload.wav', blobs['gunreload'])
    put('clip_load.wav', blobs['clipload'])

    # From each zip: every audio file, under a prefixed name, so the game can pick what it plays.
    for key in ('tabasco', 'shotgun', 'swishes', 'steps', 'rpg', 'impact', 'sfx100'):
        names, z = members(blobs[key])
        for base, full in sorted(names.items()):
            if not base.endswith(('.wav', '.ogg', '.mp3')):
                continue
            put(f'{key}_{base.replace(" ", "_")}', z.read(full))

    if record:
        with open(HASHES_FILE, 'w', encoding='utf-8') as f:
            for k in sorted(hashes):
                f.write(f'{k} {hashes[k]}\n')
        print('recorded hashes in', HASHES_FILE)


if __name__ == '__main__':
    main()
