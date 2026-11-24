#!/usr/bin/env bash
set -euo pipefail

if [ -z "${WEBHOOK_URL:-}" ]; then
  echo "::notice::NOTIFY_WEBHOOK_URL is not set, notification skipped"
  exit 0
fi

: "${MESSAGE:?MESSAGE is required}"

if [[ "$WEBHOOK_URL" == *discord* ]]; then
  payload=$(jq -n --arg msg "$MESSAGE" '{content: $msg, allowed_mentions: {parse: []}}')
else
  payload=$(jq -n --arg msg "$MESSAGE" '{text: $msg}')
fi

curl -fsS -X POST -H "Content-Type: application/json" -d "$payload" "$WEBHOOK_URL"
echo "Notification sent"
