# Orion WinUI Architecture

This document defines the folder structure and architectural layers for the Orion C# / WinUI 3 frontend. It follows the MVVM pattern using the [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) library.

## Folder Structure

```
Orion/
├── Models/             Data entities and business rules
├── Adapters/           Wrappers that adapt data for UI consumption
├── Services/           Shared app state and functionality
├── ViewModels/         UI logic and state for specific Views
├── Views/              Screens (Windows, Pages)
├── Controls/           Reusable UI components
├── Converters/         XAML value converters
├── Interop/            Native platform interop (Win32, COM, P/Invoke)
├── Utilities/          General-purpose helpers
├── Assets/             Icons, images, resources
└── App.xaml(.cs)       Application entry point
```

## Layer Diagram

```
┌──────────────────────────────────────────────────────┐
│  Views & Controls          (UI - XAML + code-behind) │
│  Converters                                          │
└──────────────┬───────────────────────────┬───────────┘
               │ data binding (x:Bind)     │
               ▼                           │
┌──────────────────────────────────┐       │
│  ViewModels   (UI logic, cmds)   │       │
└──────────┬───────────────────────┘       │
           │ uses           │              │
     ┌─────┴──────┐         │              │
     ▼            ▼         │              │
┌──────────┐ ┌──────────┐   │              │
│ Services │ │ Adapters │   │              │
└────┬─────┘ └────┬─────┘   │ uses         │
     │            │         │              │
     │    uses    │ wraps   │              │ direct use
     ▼            ▼         ▼              │
┌──────────────────────────────────┐       │
│  Models          (data entities) │       │
└──────────────────┬───────────────┘       │
                   │                       │
                   ▼                       ▼
          ┌───────────────────────────────────────┐
          │  Interop                              │
          │  (Win32, COM)                         │
          └───────────────────────────────────────┘
```

**Data flows down** through binding. **Events flow up** through commands and callbacks. ViewModels never reference Views. Models never reference ViewModels.

Views and Controls may use Interop directly when they need native APIs for UI mechanics (e.g., `HwndHostPanel` calling Win32 window management functions).

**Binding:** Prefer `{x:Bind}` over the older `{Binding}`. `x:Bind` is compiled at build time (faster, catches errors at compile time) and defaults to `OneTime` mode. Use `Mode=OneWay` for properties that change, `Mode=TwoWay` for user input controls.

---

## Models/

**Data entities and business rules.** The "M" in MVVM.

A Model is a piece of data your application works with. It represents things that exist regardless of whether there is a UI.

### What belongs here
- Data classes (tab, bookmark, history entry, profile, download)
- Data validation and business rules
- Computed properties derived from the data itself
- Identity (IDs, equality)

### Decision test
> "Would this property or logic be needed if there was no UI at all?"
> If yes, it belongs in the Model.

### Observable vs. plain

```
Use ObservableObject when:          Use plain class / record when:
─────────────────────────────       ────────────────────────────────
Properties change while displayed   Data is immutable once created
(tab title updates during load)     (history entry)

UI needs live updates via binding   Data is only displayed in lists
                                    (replaced, not mutated)
```

### Examples

```csharp
// Observable model - title and URL change while displayed
public partial class BrowserTab : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    // Business rule - true regardless of UI
    public bool IsInternalPage => Url.StartsWith("orion://");
}

// Plain model - set once, never mutates
public record HistoryEntry(string Url, string Title, DateTime VisitedAt);
```

### What does NOT belong here
- Commands (`ICommand`, `[RelayCommand]`) - those go in ViewModels
- UI state (IsSelected, Visibility, IsExpanded) - those go in ViewModels
- Error messages for the user - ViewModels decide how to present errors
- Service calls, database access - those go in Services

---

## Adapters/

**Wrappers that transform or adapt data for a specific consumer.**

An Adapter is not data itself. It wraps, projects, or reshapes existing data for use by the UI or another layer. If it contains a reference to another object and its main purpose is making that object usable in a different context, it is an Adapter.

### What belongs here
- Collection projections (e.g., flattening a tree for a flat list control)
- Observable wrappers around non-observable models (e.g., `ObservableUser` wrapping a database `User`)
- Format adapters (e.g., grouping a flat list by date for display)

