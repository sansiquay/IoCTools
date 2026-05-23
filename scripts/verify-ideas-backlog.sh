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
#
# Tag pattern: ALL-CAPS only, and must be in a tag-like context — either
# wrapped in punctuation ((SHIPPED), [DONE], <!-- SHIPPED -->) or used as a
# trailing/leading marker (`- foo SHIPPED`, `SHIPPED: foo`). Mixed-case
# occurrences of the same words in normal prose (e.g., "Track merged
# registration sources", "Support closed generic fallbacks") are NOT
# flagged. Case-SENSITIVE on purpose.
tag_word='(SHIPPED|DONE|COMPLETED|CLOSED|MERGED|REMOVED|DROPPED|SUPERSEDED|OBSOLETE|WONTFIX|WONT DO)'
# Wrapped: ( ) [ ] { } < > or HTML comment markers around the tag word.
# Note on char-class escaping: ']' must be FIRST inside a bracket class
# to be literal in POSIX ERE (no backslash escape works portably).
tag_wrapped="[([{<]${tag_word}([[:space:]]|[])>}!.,:;-])|<!--[[:space:]]*${tag_word}"
# Trailing/leading: tag word followed/preceded by ':' / '-' / end-of-line.
tag_decorated="(^|[[:space:]])${tag_word}([[:space:]]*[:.-]|[[:space:]]*\$)"

# Closes/Fixes are GitHub-canonical PR keywords that close an issue. Match
# the canonical case-sensitive forms only ("closed" lowercase is also a
# common English word; we already cover the ALL-CAPS CLOSED above).
closes_pattern='(^|[^A-Za-z])(Closes|Closed|Fixes|Fixed|Resolves|Resolved):? +(in +)?#[0-9]+'
strike_pattern='~~[^~]+~~'
checked_pattern='^[[:space:]]*[-*+] +\[[xX]\] '

# grep -nE returns non-zero when no match; we capture and union manually.
# Note: NO -i flag on tag patterns — case-sensitive ALL-CAPS only.
hits="$(
  {
    grep -nE   "${tag_wrapped}"    "${target}" || true
    grep -nE   "${tag_decorated}"  "${target}" || true
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
