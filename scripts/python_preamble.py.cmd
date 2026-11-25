1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0.. & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# This is a polyglot Batch/Python script: cmd.exe executes it as a batch file,
# but everything after the 3-line header is Python. To create a new one, copy
# the first three lines of this file and start your Python code on line 4.
#
# How the header reads to each interpreter:
#
#   cmd.exe
#     Line 1 ends with `^` (batch line continuation), so cmd joins lines 1-2
#     into one harmless no-op and moves on. Line 3 is the real batch payload:
#     `@echo off`, cd to the repo root, run THIS file (`%~dpf0` = its own
#     absolute path) with the repo's uv-managed Python, forwarding all
#     arguments (`%*`), then `exit /b` stops batch parsing before the
#     trailing `::'''` (a batch comment). cmd never reads the Python body.
#
#   Python
#     Line 1 parses as the expression `1>2` followed by a `#` comment (the
#     trailing `^` is inside the comment). Lines 2-3 form a triple-quoted
#     string that swallows the batch payload; the `'''` inside line 3's
#     batch comment closes it. Everything after line 3 is ordinary Python.
#
# Conventions and rules:
#
#   - Name polyglot files `*.py.cmd`. The `.cmd` suffix is what makes
#     Windows/cmd/MSBuild execute them; the `.py` infix marks them as Python
#     for humans, editors, and formatters.
#   - `cd %~dp0..` assumes the script lives one level below the repo root
#     (scripts/, ...). For scripts in the repo root use
#     `cd %~dp0` instead. Either way the Python process starts at the repo
#     root, so use repo-root-relative paths in the Python code.
#   - uv (bin\uv.exe, committed to the repo) picks the interpreter and
#     resolves packages from pyproject.toml / uv.lock; it does not care that
#     the file extension is .cmd rather than .py.
#   - Arguments pass through unchanged: `foo.py.cmd a b` -> `sys.argv[1:] ==
#     ["a", "b"]`.
#   - Call other batch scripts by explicit path (e.g. `call "%~dp0..\x.bat"`),
#     never by bare name: hardened environments set
#     NoDefaultCurrentDirectoryInExePath, which disables current-directory
#     command lookup in cmd.
#   - Known limitation: the Python exit code propagates correctly to cmd
#     callers (`call foo.py.cmd || exit /b 1` works, MSBuild PreBuildEvents
#     fail properly), but is NOT reliably visible to Git Bash / POSIX shells
#     invoking the .cmd directly — bash typically sees 0. cmd cannot forward
#     a chained command's errorlevel as the process exit code from a
#     single-line header (delayed expansion is parse-time; `exit /b` in an
#     `&` chain doesn't set the process code). If a shell script needs the
#     real exit code, run the Python directly: `bin/uv.exe run python
#     foo.py.cmd`.
#
# Existing polyglots: cef.py.cmd, fix_project_uuids.py.cmd,
# update_wk_solution.py.cmd (repo root); ensure_kagi_gcs_auth.py.cmd,
# vcpkg_tools_extract.py.cmd, wk_local_build.py.cmd (scripts/, the last one
# with an extended header that also clears VS env vars and calls vsenv.bat).
