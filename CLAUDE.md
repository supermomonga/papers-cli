# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

papers-cli は arXiv や J-STAGE などの論文掲載サイトから論文を検索・ダウンロード・管理する CLI ツール。ダウンロード済み論文のメタデータは SQLite で管理する。

## Tech Stack

- .NET 10 / C# (latest) / NativeAOT publish
- CLI フレームワーク: ConsoleAppFramework (source generator ベース)
- UI: Spectre.Console (Table, Panel, Progress)
- DB: Microsoft.Data.Sqlite (raw ADO.NET)
- TOML パーサ: CsToml
- DI: Microsoft.Extensions.DependencyInjection
- JSON: System.Text.Json (source-generated context, AOT 対応)
- テスト: TUnit (Microsoft.Testing.Platform)
- 設定ファイルパス: XDG Base Directory Specification に準拠 (`$XDG_CONFIG_HOME/papers-cli/config.toml` 等)
- DB パス: `$XDG_DATA_HOME/papers-cli/papers.db`

## Build & Test Commands

```bash
dotnet build                              # ビルド
dotnet test                               # テスト実行（MTP mode via global.json）
dotnet run --project src/PapersCli.Cli    # 実行（--project はフォルダまでの指定で可）
```

単一テスト実行:
```bash
dotnet run --project src/PapersCli.Cli.Tests -- --filter "SampleTest"
```

## Architecture

- `src/PapersCli.Cli/` — CLI 本体
  - `Commands/` — PaperCommands (search/download/list/show/delete), ConfigCommand (config init/show/get/set)
  - `Sources/` — IPaperSource インターフェース + ArxivSource, JStageSource, CiNiiSource
  - `Models/` — Paper, PaperFile, SearchResult
  - `Data/` — PaperRepository (SQLite)
  - `Config/` — AppConfig (TOML)
  - `Json/` — PapersJsonContext (AOT 対応 source-generated JSON)
- `src/PapersCli.Cli.Tests/` — テストプロジェクト
- `Directory.Build.props` — バージョン等の共通プロパティ
- `PapersCli.slnx` — ソリューションファイル（ルートで `dotnet build` / `dotnet test` 可能）

ConsoleAppFramework のコマンド定義は `app.Add<T>()` でクラスを登録し、`[Command("name")]` 属性でサブコマンドを定義する。パラメータの XML doc コメントがヘルプテキストになる。config サブコマンドは `app.Add<ConfigCommand>("config")` でグループ化。

## Conventions

- 実行ファイル名は kebab-case (`papers-cli`)。`<AssemblyName>` で指定済み。
- NativeAOT + self-contained で単一バイナリとして publish する。
- リリースは GHA workflow (`version-bump.yml` → `release.yml`) で管理。