### Decision test
> "Does this class primarily exist to make some other object work in a context it wasn't designed for?"
> If yes, it is an Adapter.

### Examples

```csharp
// Adapts a tree structure (TabGroup) into a flat IList for TabView
public class FlattenedTabCollection : IList, INotifyCollectionChanged { ... }

// Adapts a plain database model for UI binding
public class ObservableUser : ObservableObject
{
    private readonly User _user;
    public string Name
    {
        get => _user.Name;
        set => SetProperty(_user.Name, value, _user, (u, n) => u.Name = n);
    }
}
```

### What does NOT belong here
- Data entities with their own identity (those are Models)
- Classes that hold shared app state (those are Services)
- XAML value converters (those go in Converters/)

---

## Services/

**Shared application state and functionality.**

A Service holds state or provides functionality that multiple ViewModels might need. It is not tied to any specific View. Without dependency injection, Services are typically accessed as singletons or passed via constructors.

### What belongs here
- Shared state containers (collection of all open tabs, current profile)
- Functionality used across ViewModels (navigation, dialog display, settings persistence)
- Wrappers around external APIs or backend calls

### Decision test
> "Could multiple ViewModels need access to this state or functionality?"
> If yes, it belongs in Services.

### Examples

```csharp
// Shared state - multiple ViewModels may need to access tabs
public partial class BrowserState : ObservableObject
{
    public ObservableCollection<BrowserTab> Tabs { get; } = new();

    [ObservableProperty]
    private BrowserTab? _activeTab;
}

// Functionality - persisting and loading settings
public class SettingsService
{
    public string GetHomePage() { ... }
    public void SetHomePage(string url) { ... }
}
```

### What does NOT belong here
- UI state for a specific view (those go in ViewModels)
- Data entities (those are Models)
- P/Invoke declarations (those go in Interop/)

---

## ViewModels/

**UI logic and state for a specific View.** The "VM" in MVVM.

A ViewModel is the bridge between a View and the rest of the application. It exposes properties for display, commands for user actions, and UI-specific state. Each View typically has one ViewModel.

### What belongs here
- Properties that drive the UI (IsLoading, ErrorMessage, SelectedItem)
- Commands for user actions (`[RelayCommand]`)
- UI-specific validation and error messages
- Presentation logic (formatting data for display)
- Enums that represent UI modes or states

### Naming convention

Each ViewModel is named after its View with a `ViewModel` suffix. This creates a 1:1 mapping:

```
View                  ViewModel
────────────────      ──────────────────────
MainWindow            MainWindowViewModel
LibraryWindow         LibraryWindowViewModel
SettingsPage          SettingsPageViewModel
```

Very simple Views with no state or commands may not need a ViewModel at all.

### Decision tests
> "Is this state or logic only meaningful because a specific UI screen exists?"
> If yes, it belongs in the ViewModel.

> "Can I unit test this without creating any UI controls?"
> If yes, it probably belongs in the ViewModel (not code-behind).

### Examples

```csharp
public partial class MainViewModel : ObservableObject
{
    // UI state
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _addressBarText = string.Empty;

    // Selection state
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseTabCommand))]
    private BrowserTab? _selectedTab;

    // Command
    [RelayCommand(CanExecute = nameof(CanCloseTab))]
    private void CloseTab(BrowserTab tab) { ... }

    private bool CanCloseTab(BrowserTab tab) => tab is not null;

    // Presentation logic
    public string StatusMessage => IsLoading ? "Loading..." : "Ready";
}

// Generated from orion-linux SidebarPage enum by OrionCodeGen
public enum SidebarPage { not_set = 0, windows = 1, tabs = 2, bookmarks = 3, reading_list = 4 }
```

### What does NOT belong here
- Business rules independent of UI (those go in Models)
- State shared across multiple ViewModels (those go in Services)
- Direct Win32/COM calls (those go in Interop/, called via Services if needed)

---

## Views/

**Screens: Windows and Pages.** The "V" in MVVM.

