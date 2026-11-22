#!/usr/bin/env bash
# test-lib.sh — checks the shared game-lock helpers in lib.sh against a throwaway lock folder.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
. "$here/lib.sh"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
GAME_LOCK="$tmp/game-access.lock"
failures=0
check() { # <name> <command…>
  local name=$1
  shift
  if "$@"; then echo "ok   $name"; else echo "FAIL $name"; failures=$((failures + 1)); fi
}
owner_session() { python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["session"])' "$GAME_LOCK/owner.json"; }

# A lock someone removed mid-run is taken again instead of failing the run.
acquire_game_lock 60 >/dev/null 2>&1
rm -rf "$GAME_LOCK"
check "renew re-takes a lock that vanished" renew_game_lock 60
check "renewed lock is ours" test "$(owner_session)" = "$GAME_LOCK_SESSION"

# Another session's lock is never overwritten by a renew.
printf '{"session":"other","game":"civ","pid":1,"expires":%d}\n' $(($(date +%s) + 600)) >"$GAME_LOCK/owner.json"
check "renew leaves another session's lock alone" renew_game_lock 60
check "other session still owns it" test "$(owner_session)" = "other"

# Two sessions racing for an expired lock: exactly one wins.
rm -rf "$GAME_LOCK"
mkdir -p "$GAME_LOCK"
printf '{"session":"gone","game":"x","pid":999999,"expires":1}\n' >"$GAME_LOCK/owner.json"
for n in 1 2 3 4 5 6; do
  (GAME_LOCK_SESSION="racer-$n" acquire_game_lock 60 >/dev/null 2>&1 && echo "racer-$n" >>"$tmp/winners") &
done
wait
check "one racer takes an expired lock" test "$(wc -l <"$tmp/winners")" -eq 1
check "the winner owns the lock" test "$(owner_session)" = "$(cat "$tmp/winners")"

# A half-written owner file blocks a takeover while fresh, and frees up once it is old.
mkdir -p "$GAME_LOCK"
printf '{"session":' >"$GAME_LOCK/owner.json"
check "a fresh unreadable lock counts as held" lock_is_held
touch -d '20 minutes ago' "$GAME_LOCK/owner.json" "$GAME_LOCK"
check "an old unreadable lock is taken over" acquire_game_lock 60

[ "$failures" -eq 0 ] && echo "all passed" || { echo "$failures failed"; exit 1; }
