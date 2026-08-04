---
name: build-test
description: Build the FinFlow solution and run its xUnit test suite, then report a concise pass/fail summary. Use whenever a code change in backend/ needs to be verified, before reporting a task complete, or when the user asks to "build", "test", or "run the tests".
---

# Build & Test FinFlow

1. Run `dotnet build` from the repo root. If it fails, stop and report the
   compiler errors — do not run tests against a broken build.
2. Run `dotnet test` from the repo root.
3. Summarize the result in a few lines:
   - Build: OK / failed (+ error count if failed)
   - Tests: passed/total, and the name of every failing test with its
     assertion message
4. If tests fail, do not attempt a fix unless asked — report first.

Notes specific to this repo:
- Solution layout: `backend/FinFlow.Core` (library — parsers, export),
  `backend/FinFlow.Api` (ASP.NET Core + EF Core, references Core),
  `backend/FinFlow.Tests` (xUnit, references Core and Api).
- No network calls or external services are involved — all parsing/export
  is local file I/O, so there's no fake-handler pattern to worry about.
- Run `dotnet test --filter "ClassName=FinFlow.Tests.HvbCsvParserTests"` to
  target a single test class, or `--filter "MethodName~CanParse"` for a
  subset, when iterating on one area.
