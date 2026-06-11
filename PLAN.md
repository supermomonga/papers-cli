# papers-cli MVP Specification

## Overview

arXiv / J-STAGE / CiNii Research から論文を検索・ダウンロード・管理する CLI ツール。

---

## Command Structure

フラット構造。config のみサブコマンドグループ。

```
papers-cli search <query> [options]
papers-cli download <ids...> [options]
papers-cli list [query] [options]
papers-cli show <source:id> [options]
papers-cli delete <source:id> [options]
papers-cli config init
papers-cli config show
papers-cli config get <key>
papers-cli config set <key> <value>
```

---

## Subcommand Details

### search

キーワードで各ソースから論文を検索し、Spectre.Console Table で表示。

```
papers-cli search <query> [--source <source>] [--author <name>] [--from <year>] [--to <year>] [--category <cat>] [--sort-key <field>] [--sort-order <asc|desc>] [--limit <n>] [--page <n>] [--json]
```

- `--source`: 単一ソース指定 (`arxiv`, `jstage`, `irdb`)。未指定時は config の `default-source` を使用。
- `--sort-key`: `relevance` (default) / `date`。`jstage` のみ `title` も対応。
- `--sort-order`: `asc` / `desc`。指定可能な値とデフォルトは source と sort key に依存。
- `--limit`: 1ページあたりの件数。デフォルト 20。
- `--page`: 1始まりのページ番号。デフォルト 1。
- `--json`: 総件数メタデータと `results` 配列を含む JSON オブジェクトを出力（`download --stdin` 連携対応）
- テーブルカラム: Source:ID / Title / Authors / Year / Categories / DL (DL済みフォーマット表示)

### download

論文をダウンロード。複数 ID 指定可能。URL も受け付ける。

```
papers-cli download <ids...> [--format <formats>] [--force] [--stdin] [--json]
```

- `<ids>`: `source:id` 形式 (例: `arxiv:2301.00001`) または URL (例: `https://arxiv.org/abs/2301.00001`)
- `--format`: カンマ区切りでダウンロードするフォーマット指定 (例: `pdf,latex`)。未指定時は PDF のみ。
- `--force`: 既にダウンロード済みでも強制再ダウンロード
- `--stdin`: 標準入力から ID リストを読み取り (`search --json` からパイプ)
- 重複時: スキップ + 通知メッセージ
- PDF 取得不可の場合: エラーとして拒否（DB に保存しない）
- 進捗表示: Spectre.Console Progress バー

### list

ダウンロード済み論文の一覧表示。search と同等のフィルタをローカル DB に適用。

```
papers-cli list [query] [--source <sources>] [--author <name>] [--from <year>] [--to <year>] [--category <cat>] [--sort <field>] [--limit <n>] [--json]
```

- `--sort`: `date` / `downloaded_at` / `title` / `author`
- テーブルカラム: search と同じ

### show

論文の詳細情報を Spectre.Console Panel + Table で表示。

```
papers-cli show <source:id> [--json]
```

表示内容:
- タイトル (Panel ヘッダー)
- Source / ID / Authors / Published / DOI / Journal / Categories / PDF path / URL
- Abstract (折り返し表示)

### delete

ダウンロード済み論文を削除（ファイル + DB レコード）。

```
papers-cli delete <source:id> [--yes] [--json]
```

- デフォルトで確認プロンプト表示
- `--yes`: 確認をスキップ

### config

設定ファイルの管理。別クラスで実装。

```
papers-cli config init    # デフォルト config.toml を生成
papers-cli config show    # 全設定を表示
papers-cli config get <key>
papers-cli config set <key> <value>
```

---

## Paper Sources (MVP)

| Source | API | 対応フォーマット |
|--------|-----|-----------------|
| arXiv | arXiv API (Atom Feed) | PDF, LaTeX source 等 |
| J-STAGE | J-STAGE WebAPI | PDF 等 |
| IRDB | CiNii Research OpenSearch API (dataSourceType=IRDB) | PDF 等 |

各ソースがサポートするフォーマットはすべて対応する。ただし HTML+画像のように複数ファイルで構成されるものは MVP では対応しない。

---

## Storage

### ファイル保存

ソース別ディレクトリ:

