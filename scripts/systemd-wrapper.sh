#!/usr/bin/env bash
set -euo pipefail

INSTANCE="${1:-}"
if [[ ! "$INSTANCE" =~ ^[1-9][0-9]*$ ]]; then
    echo 'Usage: systemd-wrapper.sh <positive instance number>' >&2
    exit 1
fi

SERVER_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
SESSION="madoutserver-${INSTANCE}"
SOCKET="$SESSION"
SCRIPT="$SERVER_DIR/start-${INSTANCE}.sh"
if [[ ! -f "$SCRIPT" || ! -r "$SCRIPT" ]]; then
    echo "Missing or unreadable launcher: $SCRIPT" >&2
    exit 1
fi
if ! TMUX_BIN="$(command -v tmux)"; then
    echo 'tmux is not installed or is missing from PATH.' >&2
    exit 1
fi
unset TMUX TMUX_PANE
cd -- "$SERVER_DIR"

if "$TMUX_BIN" -L "$SOCKET" list-sessions >/dev/null 2>&1; then
    echo "tmux socket $SOCKET is already in use; stop that instance first." >&2
    exit 1
fi

PANE=''
STARTED=0
pane_state() {
    "$TMUX_BIN" -L "$SOCKET" display-message -p -t "$PANE" \
        '#{pane_dead}|#{pane_dead_status}|#{pane_dead_signal}'
}
cleanup() {
    local result=$? state dead status signal
    trap - EXIT
    trap '' TERM INT
    set +e
    if (( STARTED )); then
        state="$(pane_state 2>/dev/null)"
        IFS='|' read -r dead status signal <<< "$state"
        if [[ "$dead" == 0 ]]; then
            "$TMUX_BIN" -L "$SOCKET" send-keys -t "$PANE" C-c 2>/dev/null
            for ((waited=0; waited<25; waited++)); do
                state="$(pane_state 2>/dev/null)" || break
                IFS='|' read -r dead status signal <<< "$state"
                [[ "$dead" == 1 ]] && break
                sleep 1
            done
        fi
        "$TMUX_BIN" -L "$SOCKET" kill-server 2>/dev/null
    fi
    exit "$result"
}
trap cleanup EXIT
trap 'echo "Stopping $SESSION"; exit 0' TERM INT

PANE="$("$TMUX_BIN" -L "$SOCKET" -f /dev/null new-session -d -s "$SESSION" \
    -c "$SERVER_DIR" -x 120 -y 40 -P -F '#{pane_id}' 'exec /bin/sleep 86400')"
STARTED=1
"$TMUX_BIN" -L "$SOCKET" set-option -g default-shell /bin/bash
"$TMUX_BIN" -L "$SOCKET" set-option -w -t "$PANE" remain-on-exit on
printf -v launch_command 'exec /bin/bash %q' "$SCRIPT"
"$TMUX_BIN" -L "$SOCKET" respawn-pane -k -t "$PANE" "$launch_command"
echo "Started $SESSION; console: tmux -L $SOCKET attach -t $SESSION"

while true; do
    if ! state="$(pane_state)"; then
        echo "Lost tmux pane for $SESSION." >&2
        exit 1
    fi
    IFS='|' read -r dead status signal <<< "$state"
    if [[ "$dead" == 1 ]]; then
        echo "$SESSION exited: status=${status:-unknown}, signal=${signal:-none}" >&2
        "$TMUX_BIN" -L "$SOCKET" capture-pane -p -t "$PANE" -S -80 >&2 || true
        if [[ "$status" =~ ^[0-9]+$ ]] && (( status <= 255 )); then
            exit "$status"
        fi
        if [[ -n "$signal" ]]; then
            number="$(kill -l "$signal" 2>/dev/null)" || number=''
            if [[ "$number" =~ ^[0-9]+$ ]] && (( number < 128 )); then
                exit "$((128 + number))"
            fi
        fi
        exit 1
    fi
    if [[ "$dead" != 0 ]]; then
        echo "Invalid tmux pane state for $SESSION: $state" >&2
        exit 1
    fi
    sleep 1
done
