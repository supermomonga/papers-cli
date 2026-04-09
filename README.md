# papers-cli

arXiv / J-STAGE / IRDB（機関リポジトリ）から論文を検索・ダウンロード・管理する CLI ツール。

## Features

- 複数ソースの横断検索（arXiv, J-STAGE, IRDB）
- PDF ダウンロード＆メタデータの SQLite 管理
- Spectre.Console によるリッチな表示（テーブル、パネル、プログレスバー）
- `--json` 出力によるスクリプト連携
- XDG Base Directory Specification 準拠

## Install

```bash
# ビルド
dotnet build

# NativeAOT で単一バイナリとして publish
dotnet publish src/PapersCli.Cli -c Release
```

## Usage

### 検索

```bash
# 全ソースから検索（デフォルト）
papers-cli search "attention mechanism"

# ソースを指定して検索
papers-cli search "深層学習" --source jstage
papers-cli search "transformer" --source arxiv --category cs.AI --from 2023

# フィルタオプション
papers-cli search "強化学習" --author "山田" --from 2020 --to 2024 --sort date --limit 10

# JSON 出力
papers-cli search "attention" --source arxiv --json
```

### ダウンロード

```bash
# source:id 形式で指定
papers-cli download arxiv:2301.00001

# URL で指定
papers-cli download https://arxiv.org/abs/2301.00001

# 複数同時ダウンロード
papers-cli download arxiv:2301.00001 arxiv:2312.12345

# フォーマット指定（arXiv: pdf, source）
papers-cli download arxiv:2301.00001 --format pdf,source

# 検索結果からパイプでダウンロード
papers-cli search "attention" --source arxiv --json | papers-cli download --stdin

# 強制再ダウンロード
papers-cli download arxiv:2301.00001 --force
```

### 一覧・詳細・削除

```bash
# ダウンロード済み論文の一覧
papers-cli list
papers-cli list --source arxiv --sort title
papers-cli list "attention" --from 2023

# 論文の詳細表示
papers-cli show arxiv:2301.00001

# 論文の削除
papers-cli delete arxiv:2301.00001
papers-cli delete arxiv:2301.00001 --yes  # 確認スキップ
```

### 設定

```bash
# デフォルト設定ファイルを生成
papers-cli config init

# 設定の確認
papers-cli config show
papers-cli config get download-dir

# 設定の変更
papers-cli config set download-dir ~/my-papers
```

## Sources

| ソース | 検索対象 | API |
|--------|----------|-----|
| `arxiv` | arXiv プレプリント | arXiv API (Atom Feed) |
| `jstage` | J-STAGE 掲載論文 | J-STAGE WebAPI + CiNii Research |
| `irdb` | 大学機関リポジトリ | CiNii Research (IRDB) |

## Storage

| 種別 | パス |
|------|------|
| 設定ファイル | `$XDG_CONFIG_HOME/papers-cli/config.toml` |
| データベース | `$XDG_DATA_HOME/papers-cli/papers.db` |
| PDF 保存先 | `~/papers/{source}/` (設定で変更可能) |

## Development

```bash
# Build
dotnet build

# Test
dotnet test

# Run
dotnet run --project src/PapersCli.Cli

# 単一テスト実行
dotnet run --project src/PapersCli.Cli.Tests -- --filter "SampleTest"
```

## Tech Stack

- .NET 10 / C# / NativeAOT
- [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework) - CLI フレームワーク
- [Spectre.Console](https://spectreconsole.net/) - ターミナル UI
- [CsToml](https://github.com/pCYSl5EDgo/CsToml) - TOML パーサ
- Microsoft.Data.Sqlite - SQLite
- [TUnit](https://github.com/thomhurst/TUnit) - テストフレームワーク