A View is a top-level screen in the application. It consists of a XAML file defining layout and a **code-behind** file for UI mechanics that cannot be expressed in XAML. Each View references a ViewModel.

**Code-behind** refers to the `.xaml.cs` file that is paired with a `.xaml` file. For example, `MainWindow.xaml.cs` is the code-behind for `MainWindow.xaml`. It is a partial class that shares the same class as the XAML file, giving it direct access to all named UI controls. Partial class files like `MainWindow.Tabs.cs` are also considered code-behind since they are part of the same class.

### What belongs here
- Window and Page XAML files with their code-behind
- Partial class files for large Views (e.g., `MainWindow.Tabs.cs`)

### What belongs in code-behind (.xaml.cs)
- Wiring up the ViewModel
- Focus management, scroll-into-view
- Drag-and-drop UI handling
- Animations triggered by events
- Anything that requires direct access to UI controls

### What does NOT belong in code-behind
- Input validation (use ViewModel + data validation attributes)
- Business logic (use ViewModel commands)
- State tracking (use ViewModel properties)
- Conditional visibility logic (use binding + converters)

### Decision tests: ViewModel vs. View code-behind

> "Does this require a reference to a specific UI control?"
> Yes: code-behind. (Focus, ScrollIntoView, animation start)
> No: probably ViewModel. Continue checking:

> "Can I write a unit test for this without creating any XAML controls?"
> Yes: ViewModel. (Commands, validation, state changes)
> No: code-behind. (It depends on real UI objects)

> "If I completely redesigned the UI, would this logic stay the same?"
> Yes: ViewModel. (Closing a tab is the same regardless of layout)
> No: code-behind. (Drag-drop positioning depends on the actual controls)

Examples of the boundary:

```
ViewModel (testable without UI)         View code-behind (needs real controls)
──────────────────────────────          ─────────────────────────────────────
CloseTab command                        ScrollIntoView after selection changes
IsAddressBarFocused (bool property)     AddressBar.Focus(FocusState.Programmatic)
Input validation ("URL is invalid")     Drag-and-drop event args handling
SelectedTab property                    Setting up keyboard accelerators
"Navigate to URL" logic                 Title bar button click -> ViewModel call
Toggle sidebar open/closed (state)      Animate sidebar width transition
```

### Examples

```
Views/
├── MainWindow.xaml              # Main browser window
├── MainWindow.xaml.cs           # Constructor, title bar, close
├── MainWindow.Tabs.cs           # Tab drag-drop UI mechanics
├── MainWindow.Navigation.cs     # Address bar focus, key handling
├── MainWindow.Sidebar.cs        # Sidebar scroll, toggle animation
├── LibraryWindow.xaml            # Library screen
└── LibraryWindow.xaml.cs
```

---

## Controls/

**Reusable UI components.**

A Control is a self-contained UI element that could potentially be used in multiple Views. Controls do not typically have their own ViewModel. They expose dependency properties for data binding.

### Decision test
> "Is this a building block that could appear in different screens?"
> If yes, it is a Control.

### What belongs here
- Custom UserControls (HwndHostPanel, PopupDialog)
- Specialized panels or input controls
- App-specific components used across Views (e.g., a browser toolbar)

### Examples

```
Controls/
├── HwndHostPanel.xaml(.cs)      # Hosts native HWNDs in WinUI
├── PopupDialog.xaml(.cs)        # Modal dialog that renders above HWNDs
└── SettingsPage.xaml(.cs)       # Settings UI (embedded as tab content)
```

---

## Converters/

**XAML value converters.**

Small classes that implement `IValueConverter` for transforming bound values in XAML (e.g., bool to Visibility, enum to string). Keep them simple - if the conversion involves complex logic, it likely belongs in the ViewModel as a computed property instead.

```
Converters/
└── BoolToBackgroundConverter.cs
```

---

## Interop/

**Native platform interop.**

All P/Invoke declarations, COM interface definitions, Win32 constants, and native struct definitions. This code is the boundary between managed C# and unmanaged native code.

### What belongs here
- Win32 API declarations and constants
- COM interface definitions (DirectShow, etc.)
- OrionBackend DLL imports and callback definitions
- Type-safe native handle wrappers (HWND, HANDLE)
- Native struct layouts

