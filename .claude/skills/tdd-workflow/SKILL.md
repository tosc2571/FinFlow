---
name: tdd-workflow
description: Use when implementing a new functional requirement, bug fix, or GitHub issue in FinFlow. Encodes the project's think-first, test-first, surgical-diff workflow (see the coding-guidelines and language-conventions skills) — clarify the requirement, write a failing test, implement the minimum to pass it, then verify with build-test before calling the work done. Skip only for genuinely trivial edits.
---

# Requirement → test → implementation

1. **Clarify.** Restate the requirement in one or two sentences. If more
   than one reasonable interpretation exists, list them and ask which one —
   don't silently pick. Name anything confusing before writing code. See the
   `coding-guidelines` skill for the full think-first/simplicity/surgical-diff
   discipline, and `language-conventions` for the German/English split.
2. **Locate the right layer.** Check [README.md](../../../README.md)
   (Architecture, and "Adding a new bank parser" if the change touches
   parsing) before assuming a new abstraction or dependency is needed.
3. **Red.** Write a failing test in `backend/FinFlow.Tests`, mirroring the
   namespace under test. Use `CsvParserTestBase.CreateTempCsv` to write CSV
   fixture strings to temp files instead of relying on real bank data.
4. **Green.** Write the minimum code to make it pass. No speculative
   parameters, no unrequested configurability, no error handling for cases
   that can't occur.
5. **Verify.** Run the `build-test` skill (`dotnet build` + `dotnet test`).
   Both must be clean before reporting the task done.
6. **Diff check.** Every changed line should trace to the requirement.
   Don't touch adjacent code, comments, or formatting that wasn't part of
   the request. If you spot unrelated dead code, mention it instead of
   deleting it.
