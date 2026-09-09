#!/bin/bash

set -e

INSTANCE="$1"
SESSION="madoutserver-${INSTANCE}"
SCRIPT="/opt/MadOut2_linux/start-${INSTANCE}.sh"

tmux kill-session -t "$SESSION" 2>/dev/null || true

tmux new-session -d -s "$SESSION" "$SCRIPT"

while tmux has-session -t "$SESSION" 2>/dev/null; do
    sleep 1
done