### Examples

```
Interop/
├── Win32.cs                # Win32 API (window management, GDI, DWM)
├── DirectShow.cs           # DirectShow COM interfaces (video/media)
└── OrionBackend.cs         # OrionBackend.dll P/Invoke bridge
```

---

## Utilities/

**General-purpose helpers.**

Small utility classes and extension methods that don't fit into any specific layer. If a helper grows large or domain-specific, consider moving it to a more appropriate layer.

### What belongs here
- Visual tree helpers
- String/path utilities
- Extension methods

### Examples

```
Utilities/
└── Utils.cs                # VisualTreeHelper wrappers, GetWindowHandle
```

---

## Quick Reference: Where Does It Go?

```
Ask yourself:                                          Folder
──────────────────────────────────────────────         ──────────────
Is it data that exists regardless of UI?               Models/
Does it wrap/reshape data for a specific consumer?     Adapters/
Is it shared state or functionality across ViewModels? Services/
Is it UI logic/state for a specific screen?            ViewModels/
Is it a screen (Window, Page)?                         Views/
Is it a reusable UI building block?                    Controls/
Does it convert values for XAML binding?               Converters/
Is it native/COM interop?                              Interop/
Is it a general helper?                                Utilities/
```

## What Goes Where: Common Scenarios

```
Scenario                          Wrong place     Right place              Why
────────────────────────────────  ──────────────  ───────────────────────  ─────────────────────────────
URL validation (is valid URL?)    ViewModel       Model                    Business rule, true without UI
"Invalid URL" error message       Model           ViewModel                Presentation of a business rule
IsSelected state for a tab        Model           ViewModel                Only exists because of UI
Collection of all open tabs       ViewModel       Service                  Multiple ViewModels may need it
Flattened view of tab tree        Model           Adapter                  Reshapes data for TabView
Button enabled/disabled           Code-behind     ViewModel (CanExecute)   Testable, declarative
Focus the address bar             ViewModel       Code-behind              Requires direct control access
Bool to Visibility                ViewModel       Converter (or VM prop)   Keep bindings clean
Win32 SetWindowPos call           Service         Interop/ (via Service)   Native boundary
```

## Commands vs. Click Handlers

**Prefer `Command` binding over `Click` event handlers** when the action represents logic that belongs on a Model or ViewModel. Commands are testable, automatically handle enabled/disabled state, and keep logic out of code-behind.

```xml
<!-- Preferred — Command binding (auto-disables when CanExecute is false) -->
<Button Command="{x:Bind ViewModel.ActiveTab.GoBackCommand, Mode=OneWay}" />

<!-- Avoid — Click handler that just delegates to a command or method -->
<Button Click="BackButton_Click" />
```

**Use `[RelayCommand(CanExecute = ...)]`** to auto-disable buttons. The source generator creates the `ICommand` property and wires up `CanExecuteChanged`. Pair with `[NotifyCanExecuteChangedFor]` on the property that controls availability:

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
public partial NativeWebView? NativeWebView { get; set; }

public bool HasNativeWebView => NativeWebView != null;

[RelayCommand(CanExecute = nameof(HasNativeWebView))]
private void GoBack() => NativeWebView?.GoBack();
```

**Commands can live on Models**, not just ViewModels. If the model owns the behavior (e.g., `PopularExtensionItem` owns its install action), put the command there. XAML can bind through property paths.

**Click handlers are still appropriate for:**
- Pure UI mechanics (toggling flyouts, focus management, layout changes)
- Actions that need access to UI elements not available through binding (e.g., `FileSavePicker` requiring a window handle)
- Event handlers where `Command` isn't available (e.g., `SelectionChanged`, `ItemInvoked`, `DragItemsCompleted`)
- One-off UI interactions that don't map to reusable logic

## Namespace Convention

All classes use a single flat namespace regardless of folder:

```csharp
namespace Orion { ... }
```

Folders organize files on disk but do not affect namespaces. This avoids unnecessary `using` statements since all class names are distinct. If the project grows large enough that names collide, per-folder namespaces can be introduced later.
