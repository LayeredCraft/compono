#!/usr/bin/env bash
# validate-theory-skip-parity.sh
#
# B6.0 (PLAN-0071) regression guard for the ComposeData theory-level skip interaction matrix
# (ADR-0070 Amendment 5). Compares an actual test run's outcomes and skip reasons - for every one of
# the 84 theory x ComposeData interaction cases - against an explicit expected table, and (when more
# than one run is supplied) cross-checks the runs against each other so a JIT/AOT divergence fails.
#
# Input is the Microsoft Testing Platform (MTP) human-readable console output produced with:
#     --show-test-results all --no-ansi --progress off
# which renders one line per test:
#     passed  <FQN>(value: 1) (3ms)
#     skipped <FQN>(value: 1) (0ms)
#       <reason>                 <- immediately-following indented reason line
#     failed  <FQN>(...) ...
#
# This MTP console output is a text format, NOT a structured-result format. MTP/xUnit can emit
# structured JUnit or CTRF reports, but no report extension is referenced in this repo, so this
# slice deliberately keeps a text-based validator and adds no new dependency. Every supplied input
# must contain exactly the expected 84 cases; an input with zero recognizable test results (empty,
# malformed, unparseable, or ANSI-contaminated) fails validation rather than passing vacuously.
#
# Detects: no results, missing cases, duplicate cases, unexpected cases, unexpected skips,
# unexpected executions, wrong skip reasons, unexpected failures, a failed command, and JIT/AOT
# (run-to-run) mismatches. Exits nonzero on any of these.
#
# Usage:
#   scripts/validate-theory-skip-parity.sh [--label NAME] --result FILE [--result FILE ...]
#   scripts/validate-theory-skip-parity.sh [--label NAME] -- COMMAND [ARG ...]
#
#   --result FILE   Validate a captured MTP output file (repeatable). Labels default to the path.
#   --label NAME    Label applied to the run captured from a "-- COMMAND" invocation.
#   -- COMMAND...   Run COMMAND, capture stdout+stderr, and validate that single run. A command
#                   that exits nonzero (including one that fails to launch) never yields PASS.
#   -h | --help     Show this help.
#
# When several runs are supplied they must all agree case-for-case (outcome and skip reason); any
# disagreement is reported as a mismatch. Parsing is order-independent.
set -euo pipefail

script_name="$(basename "$0")"

usage() {
    cat <<'EOF'
Validate the ComposeData theory-level skip parity matrix (84 cases) from MTP console output.

Usage:
  validate-theory-skip-parity.sh [--label NAME] --result FILE [--result FILE ...]
  validate-theory-skip-parity.sh [--label NAME] -- COMMAND [ARG ...]

  --result FILE   Validate a captured MTP output file (repeatable). Labels default to the path.
  --label NAME    Label applied to the run captured from a "-- COMMAND" invocation.
  -- COMMAND...   Run COMMAND, capture stdout+stderr, and validate that single run. A command
                  that exits nonzero (including one that fails to launch) never yields PASS.
  -h | --help     Show this help.

If several runs are supplied they must all agree case-for-case (outcome and skip reason).
EOF
}

label=""
run_rc=""
result_files=()
run_command=()

while [ "$#" -gt 0 ]; do
    case "$1" in
        --result)
            [ "$#" -ge 2 ] || { echo "$script_name: --result requires a FILE argument" >&2; exit 2; }
            result_files+=("$2")
            shift 2
            ;;
        --label)
            [ "$#" -ge 2 ] || { echo "$script_name: --label requires a NAME argument" >&2; exit 2; }
            label="$2"
            shift 2
            ;;
        --)
            shift
            run_command=("$@")
            break
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "$script_name: unknown argument '$1' (see --help)" >&2
            exit 2
            ;;
    esac
done

tmp_files=()
cleanup() {
    for f in "${tmp_files[@]+"${tmp_files[@]}"}"; do
        [ -n "$f" ] && rm -f "$f"
    done
}
trap cleanup EXIT

