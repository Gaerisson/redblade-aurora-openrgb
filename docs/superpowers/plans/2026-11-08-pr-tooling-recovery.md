# PR tooling recovery implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task by task.

**Goal:** Apply PRs #1 and #2, preserving their authorship, and close the Windows recovery test gap.

**Architecture:** Keep the existing scripts and firmware interfaces. Execute the real PowerShell configuration script against temporary JSON files, replacing only process/service/Python boundaries. Exercise the benchmark's actual main try/finally block without requesting elevation or accessing a keyboard.

**Tech stack:** PowerShell 7, Python unittest, existing firmware host suites and GitHub Actions.

**Spec:** [PR #1](https://github.com/Gaerisson/redblade-aurora-openrgb/pull/1), [PR #2](https://github.com/Gaerisson/redblade-aurora-openrgb/pull/2), and the owner's request to review/apply both.

## Global constraints

- No firmware or BIN changes and no hardware writes are needed.
- Full minimum firmware remains 0.7.0; Core remains 0.8.0.
- Start OpenRGB before Aurora; preserve the original failure if rollback or restart also fails.
- Restore the previous service/application state on a failed configuration.
- All tests use private temporary configuration; never stop installed RGB applications.

## Review focus

- Failure before any config write: restart the previous apps without altering files.
- Failure after the first write: restore all three original config files.
- Failure during restart: recovery remains enabled until the entire restart succeeds.
- A rollback copy failure: continue restoring other files and restarting apps; rethrow the first error.
- Benchmark standalone server on a custom port: wait for that SDK before restarting Aurora.

### Task 1: Installer edition message

- [x] Add a mocked-device regression for Core 0.7.0 rejection and Full 0.7.0 acceptance in `tests/test_tools.py`; run it RED against main.
- [x] Apply the exact `tools/install-fn-layer.py` patch from PR #2; run all Python tests GREEN.

### Task 2: Windows recovery

- [x] Add `tests/windows_tools_test.ps1`, exercising the real script with temporary configs and simulated process/service boundaries. Observe the original defects RED.
- [x] Apply PR #1 to `tools/configure-windows.ps1` and `tools/measure-performance.ps1`; distinguish fixed cases from any remaining failed recovery cases.
- [x] Complete the minimal recovery fixes for failed restart and rollback-copy errors. The independent review also required original command/port retention and nonterminating recovery warnings; both additional regressions were RED then GREEN. Windows suite: 9/9.
- [x] Add the PowerShell test command to `.github/workflows/test.yml` so these scenarios are checked on every push/PR.

### Task 3: Integrate and verify

- [x] Run the Python, PowerShell and existing C suites; review the complete diff and verify BINs are unchanged.
- [ ] Merge both original PRs with expected-head guards, then publish any tested supplemental fixes/tests on main.
- [ ] Verify both PRs are merged, the final main commit has successful CI, and the installed RGB service remains running.
