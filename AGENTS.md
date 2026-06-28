# Agents Instructions

## Directives

1. **Golden rule:** Static safety & correctness come first, then intentional naming aligned with the ubiquitous language, then changeability (small cohesive surfaces), then simplicity (YAGNI/DRY), then consistency with existing style, and lastly backward compatibility. Anytime trade-offs arise, follow that priority order and document deviations.
2. **Testing cadence:** Runtime/manual validation happens inside Resonite later. During development, lean on automated checks, document assumptions, and add TODOs instead of speculative fixes.
3. **Localization:** Keep all UI and user-facing strings in English. Revisit localization only when Resonite exposes official hooks for custom locales.
4. **Knowledge management:** Public, committable process notes belong in versioned docs (e.g., this file, README). Exploratory or sensitive research should live in an ignored `local_notes/` folder with one topic per markdown file.
5. **Persistence:** Whenever workflow/process guidance changes, update this AGENTS file (or a nested `AGENTS.md`) immediately; treat it as the authoritative policy.
6. **Data model constraints:** Template examples must stick to existing FrooxEngine data-model constructs; do not introduce new sync types or delegates in boilerplate code.
7. **Design notes ban:** Avoid ad-hoc design docs. Keep intent obvious in code, commits, and this file.

## Task Completion Ritual

Before calling any change “done,” run and fix the following in order (pass `-p:ResonitePath=...` if auto-detect fails):

1. `dotnet format whitespace --verify-no-changes --no-restore`
2. `dotnet format style --verify-no-changes --no-restore --severity info`
3. `dotnet format analyzers --verify-no-changes --no-restore --severity info`
4. `dotnet build -c Release`
5. `dotnet test -c Release`

Document failures in the PR/task notes and rerun once resolved.

## Environment Notes

- Ensure `ResonitePath` points at an actual Resonite installation. Override with `-p:ResonitePath=/absolute/path/to/Resonite` when necessary (CI injects this automatically).
- When WSL or Linux builds complain about the fallback NuGet folder (`Unable to find fallback package folder 'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'`), either export `DOTNET_MSBUILD_SDK_RESOLVER_CLI_FALLBACK_FOLDER=$HOME/.nuget/fallback`, create a matching directory under `/mnt/c/.../Shared/NuGetPackages`, or prune the fallback entry from `NuGet.Config` before rerunning the ritual.
- CI provisions Resonite via `setup-resonite-env` on GitHub Actions; keep your local builds/tests aligned with Release configuration so drift is caught early.

## Localization & History

- Ship English-only UI in samples until upstream allows proper localization.
- Keep detailed work history in git commits. Do not maintain parallel changelogs outside the repo’s docs/PRs.