if [ "${#run_command[@]}" -gt 0 ]; then
    [ -z "$label" ] && label="command"
    out="$(mktemp "${TMPDIR:-/tmp}/parity-run.XXXXXX")"
    tmp_files+=("$out")
    # Capture output and let the validator (not the test runner's exit code) decide pass/fail, so a
    # run with an unexpected failure still gets a full case-by-case report - but record the command's
    # own exit status so a command that fails to launch can never be reported as PASS.
    set +e
    "${run_command[@]}" >"$out" 2>&1
    run_rc=$?
    set -e
    if [ "$run_rc" -ne 0 ]; then
        echo "$script_name: command exited with status $run_rc" >&2
    fi
    result_files+=("$out")
fi

if [ "${#result_files[@]}" -eq 0 ]; then
    echo "$script_name: no input provided (use --result FILE or -- COMMAND)" >&2
    exit 2
fi

for f in "${result_files[@]}"; do
    [ -f "$f" ] || { echo "$script_name: result file not found: $f" >&2; exit 2; }
done

# Explicit expected table: "<theorySuffix> <Dnn> <outcome> <reason>" for all 84 cases. Reason is "-"
# for expected-to-run cases. Kept in lock-step with the JIT/AOT parity test sources.
expected="\
NoTheoryCondition D00 passed -;NoTheoryCondition D01 skipped row;NoTheoryCondition D02 passed -;\
NoTheoryCondition D03 skipped row;NoTheoryCondition D04 skipped row;NoTheoryCondition D05 passed -;\
NoTheoryCondition D06 skipped attr;NoTheoryCondition D07 passed -;NoTheoryCondition D08 skipped attr;\
NoTheoryCondition D09 skipped attr;NoTheoryCondition D10 passed -;\
NoTheoryCondition D11 skipped row;NoTheoryCondition D12 skipped row;NoTheoryCondition D13 skipped row;\
TheorySkipUnlessTrue D00 passed -;TheorySkipUnlessTrue D01 skipped row;TheorySkipUnlessTrue D02 passed -;\
TheorySkipUnlessTrue D03 skipped row;TheorySkipUnlessTrue D04 skipped row;TheorySkipUnlessTrue D05 passed -;\
TheorySkipUnlessTrue D06 skipped attr;TheorySkipUnlessTrue D07 passed -;TheorySkipUnlessTrue D08 skipped attr;\
TheorySkipUnlessTrue D09 skipped attr;TheorySkipUnlessTrue D10 passed -;\
TheorySkipUnlessTrue D11 skipped row;TheorySkipUnlessTrue D12 skipped row;TheorySkipUnlessTrue D13 skipped row;\
TheorySkipUnlessFalse D00 skipped theory;TheorySkipUnlessFalse D01 skipped row;TheorySkipUnlessFalse D02 passed -;\
TheorySkipUnlessFalse D03 skipped row;TheorySkipUnlessFalse D04 skipped row;TheorySkipUnlessFalse D05 passed -;\
TheorySkipUnlessFalse D06 skipped attr;TheorySkipUnlessFalse D07 passed -;TheorySkipUnlessFalse D08 skipped attr;\
TheorySkipUnlessFalse D09 skipped attr;TheorySkipUnlessFalse D10 passed -;\
TheorySkipUnlessFalse D11 skipped row;TheorySkipUnlessFalse D12 skipped row;TheorySkipUnlessFalse D13 skipped row;\
TheorySkipWhenTrue D00 skipped theory;TheorySkipWhenTrue D01 skipped row;TheorySkipWhenTrue D02 passed -;\
TheorySkipWhenTrue D03 skipped row;TheorySkipWhenTrue D04 skipped row;TheorySkipWhenTrue D05 passed -;\
TheorySkipWhenTrue D06 skipped attr;TheorySkipWhenTrue D07 passed -;TheorySkipWhenTrue D08 skipped attr;\
TheorySkipWhenTrue D09 skipped attr;TheorySkipWhenTrue D10 passed -;\
TheorySkipWhenTrue D11 skipped row;TheorySkipWhenTrue D12 skipped row;TheorySkipWhenTrue D13 skipped row;\
TheorySkipWhenFalse D00 passed -;TheorySkipWhenFalse D01 skipped row;TheorySkipWhenFalse D02 passed -;\
TheorySkipWhenFalse D03 skipped row;TheorySkipWhenFalse D04 skipped row;TheorySkipWhenFalse D05 passed -;\
TheorySkipWhenFalse D06 skipped attr;TheorySkipWhenFalse D07 passed -;TheorySkipWhenFalse D08 skipped attr;\
TheorySkipWhenFalse D09 skipped attr;TheorySkipWhenFalse D10 passed -;\
TheorySkipWhenFalse D11 skipped row;TheorySkipWhenFalse D12 skipped row;TheorySkipWhenFalse D13 skipped row;\
TheorySkipUnconditional D00 skipped theory;TheorySkipUnconditional D01 skipped theory;TheorySkipUnconditional D02 skipped theory;\
TheorySkipUnconditional D03 skipped theory;TheorySkipUnconditional D04 skipped theory;TheorySkipUnconditional D05 skipped theory;\
TheorySkipUnconditional D06 skipped theory;TheorySkipUnconditional D07 skipped theory;TheorySkipUnconditional D08 skipped theory;\
TheorySkipUnconditional D09 skipped theory;TheorySkipUnconditional D10 skipped theory;TheorySkipUnconditional D11 skipped theory;\
TheorySkipUnconditional D12 skipped theory;TheorySkipUnconditional D13 skipped theory"

