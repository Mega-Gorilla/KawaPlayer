#!/usr/bin/env bash
# Builds a .unitypackage from a list of .meta paths (relative to the project root).
#
# Replaces pCYSl5EDgo/create-unitypackage, a wrapper around the npm package
# "unitypackage" 1.0.8, and reproduces its layout exactly:
#   <guid>/asset.meta   the .meta, copied verbatim
#   <guid>/asset        the asset, copied verbatim (regular files only -- a folder
#                       asset has no such entry)
#   <guid>/pathname     the asset path relative to the project root, no newline
# packed with `tar -C <stage> .` (so every entry starts with "./") and gzipped.
#
# Where the npm package silently skipped a missing asset or overwrote a
# duplicate guid, this fails: a package missing files is worse than no package.
#
# usage: create-unitypackage.sh <output.unitypackage> <meta-list-file>
#   meta-list-file: one .meta path per line, e.g.
#     find "Packages/<name>/" -name '*.meta' | LC_ALL=C sort
set -euo pipefail
export LC_ALL=C

die() { echo "::error::$*" >&2; exit 1; }

[ $# -eq 2 ] || { echo "usage: $0 <output.unitypackage> <meta-list-file>" >&2; exit 2; }
out=$1
list=$2
[ -f "$list" ] || die "meta list not found: $list"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
stage="$work/stage"
mkdir "$stage"

total=0
with_asset=0
folders=0

# `|| [ -n "$meta" ]` keeps a last line that has no trailing newline.
while IFS= read -r meta || [ -n "$meta" ]; do
  meta=${meta%$'\r'}
  [ -n "$meta" ] || continue
  case "$meta" in
    *.meta) ;;
    *) die "not a .meta path: $meta" ;;
  esac
  [ -f "$meta" ] || die "meta file missing: $meta"

  # Unity writes the guid as 32 lowercase hex digits on its own line.
  guid=$(sed -n 's/^guid: \([0-9a-f]\{32\}\)\r\{0,1\}$/\1/p' "$meta")
  case "$guid" in
    "") die "no 'guid: <32 hex>' line in $meta" ;;
    *$'\n'*) die "multiple guid lines in $meta" ;;
  esac

  target=${meta%.meta}
  dir="$stage/$guid"
  if [ -e "$dir" ]; then
    die "duplicate guid $guid: $meta and $(cat "$dir/pathname").meta"
  fi
  mkdir "$dir"
  cp "$meta" "$dir/asset.meta"

  if grep -q '^folderAsset: yes' "$meta"; then
    [ -d "$target" ] || die "folder meta but target is not a directory: $meta"
    folders=$((folders + 1))
  else
    [ -f "$target" ] || die "asset missing for $meta (expected regular file $target)"
    cp "$target" "$dir/asset"
    with_asset=$((with_asset + 1))
  fi

  printf '%s' "$target" > "$dir/pathname"
  total=$((total + 1))
done < "$list"

[ "$total" -gt 0 ] || die "meta list is empty: $list"

# Fixed order, owner and mtime so two builds of the same tree come out alike.
# Byte-identical archives are a convenience, not a requirement.
tar -cf "$work/package.tar" \
  --sort=name --owner=0 --group=0 --numeric-owner \
  --mtime="@${SOURCE_DATE_EPOCH:-$(date +%s)}" \
  -C "$stage" .
gzip -n -9 -c "$work/package.tar" > "$out"

echo "created $out: $total entries ($with_asset with asset, $folders folders)"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### UnityPackage"
    echo "- file: \`$out\`"
    echo "- entries: $total (assets: $with_asset, folders: $folders)"
  } >> "$GITHUB_STEP_SUMMARY"
fi
