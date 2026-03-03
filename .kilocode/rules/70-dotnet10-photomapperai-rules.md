# 70-dotnet10-photomapperai-rules.md

## Purpose and scope

This rule file is for AI coding agents (Kilo/OpenClaw/Cline-style) working in **PhotoMapperAI**.
It defines practical coding rules for **.NET 10** (formerly .NET Core), **C#**, **CLI + Avalonia UI**, **CommunityToolkit.Mvvm**, **xUnit**, and general quality/security expectations.

Use this file as the **high-level .NET + project workflow rule set**.
Keep project-specific details in `PhotoMapperAI.md` and specialized topics in other files (e.g. `30-computer-vision.md`, `40-testing.md`, `60-avalonia-mvvm.md`).

---

## Project snapshot (for agent context)

- Solution contains:
  - `src/PhotoMapperAI` (CLI/core app)
  - `src/PhotoMapperAI.UI` (Avalonia desktop UI)
  - `tests/PhotoMapperAI.Tests` (xUnit tests)
- Target framework: **`net10.0`** (CLI, UI, tests)
- UI stack:
  - Avalonia 11.x
  - CommunityToolkit.Mvvm
  - ViewLocator pattern
  - compiled bindings enabled (`<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`)
- Core/libs in repo include CsvHelper, CommandLineUtils, OllamaSharp, OpenCvSharp, ImageSharp

---

## Agent operating mode (important)

When making changes in this repo:

1. **Prefer small, verifiable slices** (one behavior/fix per change).
2. **Do not refactor unrelated code** while fixing a bug/feature.
3. **Preserve CLI behavior and parameter compatibility** unless explicitly asked.
4. **Respect cross-platform behavior** (macOS + Windows + Linux paths/builds).
5. **Keep UI responsive** (no blocking calls on UI thread).
6. **Add/adjust tests when changing logic** in parsers, matching, mapping, crop calculations, or validation.
7. **Do not commit secrets, real player data, or private DB connection strings**.

---

## .NET 10 baseline rules (general)

### 1) Use modern SDK-style project defaults consistently

- Target **`net10.0`** unless there is a clear reason to multi-target.
- Keep `Nullable` enabled.
- Keep `ImplicitUsings` enabled unless a file/project has a strong reason to disable.
- Prefer SDK/project settings and `.editorconfig` over ad-hoc IDE-only formatting preferences.

### 2) Enable/enforce analyzers and code style at build time

If not already configured centrally, prefer these properties in project or `Directory.Build.props`:

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisMode>Recommended</AnalysisMode>
  <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
