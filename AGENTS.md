# AGENTS.md

Guidance for coding agents working in the `osuplayer` repository.
Complements `README.md` with implementation-focused context.

## Project overview

Desktop music player for osu! songs, written in C# / .NET 8.

- **UI framework:** Avalonia 11.3.14 + ReactiveUI + FluentAvalonia 2.4.1
- **Solution:** `OsuPlayer.sln` (multi-project)

### Projects

| Project | Purpose |
|---|---|
| `OsuPlayer/` | Main UI app and composition root |
| `OsuPlayer.Data/` | Enums, models, shared data contracts |
| `OsuPlayer.Interfaces/` | Service interfaces |
| `OsuPlayer.Services/` | Service implementations (audio, shuffle, sort, history, etc.) |
| `OsuPlayer.Extensions/` | Helpers, value converters, enum extensions |
| `OsuPlayer.IO/` | Storage, import, DB reader logic |
| `OsuPlayer.Network/` | API and network clients |
| `OsuPlayer.Tests/` | NUnit test project |
| `OsuPlayer.CrashHandler/` | Separate crash-handler executable |
| `OsuPlayer.Updater/` | Separate auto-updater executable |

---

## Dev environment and commands

Run all commands from the repo root.

```bash
dotnet restore
dotnet build OsuPlayer.sln -c Debug
dotnet run --project OsuPlayer
dotnet test OsuPlayer.Tests/OsuPlayer.Tests.csproj -c Debug
```

CI (`.github/workflows/dotnet.yml`) builds and then runs tests from the built test DLLs.

---

## Architecture

### Dependency injection

- Composition root: `OsuPlayer/Program.cs` — `Register(...)` method.
- Services are registered with **Splat** (`RegisterLazySingleton<IFoo>(() => new Foo())`).
- Resolved in ViewModels and windows via `Locator.Current.GetRequiredService<IFoo>()`.
- Interfaces live in `OsuPlayer.Interfaces/`; implementations in `OsuPlayer.Services/` or `OsuPlayer/Modules/`.

### MVVM pattern

Every view follows this three-file structure:

```
Foo.axaml           — layout/template (Avalonia XAML)
Foo.axaml.cs        — code-behind (event handlers, wiring)
FooViewModel.cs     — ViewModel (state + commands, extends ViewModelBase)
```

`Foo.axaml.cs` is listed as `DependentUpon` the `.axaml` in `.csproj`.

### Reactive state — `Bindable<T>`

The codebase uses **Founntain.Nein**'s `Bindable<T>` instead of plain properties for reactive shared state:

```csharp
public Bindable<RepeatMode> RepeatMode { get; } = new(Enums.RepeatMode.NoRepeat);
```

React to changes with `BindValueChanged`:

```csharp
someBindable.BindValueChanged(e => DoSomething(e.NewValue), true);
```

The `true` argument fires immediately with the current value.

### Enum cycling — `Next()`

`OsuPlayer.Extensions` adds a `Next()` extension on enums that cycles through all declared values and wraps around:

```csharp
RepeatMode.Value = RepeatMode.Value.Next();
```

Order of cycling matches the declaration order in the enum.

### Value converters

Value converters in `OsuPlayer.Extensions/ValueConverters/` implement `IValueConverter` and map model values to UI values (icons, strings, colors). Many take a `Bindable<T>` directly.

---

## Key areas and files

### Audio / playback

| File | Role |
|---|---|
| `OsuPlayer/Modules/Audio/Player.cs` | Core player: next/prev, repeat, shuffle, song lifecycle |
| `OsuPlayer/Modules/Audio/BassEngine.cs` | ManagedBass wrapper (low-level playback) |
| `OsuPlayer/Modules/Audio/LinuxMprisService.cs` | MPRIS2 D-Bus service — routes GNOME/KDE media keys to the player on Linux |
| `OsuPlayer/Modules/Audio/Interfaces/` | `IPlayer`, `IHasPlaylists`, `IPlayModes`, etc. |
| `OsuPlayer.Services/OsuSongSourceService.cs` | Provides the full song library list |
| `OsuPlayer.Services/ShuffleService.cs` | Shuffle state and algorithm selection |
| `OsuPlayer.Services/ShuffleImpl/` | Concrete shuffle algorithms (`RngShuffler`, `BalancedShuffler`, etc.) |
| `OsuPlayer.Services/SortService.cs` | Song list sort logic |

### UI views (`OsuPlayer/Views/`)

