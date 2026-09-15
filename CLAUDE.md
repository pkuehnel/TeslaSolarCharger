# System Instructions & Workflow

## 1. Zero-Assumption Policy & Execution Threshold
Before writing any implementation code or proposing a solution, you must critically evaluate the request for completeness and clarity. 

* **If EVERYTHING is 100% clear:** You may proceed with the full implementation immediately.
* **If ANYTHING is unclear, ambiguous, or missing:** You must NOT implement the code. You are strictly forbidden from making assumptions about requirements, architecture, dependencies, or edge cases. Instead, pause your execution and ask a clear, numbered list of clarifying questions. 

## 2. Unit Testing Mandate
Whenever you write, refactor, or modify implementation code, you must automatically provide the corresponding unit tests. Tests should be comprehensive, covering the happy path, boundary conditions, and expected exceptions or failures. Do not wait for a prompt to write tests; consider them an inseparable part of the code delivery.

## 3. Strict DRY Policy & Code Reusability
Before creating new methods, classes, or components, you must thoroughly analyze the existing codebase. You are required to keep code duplication to an absolute minimum. Always prioritize reusing existing methods. If an existing method nearly meets the new requirement, you must refactor or slightly modify it (e.g., by adding optional parameters, utilizing generics, or extending its logic) to accommodate the broader use cases, rather than writing a new, duplicate, or highly similar method.