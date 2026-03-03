# 60-avalonia-mvvm.md

Purpose: project rules for Avalonia UI + MVVM using `CommunityToolkit.Mvvm` in Kilo/OpenClaw-style coding agents.

Scope: applies to UI code in `Views/`, `ViewModels/`, `Models/`, `Converters/`, plus UI navigation and async UI interactions.

---

## 1) Architecture and project structure

Use a clean MVVM structure and keep responsibilities strict:

- `Models/`
  - Domain/data DTOs and state objects.
  - No UI references.
  - Prefer POCOs unless the model itself must be observable.

- `ViewModels/`
  - UI state + commands.
  - No direct control manipulation.
  - Talks to services/interfaces, not concrete UI widgets.

- `Views/`
  - `.axaml` + minimal code-behind (`.axaml.cs`).
  - UI composition, bindings, styles/classes, visual-only behaviors.
  - No business logic.

- `Converters/`
  - Small, deterministic, side-effect-free converters.
  - Prefer converters only when binding cannot be expressed clearly in the ViewModel.

- `Services/` (recommended even if not listed yet)
  - File access, dialogs, APIs, persistence, image processing orchestration, etc.
  - Inject into ViewModels through interfaces.

### Naming conventions

- Views end with `View` (example: `MainView.axaml`, `PhotoReviewView.axaml`)
- ViewModels end with `ViewModel`
- Models end with `Model` only when it improves clarity (do not force suffixes everywhere)
- Converters end with `Converter`
- Command methods are verbs (example: `LoadImagesAsync`, `SaveMappingAsync`, `CancelScan`)

### Folder alignment

Keep View/ViewModel pairs easy to discover.

Example:

- `Views/MainView.axaml`
- `Views/MainView.axaml.cs`
- `ViewModels/MainViewModel.cs`

---

## 2) MVVM with CommunityToolkit.Mvvm

Use `CommunityToolkit.Mvvm` source generators to reduce boilerplate and keep ViewModels consistent.

### Base ViewModel pattern

Prefer inheriting from `ObservableObject` (or a project-specific base class deriving from it).

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoMapperAI.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
}
```

### `[ObservableProperty]` rules

Use `[ObservableProperty]` for mutable UI state.

- ViewModel classes using source generators must be `partial`.
- Annotate fields, let the toolkit generate the property.
- Access the generated property in code (`SelectedPhoto`), not the backing field (`selectedPhoto`) except during initialization.
- Keep field names private and simple.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoMapperAI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private double progressValue;
}
```

### Property change hooks (generated partial methods)

Use generated `On<Property>Changed` partial methods for local reaction logic (filter refresh, dependent state, etc.).

- Keep hooks small.
- Do not put long-running work directly in a property-changed hook.
- If async work is needed, trigger a command or debounced workflow.

```csharp
public partial class MainViewModel
{
    partial void OnErrorMessageChanged(string? value)
    {
        HasError = !string.IsNullOrWhiteSpace(value);
    }
}
```

### `[RelayCommand]` rules

Use `[RelayCommand]` for user actions.

- Command methods should be private unless there is a specific reason.
- Use sync methods only for truly synchronous work.
- Use `async Task` methods for I/O, CPU orchestration, or anything that can take noticeable time.
- Avoid `async void` (except unavoidable framework event handlers in code-behind).

```csharp
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ViewModelBase
{
    [RelayCommand]
    private void ClearError()
    {
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task LoadImagesAsync()
    {
        // Call service, update state, handle exceptions
        await Task.CompletedTask;
    }
}
```

### CanExecute and command state

Use `CanExecute` to keep UI state and command availability aligned.

- Prefer a computed boolean property for command eligibility.
- Notify command availability when dependencies change.
- Avoid duplicating validation logic in both view and viewmodel.

```csharp
public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool hasPendingChanges;

    private bool CanSave() => HasPendingChanges && !IsLoading;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        await Task.CompletedTask;
        HasPendingChanges = false;
    }
}
```

### Async commands and cancellation

Prefer async commands for long-running operations.

Rules:

- Return `Task`, not `void`.
- Expose/calculate `IsLoading` to drive spinners and disable controls.
- Use cancellation tokens for scans/imports/network operations where cancel is useful.
- Do not start fire-and-forget tasks from commands unless you explicitly capture/log failures.