| View | Purpose |
|---|---|
| `PlayerControlView` | Bottom player bar (transport controls, volume, repeat, shuffle) |
| `HomeView` | Main song library browser |
| `SearchView` | Song search |
| `PlaylistView` | Playlist browser and playlist song list |
| `PlaylistEditorView` | Create/edit playlists |
| `SettingsView` | App settings |
| `EqualizerView` | EQ configuration |
| `BeatmapsView` | Beatmap detail view |
| `BlacklistEditorView` | Manage blacklisted songs |
| `PlayHistoryView` | Recently played list |
| `StatisticsView` | Playback statistics |
| `TopBarView` | Window top bar / navigation |
| `UserView` / `EditUserView` | User profile |

### Windows (`OsuPlayer/Windows/`)

| Window | Purpose |
|---|---|
| `FluentAppWindow` | Main application window |
| `Miniplayer` | Compact always-on-top player window |
| `FullscreenWindow` | Fullscreen mode |
| `LoginWindow` / `CreateProfileWindow` | Auth flow |
| `ExportSongsProcessWindow` | Export progress |

### Data and config

| File | Purpose |
|---|---|
| `OsuPlayer.Data/OsuPlayer/Enums/` | All enums (`RepeatMode`, `ShuffleMode`, `PlayDirection`, etc.) |
| `OsuPlayer.Data/DataModels/` | Song/playlist/user model types |
| `OsuPlayer.IO/Storage/` | Config read/write (`ConfigContainer`, `JsonService`) |
| `data/config.json` | Runtime config (user-local, not committed) |
| `data/playlists.json` | Saved playlists (user-local) |

### Services

| Service | Purpose |
|---|---|
| `HistoryService` | Track play history |
| `LastFmService` | Last.fm scrobbling |
| `DiscordService` | Discord rich presence |
| `ProfileManagerService` | User profile management |
| `ApiStatisticsService` | osu!player API stats |
| `LoggingService` | App-level logging |
| `JsonService` | Generic JSON persistence |
| `DbReaderFactory` | Creates the osu! DB reader for the current install type |

---

## Coding conventions

- **Interfaces first:** add to `OsuPlayer.Interfaces/`, implement in `OsuPlayer.Services/` or the appropriate module.
- **No magic strings for config keys:** use the existing `ConfigContainer` properties.
- **Icon system:** use `MaterialIconKind` from `Material.Icons.Avalonia`. Converters map model values to icon kinds.
- **`PlayerControlView` and `Miniplayer` must stay behaviorally aligned.** If you change player behavior, update both.
- **Value converters:** when a UI element needs a derived value from a model property, write a converter in `OsuPlayer.Extensions/ValueConverters/` rather than putting logic in code-behind.
- **Tests:** unit tests live in `OsuPlayer.Tests/`. Add or update tests when changing public behavior. Tests use NUnit 4.

---

## Linux / MPRIS2

`LinuxMprisService` implements the MPRIS2 D-Bus interface so GNOME Shell, KDE, and other desktop environments route hardware media keys to osu!player. It is instantiated by `Player.cs` on Linux only.

- Do **not** add an explicit `Tmds.DBus.Protocol` package reference — it is resolved transitively from `Avalonia.FreeDesktop` and must stay in sync with Avalonia.
- `PlayerControlView` and `Miniplayer` must remain aligned with any transport changes that also affect MPRIS.

---

## Avalonia 11.3 AXAML notes

The project targets **Avalonia 11.3.14** / **FluentAvaloniaUI 2.4.1** / **SkiaSharp 2.88.9**. When writing AXAML:

- **`ItemsRepeater`, `StackLayout`, `WrapLayout`** do not exist in Avalonia 11.2+. Do not add the old `Avalonia.Controls.ItemsRepeater` NuGet package. Use `ItemsControl` + `ItemsPanelTemplate` with `StackPanel` or `WrapPanel` instead.
- **`HyperlinkButton`** is in Avalonia core — use it without a namespace prefix, not `ui:HyperlinkButton`.
- **`Avalonia.ReactiveUI`** must stay pinned at **11.3.9** (11.3.14 does not exist for this package).

---

## Safety rules

- `data/` is user-local runtime state. Never commit its contents.
- Do not hardcode secrets or credentials anywhere in source.
- Do not edit generated or build outputs (`bin/`, `obj/`).
- Keep changes minimal and scoped. Do not refactor unrelated files in the same patch.

---

## Verification checklist

Before finishing any change:

1. `dotnet build OsuPlayer.sln -c Debug` — must succeed with 0 errors.
2. `dotnet test OsuPlayer.Tests/OsuPlayer.Tests.csproj -c Debug` — must pass (explain any pre-existing failures).
3. If you changed a converter, check all views that bind to it.
4. If you changed playback behavior, verify both `PlayerControlView` and `Miniplayer`.
5. If you changed a service interface, update the registration in `Program.cs` if needed.

---

If instructions conflict: explicit user request > nearest `AGENTS.md` > this root file.
