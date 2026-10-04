


## Run locally

Run these commands from the repository root. Install the .NET 10 SDK selected
by `global.json` and the .NET 8 runtime for tests and benchmarks. Examples target
.NET 10; the library targets .NET 8. Automation scripts require Python 3
(`python3` below; use `python` or `py -3` on Windows).

### Build and test

```sh
dotnet restore FormatParse.slnx
dotnet build FormatParse.slnx -c Release --no-restore -warnaserror
dotnet test tests/FormatParse.Tests/FormatParse.Tests.csproj -c Release --no-build --no-restore
python3 scripts/test_automation.py
```

To build only the library:

```sh
dotnet build src/FormatParse/FormatParse.csproj -c Release
```

### Coverage

Use a fresh results directory for each run: the coverage script expects exactly
one report. The following Bash commands keep previous reports intact:

```sh
mkdir -p artifacts
coverage_dir=$(mktemp -d artifacts/coverage.XXXXXX)
dotnet test tests/FormatParse.Tests/FormatParse.Tests.csproj -c Release --no-build --no-restore --collect:"XPlat Code Coverage" --results-directory "$coverage_dir"
python3 scripts/coverage.py "$coverage_dir"
```

In PowerShell, pass a new directory name to `--results-directory`, then pass
that same directory to `scripts/coverage.py`. The gate is 90% lines and 85% branches.

### Examples

```sh
dotnet run -c Release --project examples/FormatParse.Example
```

### Benchmarks

```sh
# Check that all parsers return equivalent results, without timing.
dotnet run -c Release --project benchmarks/FormatParse.Benchmarks -- --validate

# Smoke check: not suitable for performance conclusions.
dotnet run -c Release --project benchmarks/FormatParse.Benchmarks -- --job Dry --filter '*'

# Short measured run, including allocation reports.
dotnet run -c Release --project benchmarks/FormatParse.Benchmarks -- --job Short --filter '*' --exporters json

# Full default measurement.
dotnet run -c Release --project benchmarks/FormatParse.Benchmarks -- --filter '*' --exporters json
```

Reports appear in `BenchmarkDotNet.Artifacts/results`. After building in Release,
add `--no-build` before `--project` to skip rebuilding the benchmark host.

### Release package

`-c Release` selects the build configuration; it does not publish anything.
Build and test first, then create and inspect the local NuGet package:

```sh
dotnet pack src/FormatParse/FormatParse.csproj -c Release --no-build --no-restore -o artifacts/packages
python3 scripts/release.py version v0.1.0
python3 scripts/release.py package artifacts/packages/FormatParse.0.1.0.nupkg 0.1.0
```

For later releases, update `Version` in the library project and replace `0.1.0`
in these commands. The release tag must match that version exactly.

### Publish a release

Prefer the GitHub [release workflow](.github/workflows/release.yml), which uses
[NuGet Trusted Publishing](https://github.com/NuGet/login) rather than a stored
API key. Confirm ownership of the NuGet package ID, configure its trusted
publishing policy for this repository, `release.yml`, and the `production`
environment, and set the GitHub Actions variable `NUGET_USERNAME` to your NuGet
username. Commit and push your changes, and wait for CI to pass. Then push the
matching version tag:

```sh
git tag -a v0.1.0 -m "FormatParse 0.1.0"
git push origin v0.1.0
```

The workflow revalidates the tagged commit, publishes the package to NuGet,
and publishes a GitHub Release. A manual retry accepts the same existing tag.

If publishing directly from your machine instead, set `NUGET_API_KEY` securely
in your environment and run this Bash command after package inspection:

```sh
dotnet nuget push artifacts/packages/FormatParse.0.1.0.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

This last command publishes externally. Keep the key out of source control;
an existing NuGet version cannot be replaced. Use a new version for corrections.