If a command can run long enough to need cancel/retry, model the UI state explicitly:

- `IsLoading`
- `ProgressValue` (0-100 or 0-1, choose one convention and keep it consistent)
- `StatusText`
- `ErrorMessage`
- `CanRetry` / `HasError`

### Validation (optional but recommended)

For forms and editable mappings, prefer ViewModel-driven validation (`INotifyDataErrorInfo`-compatible patterns) instead of ad-hoc UI-only checks.

Avalonia supports validation integration and commonly used MVVM libraries (including CommunityToolkit.Mvvm scenarios) in its validation pipeline.

---

## 3) AXAML views: bindings, templates, styles

Views should be declarative and binding-driven.

### General AXAML rules

- Prefer bindings over code-behind assignments.
- Keep code-behind minimal (view-only concerns such as focus, visual initialization, and platform-specific UI glue).
- Do not call services from code-behind unless it is purely visual/platform plumbing.

### Data binding rules

- Set `x:DataType` when using compiled bindings / for stronger tooling and type safety.
- Be explicit with binding mode when it matters (`Mode=TwoWay` for editable controls).
- Prefer binding commands instead of click handlers.
- Prefer `StringFormat` in XAML for presentation-only formatting.
- Keep business formatting/parsing rules in the ViewModel or converter.

Example:

```xml
<UserControl
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:PhotoMapperAI.ViewModels"
    x:Class="PhotoMapperAI.Views.MainView"
    x:DataType="vm:MainViewModel">

    <StackPanel Spacing="8">
        <TextBlock Text="{Binding StatusText}" />

        <ProgressBar IsVisible="{Binding IsLoading}"
                     Value="{Binding ProgressValue}" />

        <Button Content="Load Images"
                Command="{Binding LoadImagesCommand}" />
    </StackPanel>
</UserControl>
```

### Compiled bindings (recommended)

Prefer compiled bindings when available in the project setup.

- Set `x:DataType` on root views.
- Set `x:DataType` inside `DataTemplate` when needed.
- If the template binds to a different item type than the parent view, declare that template item type explicitly.

This improves:

- build-time binding checks
- refactor safety
- tooling support
- runtime performance/trimming friendliness (depending on scenario)

### Data templates

Use `DataTemplate` for rendering item ViewModels and polymorphic content.

Rules:

- Keep templates small and composable.
- Prefer dedicated item ViewModels for complex rows/cards.
- Avoid deeply nested templates that hide logic.
- Order templates from most specific to least specific when applicable.

### Styles and classes

Prefer reusable styles and classes over repeated local property setters.

Rules:

- Put shared styles in dedicated style files or app-level styles.
- Use style classes (`Classes="..."`) for semantic styling (example: `danger`, `muted`, `toolbar-button`).
- Keep style selectors readable; avoid overly clever selectors.
- Centralize spacing/font tokens if your app has a design system.

### Converters usage policy

Use converters sparingly.

Use a converter when:

- the transformation is purely presentational,
- reusable across multiple views,
- and would make the ViewModel less cohesive.

Do not use a converter when:

- the value is business state (belongs in ViewModel),
- the converter needs services/I/O,
- the converter contains app workflow decisions.

---

## 4) Navigation: ViewLocator pattern (and alternatives)

Avalonia templates often include a `ViewLocator`. It is useful, but optional.

### ViewLocator pattern rules

Use ViewLocator when:

- you display different ViewModels dynamically (shell content, content regions, dialogs/pages),
- you want convention-based View resolution (`FooViewModel` -> `FooView`).

Rules:

- Keep the naming convention consistent across the app.
- ViewLocator should resolve views only (no navigation state changes, no service calls).
- Log or provide a visible fallback for missing views in development.

Minimal example pattern:

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PhotoMapperAI.ViewModels;
using System;

namespace PhotoMapperAI;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        var viewModelType = data.GetType();
        var viewTypeName = viewModelType.FullName!.Replace("ViewModel", "View");
        var viewType = Type.GetType(viewTypeName);

        if (viewType is null)
            return new TextBlock { Text = $"View not found: {viewTypeName}" };

        return (Control)Activator.CreateInstance(viewType)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