</PropertyGroup>
```

Notes:
- For mature codebases, enable warning-as-error **gradually** (CI first for selected warnings, or new code only).
- Put style and analyzer severities in `.editorconfig`.

### 3) Prefer deterministic, repeatable CLI commands

Use `dotnet` CLI commands in docs/scripts/agent actions:

```bash
dotnet restore
dotnet build
dotnet test
```

For coverage (when needed):

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## C# coding rules (complements `10-csharp-style.md`)

### 1) Readability first

- Prefer explicit, boring, maintainable code over clever code.
- Use short methods with clear names.
- Keep business rules in services, not in command handlers or views.
- Prefer early returns to reduce nesting.

### 2) Naming

- `PascalCase`: public types/members, methods, properties
- `_camelCase`: private fields
- `camelCase`: locals/parameters
- Interface names start with `I`
- Use domain names that reflect repo concepts (`PlayerRecord`, `PhotoMetadata`, `MappingResult`, etc.)

### 3) Nullability discipline

- Model nullability intentionally (`string` vs `string?`).
- Do not use `!` (null-forgiving operator) casually.
- Validate external input at boundaries (CSV rows, file names, JSON config, command args, API responses).

### 4) Exceptions

- Throw exceptions for **programming/configuration errors**.
- Return result objects / validation messages for **expected domain failures** (e.g., unmatched photo, parse issue).
- Preserve stack traces (`throw;`, not `throw ex;`).

---

## Architecture and layering rules

### 1) Keep layers clean

- **CLI/UI** layers orchestrate; **services** implement business behavior.
- Models used by core logic should remain UI-agnostic.
- Avoid duplicating logic in both CLI command handlers and ViewModels.
- If UI and CLI share logic, move it into core services or shared abstractions.

### 2) Dependency direction

Allowed direction (preferred):

- `UI/CLI -> Services -> Models/Utilities`
- `Tests -> any production layer`

Avoid:

- Core services depending on Avalonia UI types
- ViewModels doing file/image/database logic directly
- Static service locators and hidden global state

### 3) Introduce interfaces only when useful

Use interfaces for:
- external dependencies (LLM provider, file system abstraction when needed, face detection services)
- testing seams
- multiple implementations already exist or are planned

Avoid interface explosion for tiny one-off classes with no abstraction value.

---

## Configuration, DI, and app startup rules (.NET Extensions)

### 1) Prefer Generic Host patterns for non-trivial apps/services

For CLI and services with logging/configuration/DI, prefer `HostApplicationBuilder` / Generic Host patterns.

Benefits:
- consistent configuration loading
- DI composition root
- structured logging
- easier testing and future background services

### 2) Strongly typed configuration (Options pattern)

- Bind config sections to POCO options classes.
- Validate options at startup for required settings/paths.
- Keep config sections focused (e.g., `Ollama`, `OpenCv`, `Cropping`, `Paths`, `Logging`).
- Do not pass raw `IConfiguration` deep into business services unless absolutely necessary.

### 3) DI registration rules

- Register concrete services in one composition root.
- Prefer constructor injection.
- Avoid resolving services manually with `IServiceProvider` except composition/infrastructure glue code.
- Avoid static access to service provider.

### 4) Lifetime guidance (practical)

- `Singleton`: stateless utilities, caches, configuration readers, factories (thread-safe only)
- `Scoped`: request/operation scope (mostly web apps, less common here)
- `Transient`: lightweight services with no shared state

For desktop/CLI apps, be explicit and keep lifetime choices simple.

---

## Logging and diagnostics rules

### 1) Structured logging (not string soup)

Prefer structured templates:

```csharp
_logger.LogInformation("Mapped {MappedCount} of {TotalCount} players for team {TeamId}", mapped, total, teamId);
```

Avoid:

```csharp
_logger.LogInformation($"Mapped {mapped} of {total} players for team {teamId}");
```

Reason: structured logging preserves named fields for filtering and analysis.

### 2) Log levels

- `Trace`: noisy internals / debug loops
- `Debug`: developer troubleshooting
- `Information`: normal progress milestones
- `Warning`: recoverable problems / fallbacks
- `Error`: operation failed, but app continues
- `Critical`: app cannot continue / corrupted state / startup failure

### 3) Sensitive data

Never log:
- secrets / API keys
- connection strings
- raw personal data if avoidable
- full file system paths from users if not necessary (truncate or sanitize in user-facing logs)

---

## Async, threading, cancellation, and progress rules

### 1) Async all the way (where applicable)

- Prefer `async/await` for I/O-bound operations (HTTP, file I/O, process calls).
- Do not block on tasks with `.Result` / `.Wait()` in UI or request-like flows.
- Avoid mixing sync and async code paths unnecessarily.

### 2) Cancellation support

- Accept `CancellationToken` in long-running operations.
- Pass the token to cancellable APIs (`Task.Delay`, HTTP calls, async loops, etc.).
- Check cancellation inside loops / batch operations at sensible checkpoints.
- Treat cancellation as a **normal control path**, not an error.

### 3) UI thread safety (Avalonia)

- Do not perform heavy CPU or blocking I/O on the UI thread.
- Update bindable state from the correct context.
- Maintain explicit loading/progress state in ViewModels (e.g., `IsBusy`, `ProgressValue`, `StatusText`).
- Commands that run work should disable/re-enable appropriately (via `CanExecute` or busy flags).

### 4) Progress reporting

For long operations (mapping / detection / batch runs):
- expose progress percentage and current step
- surface recoverable errors in a user-friendly way
- support cancellation and partial results where possible

---

## HTTP/API client rules (LLM and external providers)

Even if current providers are wrapped by libraries, follow these rules for any direct HTTP work:

### 1) Reuse HTTP infrastructure correctly

- Prefer `IHttpClientFactory` for configurable/reusable HTTP clients when using .NET Extensions DI.
- Avoid creating/discarding new `HttpClient` per request in loops (risk of socket exhaustion).
- Prefer typed clients for provider-specific APIs.

### 2) Timeouts, retries, resilience

- Set sensible timeouts per operation type (name matching vs vision calls can differ).
- Retry only transient failures (network hiccups, 429/5xx where safe).
- Do not retry non-idempotent operations blindly.
- Log retries at `Warning`/`Debug` with attempt count and reason.

### 3) Request/response boundaries

- Validate and normalize outbound payloads.
- Parse responses defensively (nulls, missing fields, invalid JSON).
- Avoid leaking provider-specific response formats deep into domain logic (map to internal models).

---

## File system, paths, and cross-platform rules

### 1) Paths

- Use `Path.Combine`, `Path.GetExtension`, `Path.GetFileName`, etc.
- Never hardcode path separators.
- Normalize/validate user-provided paths before processing.

### 2) File I/O safety

- Check file existence before heavy processing.
- Fail fast with clear messages for missing config/model files.
- Prefer streaming for large files when practical.
- Clean up temp files deterministically.

### 3) Output behavior

- Do not overwrite user outputs silently unless explicitly requested.
- Make output naming deterministic and documented.
- When generating batch outputs, keep folder structure predictable.

---

## CLI command rules (PhotoMapperAI core)

### 1) Command handlers should orchestrate, not contain business logic

Command classes should:
- parse/validate arguments
- resolve dependencies/services
- call service methods
- format output/errors for console

Move reusable logic to services.

### 2) Exit codes and messages

- Return non-zero exit code on fatal failure.
- Use concise user-facing messages with enough detail to fix the issue.
- Separate verbose debug logging from normal console output.

### 3) Deterministic outputs

For CSV mapping/generation flows:
- preserve stable column names and order unless versioned change is intended
- document any schema changes in changelog/release notes

---

## Avalonia + MVVM + CommunityToolkit.Mvvm rules (repo UI)

> This section complements `60-avalonia-mvvm.md` and focuses on cross-cutting rules.

### 1) ViewModel design

- Prefer `partial` ViewModels with CommunityToolkit source generators.
- Use `[ObservableProperty]` for bindable state when it improves readability.
- Use `[RelayCommand]` for commands; keep command methods focused.
- Keep ViewModels free of Avalonia view/control references when possible.

Example shape:

```csharp
public partial class MapStepViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? statusText;

    [RelayCommand]
    private async Task RunMappingAsync(CancellationToken cancellationToken)
    {
        // orchestrate service call, update progress state
    }
}
```

### 2) AXAML binding rules

- Prefer bindings over code-behind for UI state.
- Use compiled bindings where practical (already enabled in project).
- Keep converters small and reusable; avoid converter logic that belongs in ViewModels.
- Extract repeated styles/templates into shared resources.

### 3) Navigation / view resolution

- Keep `ViewLocator` simple and predictable.
- Prefer convention-based ViewModel -> View mapping unless a custom map is truly needed.
- Avoid hidden navigation side effects in constructors.

### 4) Code-behind usage

Allowed in code-behind for:
- view-only behavior
- platform-specific UI glue
- control initialization not suitable for VM

Avoid in code-behind:
- business logic
- file parsing / image processing / API calls
- application workflow orchestration

---

## Computer vision and image processing rules (practical)

### 1) Resource management

- Dispose native/image resources correctly (`IDisposable`) to avoid leaks, especially in loops.
- Be careful with OpenCV/native resources and cross-platform runtime libraries.
- Avoid retaining large image buffers longer than necessary.

### 2) Deterministic processing pipeline

When changing crop/detection logic:
- preserve fallback order unless explicitly redesigning
- document behavioral changes (especially crop dimensions and eye-position heuristics)
- add/update tests for edge cases and regressions

### 3) Benchmarking and validation

- Separate correctness checks from benchmark code.
- Never optimize based on intuition only—measure representative samples.

---

## Testing rules (xUnit + coverage)

### 1) What to test first

Prioritize tests for:
- filename parsing / manifest parsing
- mapping heuristics and threshold logic
- fallback selection behavior
- crop dimension calculations
- configuration validation
- error handling for missing files / invalid inputs

### 2) Test naming and structure

Use clear names that describe behavior and expected outcome, e.g.:
- `ParseFilename_ReturnsMetadata_WhenPatternMatches`
- `MapPlayers_MarksAmbiguousMatches_AsInvalid`
- `GeneratePortrait_UsesCenterCrop_WhenNoFaceDetected`

Prefer Arrange-Act-Assert readability.

### 3) Unit vs integration tests

- Unit test pure logic and deterministic services.
- Integration tests may use local test assets/configs but should avoid external paid APIs by default.
- Mock or fake LLM/vision providers whenever practical.

### 4) Coverage expectations

Coverage is a tool, not the goal.
- Focus on critical logic paths and regression prevention.
- Use coverage reports to identify blind spots, not to chase vanity percentages.

---

## Security and privacy rules (must-follow)

### 1) Secrets and credentials

- Never commit API keys, tokens, connection strings, or real credentials.
- Use templates (`appsettings.template.json`, test config templates) and environment variables where possible.
- Redact secrets in logs and screenshots.

### 2) Personal/sensitive data

- Do not commit real player data or private datasets.
- Use synthetic/test data for automated tests.
- Use publicly available or explicitly permitted photos for validation datasets.

### 3) Input validation

Treat all external inputs as untrusted:
- CSV files
- SQL query files
- file names
- manifests
- API responses
- user-selected paths

Validate early, fail clearly.

---

## Performance rules (practical for this repo)

- Avoid repeated full-directory scans inside inner loops.
- Cache parsed metadata and reusable configuration.
- Be explicit about CPU-bound vs I/O-bound work.
- Throttle/limit parallelism when native libs or provider APIs are involved.
- Measure before introducing complex optimizations.

For UI:
- batch UI updates for large lists/progress events when needed
- avoid flooding UI with per-item notifications if thousands of rows are processed

---

## Documentation and change management rules

### 1) When code changes require docs updates

Update docs/notes when changing:
- CLI parameters or defaults
- CSV schema/columns
- crop behavior/output dimensions
- model/provider selection rules
- config file structure
- publishing/build steps

### 2) Changelog discipline

For user-visible behavior changes:
- add short changelog/release note entry
- mention migration impact if output format changed

---

## Agent verification checklist (run before claiming done)

Choose the minimum set relevant to your change. Prefer exact commands in the repo root.

### Fast checks (always)

```bash
dotnet build
```

### Unit tests (logic changes)

```bash
dotnet test
```

### Coverage (when requested / major logic changes)

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### UI compile check (UI changes)

```bash
dotnet build src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj
```

### CLI compile check (CLI/core changes)

```bash
dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj
```

### Manual smoke tests (when touching workflow behavior)

- Run representative command(s) with sample/test inputs.
- Verify no regression in output files / console logs.
- For UI changes, open the affected view and validate bindings/commands/disabled states.

---

## Do / Don't summary

### Do

- Write small, verifiable changes
- Keep UI responsive and cancellation-aware
- Use DI + configuration + logging patterns consistently
- Validate external inputs at boundaries
- Add tests for changed logic
- Preserve cross-platform behavior
- Keep rules and docs concise and actionable

### Don’t

- Put business logic in views or code-behind
- Block on async tasks in UI flows
- Create `HttpClient` per request in loops
- Use static service locators
- Log secrets or commit real data
- Refactor unrelated files “while you’re there”
- Claim success without a build/test verification step

---

## Suggested companion files in `.kilocode/rules/`

If you want a stronger rule set, add these (or generate them):

- `61-avalonia-dialogs-and-filepickers.md`
- `62-avalonia-performance-large-lists.md`
- `63-async-cancellation-patterns.md`
- `64-http-clients-and-provider-retries.md`
- `65-editorconfig-and-analyzers.md`
- `66-testing-fixtures-and-test-data.md`
- `67-security-secrets-and-redaction.md`

---

## Curated links (official / high quality)

### .NET 10 and C#

- .NET 10 overview (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview
- C# coding conventions (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
- C# identifier naming conventions (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
- .NET code style rule options / EditorConfig (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options
- .NET code analysis overview (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview
- Configure code analysis rules (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options
- Nullable reference types (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references
- MSBuild props for .NET SDK projects (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props

### Host / DI / config / logging / HTTP

- .NET Generic Host (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host
- Dependency injection overview (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview
- DI guidelines (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines
- Configuration in .NET (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration
- Options pattern in .NET (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/extensions/options
- Logging overview in .NET (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/overview
- Logging guidance for library authors (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/library-guidance
- IHttpClientFactory (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory
- HttpClient guidelines (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines

### Async / cancellation

- Cancellation in managed threads (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads
- Task cancellation (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation
- CancellationToken API (net10) (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken?view=net-10.0

### Avalonia / MVVM / AXAML / ViewLocator

- Avalonia data binding docs: https://docs.avaloniaui.net/docs/basics/data/data-binding/
- Avalonia compiled bindings docs: https://docs.avaloniaui.net/docs/basics/data/data-binding/compiled-bindings
- Avalonia View Locator docs: https://docs.avaloniaui.net/docs/concepts/view-locator
- Avalonia samples (MVVM / converters examples): https://docs.avaloniaui.net/docs/tutorials/samples

### CommunityToolkit.Mvvm

- ObservableObject (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observableobject
- ObservableProperty attribute (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty
- RelayCommand attribute (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand

### Testing / coverage

- xUnit home: https://xunit.net/
- xUnit getting started (v2): https://xunit.net/docs/getting-started/v2/getting-started
- xUnit getting started (v3, .NET SDK): https://xunit.net/docs/getting-started/netcore/cmdline
- Microsoft Learn: Unit testing C# in .NET with xUnit: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit
- Microsoft Learn: Code coverage with Coverlet: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage
- Coverlet repo (collector docs): https://github.com/coverlet-coverage/coverlet

### Security

- OWASP Cheat Sheet Series (official repo): https://github.com/OWASP/CheatSheetSeries
- OWASP Cheat Sheet Series (site): https://cheatsheetseries.owasp.org/

### .NET runtime team style references (great source material for rules)

- dotnet/runtime C# coding style: https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md
- dotnet/runtime project/build guidelines: https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/project-guidelines.md

---

## Optional future refinement for this repo

If you want a stricter rule system for agents, add a separate file with:

- exact approved commands for build/test/publish
- exact sample input paths (local only, gitignored)
- retry policy for flaky provider calls
- “ask-human” triggers (e.g., CSV schema changes, output format changes, package upgrades, UI workflow redesign)
- mandatory screenshots for UI changes (before/after)