```
~/papers/            (config の download-dir で変更可能)
  arxiv/
    2301.00001.pdf
    2301.00001.tar.gz   (LaTeX source)
  jstage/
    abc123.pdf
  cinii/
    xyz789.pdf
```

### SQLite Database

`$XDG_DATA_HOME/papers-cli/papers.db`

```sql
CREATE TABLE papers (
  id INTEGER PRIMARY KEY,
  source TEXT NOT NULL,           -- 'arxiv', 'jstage', 'cinii'
  source_id TEXT NOT NULL,        -- ソース固有 ID
  title TEXT NOT NULL,
  authors TEXT NOT NULL,          -- JSON 配列
  published_at TEXT,              -- ISO 8601
  abstract TEXT,
  url TEXT NOT NULL,              -- 論文ページ URL
  doi TEXT,                       -- DOI (あれば)
  journal TEXT,                   -- ジャーナル名
  categories TEXT,                -- JSON 配列
  created_at TEXT NOT NULL,       -- DB 登録日時
  UNIQUE(source, source_id)
);

CREATE TABLE paper_files (
  id INTEGER PRIMARY KEY,
  paper_id INTEGER NOT NULL REFERENCES papers(id) ON DELETE CASCADE,
  format TEXT NOT NULL,           -- 'pdf', 'latex', etc.
  file_path TEXT NOT NULL,        -- ローカルファイルパス
  source_url TEXT NOT NULL,       -- ダウンロード元 URL
  downloaded_at TEXT NOT NULL,    -- ISO 8601
  UNIQUE(paper_id, format)
);
```

### Config

`$XDG_CONFIG_HOME/papers-cli/config.toml`

```toml
download-dir = "~/papers"
default-source = "arxiv"

[api-keys]
# 将来的な認証付きソース用
```

---

## UI / Output

- **search / list**: Spectre.Console Table
- **show**: Spectre.Console Panel + Table
- **download**: Spectre.Console Progress バー
- **全コマンド**: `--json` オプションで JSON 出力をサポート
- **エラー**: stderr に出力。終了コード 0 (成功) / 1 (エラー)。

---

## Architecture

### Tech Stack

- .NET 10 / C# (latest) / NativeAOT publish (Spectre.Console との互換性は要検証)
- CLI: ConsoleAppFramework
- UI: Spectre.Console
- TOML: CsToml
- DB: Dapper.AOT + Microsoft.Data.Sqlite
- DI: Microsoft.Extensions.DependencyInjection
- Test: TUnit

### Project Structure

```
src/PapersCli.Cli/
  Commands/
    PaperCommands.cs          # search, download, list, show, delete (1 class)
    ConfigCommand.cs          # config init, show, get, set (separate class)
  Sources/
    IPaperSource.cs           # interface
    ArxivSource.cs
    JStageSource.cs
    CiNiiSource.cs
  Models/
    Paper.cs
    PaperFile.cs
    SearchResult.cs
  Data/
    PaperRepository.cs
  Config/
    AppConfig.cs
  Program.cs
```

### DI Registration

```csharp
var services = new ServiceCollection();
services.AddSingleton<IPaperSource, ArxivSource>();
services.AddSingleton<IPaperSource, JStageSource>();
services.AddSingleton<IPaperSource, CiNiiSource>();
services.AddSingleton<PaperRepository>();
// ... HttpClient, Config etc.

var app = ConsoleApp.Create();
app.Add<PaperCommands>();
app.Add("config", app => {
  app.Add<ConfigCommand>();
});
app.Run(args);
```

### HTTP

- 固定回数リトライ: 最大 3 回、1 秒間隔
- 手動実装（外部ライブラリ不使用）

---

## Download Identifier Format

download / show / delete コマンドで使用する論文指定方法:

1. **source:id 形式**: `arxiv:2301.00001`, `jstage:abc123`, `cinii:xyz789`
2. **URL 形式**: `https://arxiv.org/abs/2301.00001`, `https://www.jstage.jst.go.jp/article/...`

URL からソースと ID を自動判定する URL パーサーが必要。

---

## Future Enhancements (Post-MVP)

- open コマンド (PDF ビューアで開く)
- タグ/ラベル機能
- メモ機能
- Semantic Scholar / DOI 直接指定
- HTML+画像フォーマット対応
- インタラクティブモード (search → 番号指定 → download)