```

### Navigation state rules

Keep navigation state in a ViewModel or navigation service, not in views.

Patterns that are acceptable:

- `MainViewModel.CurrentPage` (content region + `ContentControl` + ViewLocator/DataTemplates)
- `INavigationService` abstraction injected into ViewModels
- message-based navigation/event aggregation only if complexity requires it

Avoid:

- direct `Window`/`TopLevel` manipulation from child ViewModels
- storing navigation logic in converters
- cross-view direct references

---

## 5) Async patterns: loading states, progress, threading

Desktop UI apps fail most often from blocking the UI thread or poorly modeled async state. Be strict here.

### Loading state pattern (recommended baseline)

For any operation longer than ~150 ms, model loading explicitly:

- `IsLoading`
- `StatusText`
- `ErrorMessage`
- `ProgressValue` (optional)
- `CanCancel` / `CanRetry` (optional)

Example workflow:

1. Clear previous error
2. Set loading state
3. Execute operation in service layer
4. Update result state
5. Reset loading state in `finally`

```csharp
[RelayCommand]
private async Task ScanFolderAsync()
{
    if (IsLoading)
        return;

    IsLoading = true;
    ErrorMessage = null;
    StatusText = "Scanning folder...";

    try
    {
        await _photoScanService.ScanAsync();
        StatusText = "Scan completed";
    }
    catch (OperationCanceledException)
    {
        StatusText = "Scan canceled";
    }
    catch (Exception ex)
    {
        ErrorMessage = ex.Message;
        StatusText = "Scan failed";
    }
    finally
    {
        IsLoading = false;
    }
}
```

### Threading and UI thread access

Avalonia UI updates must happen on the UI thread.

Rules:

- Do CPU-intensive work off the UI thread.
- Use `Dispatcher.UIThread` when updating UI-bound state from background callbacks/threads.
- Prefer `InvokeAsync` when you need to await completion; use `Post` for fire-and-forget UI notifications.

```csharp
using Avalonia.Threading;

