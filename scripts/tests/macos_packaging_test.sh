#!/usr/bin/env bash
# 使用受控 macOS 工具验证镜像布局、校验文件和失败传播，不操作桌面。
set -euo pipefail
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
fixture="$(mktemp -d "${CODEWF_TEST_TMP:-${TMPDIR:-/tmp}}/app-macos-test-XXXXXX")"
[[ "$fixture" == */app-macos-test-* ]] || exit 1
trap 'rm -rf "$fixture"' EXIT
mkdir -p "$fixture/bin" "$fixture/publish/I18n" "$fixture/publish/NexusDash.dSYM" "$fixture/tmp"
printf 'executable' >"$fixture/publish/NexusDash"
printf '{}' >"$fixture/publish/I18n/en-US.json"
printf 'symbols' >"$fixture/publish/NexusDash.pdb"
printf 'symbols' >"$fixture/publish/NexusDash.dSYM/data"
printf 'image' >"$fixture/icon.png"
cat >"$fixture/bin/sips" <<'STUB'
#!/usr/bin/env bash
printf 'image' >"${!#}"
STUB
cat >"$fixture/bin/iconutil" <<'STUB'
#!/usr/bin/env bash
printf 'icon' >"${!#}"
STUB
cat >"$fixture/bin/codesign" <<'STUB'
#!/usr/bin/env bash
[[ "${SIMULATE_SIGN_FAILURE:-0}" == '0' ]]
STUB
cat >"$fixture/bin/ln" <<'STUB'
#!/usr/bin/env bash
printf '/Applications' >"${!#}"
STUB
cat >"$fixture/bin/hdiutil" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
[[ "${SIMULATE_DMG_FAILURE:-0}" == '0' ]] || exit 28
case "$1" in
  create)
    for ((index=1;index<$#;index++)); do
      if [[ "${!index}" == -srcfolder ]]; then next=$((index+1)); cp -R "${!next}" "$TEST_CAPTURE"; fi
    done
    printf 'writable image' >"${!#}"
    ;;
  attach|detach) : ;;
  convert) while [[ $# -gt 0 ]]; do if [[ "$1" == -o ]]; then printf 'compressed image' >"$2"; fi; shift; done ;;
  verify) [[ -s "$2" ]] ;;
  *) exit 2 ;;
esac
STUB
cat >"$fixture/bin/osascript" <<'STUB'
#!/usr/bin/env bash
printf 'Finder metadata' >"$2/.DS_Store"
STUB
cat >"$fixture/bin/sync" <<'STUB'
#!/usr/bin/env bash
exit 0
STUB
cat >"$fixture/bin/shasum" <<'STUB'
#!/usr/bin/env bash
[[ "$1" == '-a' && "$2" == '256' ]] && shift 2
sha256sum "$@"
STUB
chmod +x "$fixture/bin/"*
export PATH="$fixture/bin:$PATH" PUBLISH_DIR="$fixture/publish" OUTPUT_DIR="$fixture/success" ICON_SOURCE="$fixture/icon.png" TMPDIR="$fixture/tmp" TEST_CAPTURE="$fixture/captured"
bash "$repository_root/scripts/package_macos_dmg.sh" osx-x64 1.2.3 >"$fixture/success.log" 2>&1 || { cat "$fixture/success.log"; exit 1; }
[[ -f "$TEST_CAPTURE/NexusDash.app/Contents/MacOS/NexusDash" ]]
[[ -f "$TEST_CAPTURE/NexusDash.app/Contents/Resources/I18n/en-US.json" ]]
[[ ! -e "$TEST_CAPTURE/NexusDash.app/Contents/Resources/NexusDash.dSYM" ]]
[[ ! -e "$TEST_CAPTURE/NexusDash.app/Contents/Resources/NexusDash.pdb" ]]
[[ -f "$PUBLISH_DIR/NexusDash.dSYM/data" ]]
[[ -s "$OUTPUT_DIR/NexusDash-1.2.3-osx-x64.dmg.sha256" ]]
(cd "$OUTPUT_DIR" && shasum -a 256 -c ./*.sha256)
export OUTPUT_DIR="$fixture/sign-failure" SIMULATE_SIGN_FAILURE=1
if bash "$repository_root/scripts/package_macos_dmg.sh" osx-x64 1.2.3 >"$fixture/sign-failure.log" 2>&1; then
  echo 'Signing failure was swallowed.' >&2; exit 1
fi
export OUTPUT_DIR="$fixture/dmg-failure" SIMULATE_SIGN_FAILURE=0 SIMULATE_DMG_FAILURE=1
if bash "$repository_root/scripts/package_macos_dmg.sh" osx-x64 1.2.3 >"$fixture/dmg-failure.log" 2>&1; then
  echo 'DMG failure was swallowed.' >&2; exit 1
fi
if grep -q 'Created:' "$fixture/sign-failure.log" "$fixture/dmg-failure.log"; then exit 1; fi
echo 'PASS: standard resources, symbol exclusion, SHA256 and signing/DMG failure propagation.'