# Hand the files to awk with their display labels, in input order. awk receives:
#   EXPECTED=<table>  LABELFILE=<path>\t<label>\n...  RUNRC=<command exit, or empty>  file paths.
label_file="$(mktemp "${TMPDIR:-/tmp}/parity-labels.XXXXXX")"
tmp_files+=("$label_file")
for f in "${result_files[@]}"; do
    disp="$f"
    if [ "$f" = "${result_files[0]}" ] && [ -n "$label" ]; then
        disp="$label"
    fi
    printf '%s\t%s\n' "$f" "$disp" >> "$label_file"
done

awk -v EXPECTED="$expected" -v LABELFILE="$label_file" -v RUNRC="$run_rc" '
function trim(s) { sub(/^[ \t]+/, "", s); sub(/[ \t]+$/, "", s); return s }

function parse_fqn(fqn,   i, rest, dot, meth, d) {
    i = index(fqn, "ComposeDataTheorySkipMatrix_")
    if (i == 0) return ""
    rest = substr(fqn, i + length("ComposeDataTheorySkipMatrix_"))
    dot = index(rest, ".")
    if (dot == 0) return ""
    meth = substr(rest, dot + 1)
    d = substr(meth, 1, 3)
    if (d !~ /^D[0-9][0-9]$/) return ""
    return substr(rest, 1, dot - 1) " " d
}

BEGIN {
    n = split(EXPECTED, e, ";")
    for (i = 1; i <= n; i++) {
        split(e[i], ef, " ")
        ck = ef[1] " " ef[2]
        expOutcome[ck] = ef[3]
        expReason[ck] = ef[4]
        expKeys[++en] = ck
    }
    # Register every supplied input up-front, preserving input order, BEFORE its contents are read.
    # A file is therefore always accounted for even if it contains no recognizable test results.
    while ((getline line < LABELFILE) > 0) {
        tab = index(line, "\t")
        if (tab > 0) {
            f = substr(line, 1, tab - 1)
            labelOf[f] = substr(line, tab + 1)
            order[++fo] = f
            seenFile[f] = 1
        }
    }
    close(LABELFILE)
}

FNR == 1 { pendingKey = "" }

/^(passed|skipped|failed) / {
    pendingKey = ""
    outcome = $1
    ck = parse_fqn($2)
    if (ck != "") {
        recognized[FILENAME]++
        k = FILENAME SUBSEP ck
        if (k in rec) {
            dup[FILENAME] = dup[FILENAME] " " ck
        } else {
            rec[k] = outcome
            if (outcome == "skipped") pendingKey = k
        }
    }
    next
}

