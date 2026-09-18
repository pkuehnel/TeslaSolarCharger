# System Instructions & Workflow

## 1. Zero-Assumption Policy & Execution Threshold
Before writing any implementation code or proposing a solution, you must critically evaluate the request for completeness and clarity. 

* **If EVERYTHING is 100% clear:** You may proceed with the full implementation immediately.
* **If ANYTHING is unclear, ambiguous, or missing:** You must NOT implement the code. You are strictly forbidden from making assumptions about requirements, architecture, dependencies, or edge cases. Instead, pause your execution and ask a clear, numbered list of clarifying questions. 

## 2. Unit Testing Mandate
Whenever you write, refactor, or modify implementation code, you must automatically provide the corresponding unit tests. Tests should be comprehensive, covering the happy path, boundary conditions, and expected exceptions or failures. Do not wait for a prompt to write tests; consider them an inseparable part of the code delivery.

## 3. Strict DRY Policy & Code Reusability
Before creating new methods, classes, or components, you must thoroughly analyze the existing codebase. You are required to keep code duplication to an absolute minimum. Always prioritize reusing existing methods. If an existing method nearly meets the new requirement, you must refactor or slightly modify it (e.g., by adding optional parameters, utilizing generics, or extending its logic) to accommodate the broader use cases, rather than writing a new, duplicate, or highly similar method.

## 4. Blazor-First & Minimal JavaScript Policy
When developing or modifying this application, you must attempt to implement all functionality natively within Blazor using C#. You are required to keep the use of custom JavaScript as low as absolutely possible. Only resort to JavaScript interop as a strict last resort when native Blazor solutions do not exist or are fundamentally insufficient. **When JavaScript is required, it must exclusively be called through the `JavaScriptWrapper` class.** You are forbidden from injecting or calling `IJSRuntime` directly from standard components or other services.