#!/usr/bin/env python3
# ビルド後にプラグインファイルをZIPにまとめるスクリプト
# Usage: make_release_zip.py <output_zip> <stamp_file> <src> [src ...]
import sys
import zipfile
import pathlib

output_zip = pathlib.Path(sys.argv[1])
stamp_file = pathlib.Path(sys.argv[2])
src_files  = sys.argv[3:]

output_zip.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(output_zip, 'w', zipfile.ZIP_DEFLATED) as zf:
    for src in src_files:
        p = pathlib.Path(src)
        zf.write(p, p.name)
        print(f'  Added: {p.name}')

print(f'Created: {output_zip}')
stamp_file.write_text('done\n', encoding='utf-8')
