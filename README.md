# papers-cli

A CLI tool for searching, downloading, and managing academic papers from arXiv, J-STAGE, and IRDB (Institutional Repositories).

## Features

- Single-source search with paging (arXiv, J-STAGE, IRDB)
- PDF download with SQLite metadata management
- Rich terminal UI via Spectre.Console (tables, panels, progress bars)
- `--json` output for scripting
- XDG Base Directory Specification compliant

## Install

```bash
# Build
dotnet build

# Publish as a self-contained single binary
dotnet publish src/PapersCli.Cli -c Release -r linux-x64
```

## Usage

### Search

```bash
# Search the configured default source (arxiv by default)
papers-cli search "attention mechanism"

# Search a specific source
papers-cli search "深層学習" --source jstage
papers-cli search "transformer" --source arxiv --category cs.AI --from 2023

# Filter options
papers-cli search "reinforcement learning" --author "Yamada" --from 2020 --to 2024 --sort-key date --sort-order desc --limit 10

# Paging
papers-cli search "attention" --source arxiv --limit 10 --page 2

# JSON output includes result metadata and a results array
papers-cli search "attention" --source arxiv --json
```

### Download

```bash
# Specify by source:id format
papers-cli download arxiv:2301.00001

# Specify by URL
papers-cli download https://arxiv.org/abs/2301.00001

# Download multiple papers at once
papers-cli download arxiv:2301.00001 arxiv:2312.12345

# Specify formats (arXiv: pdf, source)
papers-cli download arxiv:2301.00001 --format pdf,source

# Pipe from search results
papers-cli search "attention" --source arxiv --json | papers-cli download --stdin

# Force re-download
papers-cli download arxiv:2301.00001 --force
```

### List / Show / Delete

```bash
# List downloaded papers
papers-cli list
papers-cli list --source arxiv --sort title
papers-cli list "attention" --from 2023

# Show paper details
papers-cli show arxiv:2301.00001

# Delete a paper
papers-cli delete arxiv:2301.00001
papers-cli delete arxiv:2301.00001 --yes  # Skip confirmation
```

### Configuration

```bash
# Generate default config file
papers-cli config init

# View configuration
papers-cli config show
papers-cli config get download-dir

# Modify configuration
papers-cli config set download-dir ~/my-papers
```

## Sources

| Source | Target | API |
|--------|--------|-----|
| `arxiv` | arXiv preprints | arXiv API (Atom Feed) |
| `jstage` | J-STAGE articles | J-STAGE WebAPI |
| `irdb` | Institutional repositories | CiNii Research (IRDB) |

## Storage

| Type | Path |
|------|------|
| Config file | `$XDG_CONFIG_HOME/papers-cli/config.toml` |
| Database | `$XDG_DATA_HOME/papers-cli/papers.db` |
| PDF storage | `~/papers/{source}/` (configurable) |

## Development

```bash
# Build
dotnet build

# Test
dotnet test

# Run
dotnet run --project src/PapersCli.Cli

# Run a single test
dotnet run --project src/PapersCli.Cli.Tests -- --filter "SampleTest"
```

## Tech Stack

- .NET 10 / C# / NativeAOT
- [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework) - CLI framework
- [Spectre.Console](https://spectreconsole.net/) - Terminal UI
- [CsToml](https://github.com/pCYSl5EDgo/CsToml) - TOML parser
- Microsoft.Data.Sqlite - SQLite
- [TUnit](https://github.com/thomhurst/TUnit) - Test framework
