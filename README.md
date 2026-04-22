# ResoniteImeIntegration

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod repository for [Resonite](https://resonite.com/) that ships the `ImeIntegration` mod and targets Windows desktop IME integration.

## Installation

1. Install the [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Ensure your Resonite launch options include `-LoadAssembly Libraries/ResoniteModLoader.dll`, per the ResoniteModLoader installation instructions.
1. Place the `ImeIntegration.dll` build output into your `rml_mods` folder. This folder should be located at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a standard installation. You can create it if it's missing, or if you start the game once with the ResoniteModLoader installed it will create this folder for you.
1. Launch the game. If you want to check that the mod is working you can check your Resonite logs.

## Development

### Requirements

- .NET 10 SDK (install the latest preview until GA ships)
- A Resonite installation that exposes `FrooxEngine.dll` (the default Steam paths on Windows/WSL are discovered automatically, otherwise pass `-p:ResonitePath=/absolute/path/to/Resonite`)
- [ResoniteHotReloadLib](https://github.com/Nytra/ResoniteHotReloadLib) if you plan to use hot reload

### Installation for Development

1. Clone this repository.
2. Ensure the Resonite installation path is reachable. If it lives somewhere unusual, add `-p:ResonitePath="/path/to/Resonite"` to your build/test commands.
3. Build the project: `dotnet build`

### Development Workflow

- Before committing, run `dotnet format ResoniteImeIntegration.sln --verify-no-changes --no-restore`.
- Keep local builds/tests aligned with CI by running `dotnet build ResoniteImeIntegration.sln -c Release -p:ResonitePath="..."` and `dotnet test ResoniteImeIntegration.sln -c Release -p:ResonitePath="..."`.
- If the runtime `Renderite.Shared` contract does not expose composition fields, `ImeIntegration` stays loaded but does not patch or render overlays.
- Refer to `AGENTS.md` for the authoritative checklist shared with CI.

### Install to `rml_mods` Directory (and `rml_mods/HotReloadMods`)

Set `CopyToMods=true` when building to mirror the compiled DLL into your Resonite install automatically:

```bash
dotnet build -p:CopyToMods=true -p:ResonitePath="C:\Program Files (x86)\Steam\steamapps\common\Resonite"
```

### Hot Reload Development

Opt-in to hot reload support by dropping [ResoniteHotReloadLib](https://github.com/Nytra/ResoniteHotReloadLib) into `$(ResonitePath)/rml_libs` and passing `-p:EnableResoniteHotReloadLib=true` (include it alongside `CopyToMods=true` if you also want the HotReloadMods copy). Without that property the project omits both the reference and compiler symbol, so developers without the DLL can still build.

### Versioning & Releases

[GitVersion](https://gitversion.net/) supplies semantic versions for builds and packages. Push a `v*` tag (for example `v0.2.0`) and the CI workflow will build, test, and publish the release artifacts automatically.
