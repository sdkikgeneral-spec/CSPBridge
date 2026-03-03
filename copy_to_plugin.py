#!/usr/bin/env python3
# ビルド後にプラグインファイルを指定フォルダにコピーするスクリプト
# Usage: copy_to_plugin.py <dst_dir> <stamp_file> <src> [src ...]
import sys
import shutil
import pathlib

dst_dir = pathlib.Path(sys.argv[1])
stamp_file = pathlib.Path(sys.argv[2])
src_files = sys.argv[3:]

dst_dir.mkdir(parents=True, exist_ok=True)
for src in src_files:
    shutil.copy2(src, dst_dir)
    print(f'Copied: {pathlib.Path(src).name} -> {dst_dir}')

stamp_file.write_text('done\n', encoding='utf-8')
