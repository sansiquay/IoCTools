#!/usr/bin/env bash
# verify-ideas-backlog.sh
#
# Mechanical guard: prevent shipped/closed/removed ideas from re-entering
# ideas.md silently. Source: workbench memory pin
# feedback_audit_must_honor_shipped_markers (IoCTools#21).
#
# Flags any backlog line that looks like a completed-but-still-listed item:
#   - explicit completion tags (SHIPPED, DONE, COMPLETED, CLOSED, MERGED,
#     REMOVED, DROPPED, SUPERSEDED, OBSOLETE, WON'T DO, WONTFIX)
#   - GitHub-style closed-issue references (Closes #N, Fixed in #N)
#   - markdown strikethrough (~~text~~) — kept entries should be deleted,
#     not preserved struck-through
#   - completed checklist items ([x] or [X])
#
# Exit codes:
#   0  -> clean
#   1  -> offending lines found (printed with file:line: pattern context)
#   2  -> ideas.md not found (premise wrong; CI should fail loud)
#
# Usage:
#   scripts/verify-ideas-backlog.sh             # check repo-root ideas.md
#   scripts/verify-ideas-backlog.sh path/to.md  # check explicit file

set -u

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
target="${1:-${repo_root}/ideas.md}"

if [[ ! -f "${target}" ]]; then
  echo "verify-ideas-backlog: ERROR: backlog file not found: ${target}" >&2
  exit 2
fi

# Patterns are POSIX-ERE so grep -E works portably (macOS BSD + GNU).
# Tag pattern: word-boundary-ish, case-insensitive, allows surrounding
# punctuation like (SHIPPED), [DONE], <!-- SHIPPED -->.
tag_pattern='(^|[^A-Za-z])(SHIPPED|DONE|COMPLETED|CLOSED|MERGED|REMOVED|DROPPED|SUPERSEDED|OBSOLETE|WONTFIX|WON.?T DO)([^A-Za-z]|$)'
closes_pattern='(^|[^A-Za-z])(Closes|Closed|Fixes|Fixed|Resolves|Resolved) +(in +)?#[0-9]+'
strike_pattern='~~[^~]+~~'
checked_pattern='^[[:space:]]*[-*+] +\[[xX]\] '

# grep -nE returns non-zero when no match; we capture and union manually.
hits="$(
  {
    grep -nEi  "${tag_pattern}"    "${target}" || true
    grep -nE   "${closes_pattern}" "${target}" || true
    grep -nE   "${strike_pattern}" "${target}" || true
    grep -nE   "${checked_pattern}" "${target}" || true
  } | sort -t: -k1,1n -u
)"

if [[ -n "${hits}" ]]; then
  echo "verify-ideas-backlog: shipped/closed/removed markers found in ${target}:" >&2
  echo "${hits}" >&2
  echo "" >&2
  echo "These entries should be DELETED from the backlog (and recorded in CHANGELOG" >&2
  echo "or issue history) rather than left in ideas.md. See workbench memory pin" >&2
  echo "feedback_audit_must_honor_shipped_markers." >&2
  exit 1
fi

echo "verify-ideas-backlog: ${target} clean (no shipped/closed/removed markers)"
exit 0
