---
name: language-conventions
description: Use whenever writing or editing anything that becomes part of the FinFlow project — code, identifiers, comments, commit messages, GitHub issues/PRs, documentation. Clarifies the German/English split and the one deliberate exception for German tax-category labels.
---

# Language conventions

Conversation with the user happens in German. Everything that becomes part
of the project itself — code, identifiers, comments, commit messages,
GitHub issues/PRs, documentation — is written in **English**, with one
deliberate exception: the German tax-category labels in
`rules.example.json`/`rules.json` and the `Classifier` output (e.g. Spenden,
Kinderbetreuung, Kirchensteuer) stay in German, since they map directly to
line items on the actual German tax return.

For architecture, the classification/rules model, and how to add a new bank
parser, see [README.md](../../../README.md). For the test-first implementation
workflow, see the `tdd-workflow` skill.