await Dispatcher.UIThread.InvokeAsync(() =>
{
    ProgressValue = 42;
    StatusText = "Processing...";
});
```

Avoid:

- `.Wait()` / `.Result` on tasks in UI code
- synchronous file/network calls from commands
- parallel writes to the same UI-bound collection without synchronization

### Collections and incremental updates

For lists shown in UI:

- Prefer `ObservableCollection<T>` (or a dedicated observable collection abstraction).
- Batch updates when possible to avoid UI thrash.
- For very large data sets, consider virtualization-friendly controls/patterns.

### Progress reporting

Use a clear convention:

- either `0..100` percentage
- or `0..1` normalized ratio

Document the chosen convention in the ViewModel and keep all views consistent.

### Cancellation and retries

For long-running import/scan/matching operations:

- Create a `CancellationTokenSource` per operation run
- Cancel previous run before starting a new mutually-exclusive run (if applicable)
- Dispose the old CTS safely
- Distinguish cancellation from errors in UI messages

---

## 6) Best practices: do and don't

### Do

- Do keep ViewModels `partial` when using Toolkit generators.
- Do prefer `[ObservableProperty]` and `[RelayCommand]` over manual boilerplate.
- Do keep AXAML declarative and command-driven.
- Do set `x:DataType` for stronger binding checks.
- Do model loading/error/progress explicitly.
- Do move file system/API/image processing logic to services.
- Do keep converters pure and lightweight.
- Do keep navigation logic centralized.
- Do write small, focused views and item templates.
- Do preserve responsiveness first; optimize later with measurements.

### Don't

- Don't put business logic in code-behind.
- Don't block the UI thread with `.Result`, `.Wait()`, or long loops.
- Don't manipulate controls directly from ViewModels.
- Don't hide workflow logic in converters or attached properties.
- Don't duplicate the same validation rules in multiple places.
- Don't swallow exceptions silently in async commands.
- Don't update UI-bound state from background threads without marshaling.
- Don't create god-ViewModels with unrelated responsibilities.

---

## 7) Kilo/agent implementation rules for this project

When the coding agent modifies Avalonia MVVM code:

### File targeting rules

If a feature touches UI, consider whether all of these must change:

- `ViewModels/...ViewModel.cs`
- `Views/...View.axaml`
- `Views/...View.axaml.cs` (only if view-only glue is needed)
- `Services/...` (for I/O/business workflow)
- `Converters/...` (only if truly presentational transformation is needed)

### Change discipline

- Preserve naming conventions (`FooViewModel` <-> `FooView`).
- Do not move business logic into AXAML/code-behind.
- Prefer incremental edits over large rewrites unless the user requested a refactor.
- If adding async commands, add loading/error handling in the same change.
- If adding a new bindable property, confirm the AXAML binding path is updated.

### Validation checklist after edits

Run what is available in the repo (adjust commands if the solution name differs):

```bash
dotnet restore
dotnet build
```

If tests exist:

```bash
dotnet test
```

If formatting/analyzers are configured:

```bash
dotnet format --verify-no-changes
```

Manual UI sanity checks (important for Avalonia):

- View opens without binding errors in output/logs
- Commands enable/disable correctly
- Loading spinner/progress visibility works
- No UI freeze during long operations
- Cancel/retry behavior is correct
- DataTemplate renders expected item type(s)

---

## 8) Suggested project-specific additions (optional)

For `PhotoMapperAI` specifically, consider adding small companion rules later:

- `61-avalonia-dialogs-files.md` (file pickers, dialogs, platform differences)
- `62-avalonia-performance-large-lists.md` (virtualization, thumbnail loading)
- `63-image-processing-threading.md` (background processing + progress/cancel)
- `64-ux-states-errors.md` (empty states, retry UX, status messages)

---

## 9) Reference links (official docs first)

### Avalonia Docs (official)

- Avalonia Docs home: https://docs.avaloniaui.net/
- The MVVM Pattern (Avalonia): https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/
- Avalonia UI and MVVM: https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/avalonia-ui-and-mvvm
- View Locator: https://docs.avaloniaui.net/docs/concepts/view-locator
- Data Binding (overview): https://docs.avaloniaui.net/docs/basics/data/data-binding/
- Data Binding Syntax: https://docs.avaloniaui.net/docs/basics/data/data-binding/data-binding-syntax
- Compiled Bindings: https://docs.avaloniaui.net/docs/basics/data/data-binding/compiled-bindings
- Data Templates (basics): https://docs.avaloniaui.net/docs/basics/data/data-templates
- Styles (basics): https://docs.avaloniaui.net/docs/basics/user-interface/styling/styles
- Style Classes: https://docs.avaloniaui.net/docs/basics/user-interface/styling/style-classes
- Control Themes: https://docs.avaloniaui.net/docs/basics/user-interface/styling/control-themes
- Style Selector Syntax (reference): https://docs.avaloniaui.net/docs/reference/styles/style-selector-syntax
- Accessing the UI Thread: https://docs.avaloniaui.net/docs/guides/development-guides/accessing-the-ui-thread
- Data Validation (Avalonia): https://docs.avaloniaui.net/docs/guides/development-guides/data-validation

### Avalonia API reference

- Dispatcher class: https://api-docs.avaloniaui.net/docs/T_Avalonia_Threading_Dispatcher
- Dispatcher.UIThread property: https://api-docs.avaloniaui.net/docs/P_Avalonia_Threading_Dispatcher_UIThread

### CommunityToolkit.Mvvm (official Microsoft Learn)

- MVVM Toolkit overview: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- Introduction to MVVM Toolkit: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- ObservableObject: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observableobject
- MVVM source generators overview: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/overview
- ObservableProperty attribute: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty
- RelayCommand attribute: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand
- AsyncRelayCommand: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/asyncrelaycommand

---

## 10) Copy-paste summary for agent prompt (optional)

If you want a shorter instruction for an AI agent, use this:

- Use Avalonia MVVM with strict separation: View = AXAML/UI only, ViewModel = state+commands, Services = I/O/business workflow.
- Use `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`) and keep generator-based ViewModels `partial`.
- Prefer bindings/commands over code-behind handlers; keep code-behind minimal.
- Use `x:DataType` and compiled bindings where available.
- Model async state explicitly (`IsLoading`, `StatusText`, `ErrorMessage`, `ProgressValue`) and never block the UI thread.
- Use `Dispatcher.UIThread` when updating UI-bound state from background work.
- Keep navigation in a ViewModel/service; ViewLocator resolves views only.
- Validate with `dotnet build` (and `dotnet test` if present) after changes.

