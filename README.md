# RenderiteImeIntegration

Windows desktop IME composition overlay mod for [Resonite](https://resonite.com/).

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Ensure your Resonite launch options include `-LoadAssembly Libraries/ResoniteModLoader.dll`.
1. Put `ImeIntegration.dll` into `rml_mods`.
   Standard Steam path: `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods`
1. Launch Resonite and confirm the mod loads in the log.

## Notes

- This mod depends on a `Renderite` composition message path. If the runtime `Renderite.Shared.KeyboardState` does not expose the composition fields, the mod stays loaded but does not patch or render overlays.
- The currently required custom `Renderite` forks are:
  - [esnya/Renderite](https://github.com/esnya/Renderite)
  - [esnya/Renderite.Unity.Renderer](https://github.com/esnya/Renderite.Unity.Renderer)

## Development

- Build:
  - `dotnet build ResoniteImeIntegration.sln -c Release -p:ResonitePath="..."`
- Test:
  - `dotnet test ResoniteImeIntegration.sln -c Release -p:ResonitePath="..."`
- Copy to local `rml_mods`:
  - `dotnet build ResoniteImeIntegration.sln -c Release -p:CopyToMods=true -p:ResonitePath="..."`
- Hot reload:
  - add `-p:EnableResoniteHotReloadLib=true`

## License

[MIT](./LICENSE)