/^[ \t]/ {
    if (pendingKey != "") { reason[pendingKey] = trim($0); pendingKey = "" }
    next
}

END {
    bad = 0
    for (fi = 1; fi <= fo; fi++) {
        f = order[fi]
        name = (f in labelOf) ? labelOf[f] : f
        fileErrors = 0
        checked = 0
        # An input with no recognizable results (empty, malformed, unparseable, ANSI-contaminated)
        # must fail loudly instead of passing with zero cases.
        if (recognized[f] == 0) {
            printf "  NO RESULTS        %s\n", name
            fileErrors++
        }
        for (i = 1; i <= en; i++) {
            ck = expKeys[i]
            k = f SUBSEP ck
            eo = expOutcome[ck]
            er = expReason[ck]
            if (!(k in rec)) {
                printf "  MISSING           %s\n", ck
                fileErrors++
            } else if (rec[k] == "failed") {
                printf "  UNEXPECTED FAILURE %s\n", ck
                fileErrors++
            } else if (eo == "passed") {
                if (rec[k] == "passed") { checked++ }
                else if (rec[k] == "skipped") { printf "  UNEXPECTED SKIP   %s (reason=%s)\n", ck, (k in reason ? reason[k] : ""); fileErrors++ }
            } else {
                if (rec[k] == "passed") { printf "  UNEXPECTED RUN    %s (expected skip reason=%s)\n", ck, er; fileErrors++ }
                else if (rec[k] == "skipped") {
                    r = (k in reason) ? reason[k] : ""
                    if (r != er) { printf "  WRONG REASON      %s expected=%s actual=%s\n", ck, er, r; fileErrors++ }
                    else { checked++ }
                }
            }
        }
        # Any matrix case present that is not in the expected table.
        for (k in rec) {
            p = index(k, SUBSEP)
            if (substr(k, 1, p - 1) != f) continue
            extra = substr(k, p + 1)
            if (!(extra in expOutcome)) { printf "  UNEXPECTED CASE   %s\n", extra; fileErrors++ }
        }
        if (f in dup) { printf "  DUPLICATE CASES  %s\n", dup[f]; fileErrors++ }

        if (fileErrors == 0) {
            printf "PASS %-45s (%d/%d cases)\n", name, checked, en
        } else {
            printf "FAIL %-45s (%d error(s))\n", name, fileErrors
            bad = 1
        }
    }

    # Cross-run comparison: every supplied run must agree case-for-case.
    if (fo > 1) {
        for (a = 1; a < fo; a++) {
            for (b = a + 1; b <= fo; b++) {
                fa = order[a]; fb = order[b]
                na = (fa in labelOf) ? labelOf[fa] : fa
                nb = (fb in labelOf) ? labelOf[fb] : fb
                mismatches = 0
                for (i = 1; i <= en; i++) {
                    ck = expKeys[i]
                    ka = fa SUBSEP ck; kb = fb SUBSEP ck
                    oa = (ka in rec) ? rec[ka] : "<missing>"
                    ob = (kb in rec) ? rec[kb] : "<missing>"
                    ra = (ka in reason) ? reason[ka] : ""
                    rb = (kb in reason) ? reason[kb] : ""
                    if (oa != ob || (oa == "skipped" && ra != rb)) {
                        printf "  MISMATCH          %s : %s(%s/%s) vs %s(%s/%s)\n", ck, na, oa, ra, nb, ob, rb
                        mismatches++
                    }
                }
                if (mismatches == 0) {
                    printf "MATCH %s <-> %s (%d cases)\n", na, nb, en
                } else {
                    printf "MISMATCH %s <-> %s (%d differing case(s))\n", na, nb, mismatches
                    bad = 1
                }
            }
        }
    }

    # A command that exited nonzero (including one that failed to launch) is never a pass.
    if (RUNRC != "" && RUNRC + 0 != 0) {
        printf "  COMMAND FAILED    runner exited %s\n", RUNRC
        bad = 1
    }

    if (bad) { printf "RESULT: FAIL\n"; exit 1 }
    printf "RESULT: PASS\n"
    exit 0
}
' "${result_files[@]}"
