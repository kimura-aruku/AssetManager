# AssetManager

AssetManagerは、購入・ダウンロードした制作素材の保存場所、種類、タグ、入手情報、クレジット条件、利用規約などを一元管理するWindows向けデスクトップアプリケーションです。素材の実ファイルはアプリ内へ取り込まず、ローカルパスと管理情報だけを保存します。

## 動作環境

- Windows 10またはWindows 11（x64）
- 配布版は.NETランタイム、インストーラー、管理者権限を必要としません
- 開発・ソースからのビルドには [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) が必要です

## 配布版の導入と起動

1. `AssetManager-win-x64.zip` を任意のフォルダーへ展開します
2. 展開したファイルをすべて同じフォルダーに置いたまま、`AssetManager.App.exe` を実行します
3. Windowsの警告が表示された場合は、発行元と入手元を確認してから実行します

単一EXEではありません。`AssetManager.App.exe` と同じ場所にあるDLLなども実行に必要です。アプリの更新時は、終了してから配布フォルダー全体を差し替えてください。

## 基本的な使い方

- 「新規」から対象ファイルまたはフォルダーを選び、素材情報を保存します
- ファイル選択時は、素材名と拡張子に一致する種類が空欄の場合だけ自動入力されます
- 左ペインでカラムごとの検索・絞り込み、中央で一覧、右ペインで詳細編集を行います
- 複数セルを選択して`Ctrl+C`／`Ctrl+V`、複数行を選択して右ペインから一括編集できます
- 12項目のライセンス条件とパス状態は一覧の小アイコンに表示され、マウスを重ねると概要・説明や理由を確認できます
- 定型ライセンスを登録しておくと、選択するだけでライセンス条件を一括入力できます
- 「カラム・マスタ管理」からカスタムカラム、種類、購入／入手元、定型ライセンス、タグ分類、タグを管理できます
- 「設定」から起動時パス確認、ライセンス警告日数、データ保存先を変更できます

主なショートカットは`Ctrl+N`（新規）、`Ctrl+S`（保存）、`Ctrl+C`／`Ctrl+V`（セル範囲）、`Ctrl+Z`／`Ctrl+Y`（元に戻す／やり直す）、`Delete`（管理レコード削除）、`F5`（パス確認）です。

## データ保存先

固定アプリ領域は`%LOCALAPPDATA%\kimura-aruku\AssetManager`です。初期状態の管理データは、その配下の`Data`に保存されます。

- 1素材レコードを1つのUTF-8 JSONファイルとして保存します
- ログは固定アプリ領域の`logs`へ保存します
- アンドゥ履歴は起動中だけ保持し、正常終了時または次回起動時に削除します
- 展開した配布フォルダーへユーザーデータを書き込みません
- 設定画面から空のローカルフォルダーへ管理データをコピー・検証して保存先を変更できます

データ保存先変更時に削除対象となるのは、元の管理JSONフォルダーだけです。登録素材の実ファイルはコピー・移動・削除されません。

## 既知の制約

- Windows x64専用です
- 対応する対象パスは固定ローカルドライブです。ネットワークドライブ、UNC、外付けドライブ、クラウド同期フォルダーは正式対応外です
- 素材のプレビュー、実ファイルの自動探索・再リンク、常時監視は行いません
- JSON／CSVインポート・エクスポート、クラウド同期、複数PC同期、暗号化には対応していません
- 管理JSONの直接編集はサポートしません
- 初期版は1つの管理データセットを扱います

## プライバシー

- 素材の実ファイルはアップロードしません
- 利用統計、エラー情報、個人データを外部へ自動送信しません
- エラー詳細はローカルログだけへ記録します

## 開発ビルドとテスト

```powershell
dotnet restore AssetManager.sln
dotnet build AssetManager.sln --configuration Release --no-restore
dotnet test AssetManager.sln --configuration Release --no-build --no-restore
```

実行：

```powershell
dotnet run --project src/AssetManager.App/AssetManager.App.csproj
```

## 自己完結型ReleaseとZIPの作成

リポジトリのルートで次を実行します。

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-release.ps1
```

次の成果物が生成されます。

```text
artifacts/
├─ AssetManager-win-x64/      # 自己完結型の発行フォルダー
└─ AssetManager-win-x64.zip   # 配布用ZIP
```

発行設定は`src/AssetManager.App/Properties/PublishProfiles/PortableWinX64.pubxml`にあります。

## プロジェクト構成

```text
src/
├─ AssetManager.App/             # WPF UI、MVVM、アプリ起動
├─ AssetManager.Application/     # ユースケースと処理手順
├─ AssetManager.Domain/          # モデルとビジネスルール
└─ AssetManager.Infrastructure/  # JSON、ファイルシステム、Windows連携
tests/
├─ AssetManager.UnitTests/
└─ AssetManager.IntegrationTests/
```

仕様・設計・受け入れ項目は[`docs/specification.md`](docs/specification.md)、[`docs/architecture.md`](docs/architecture.md)、[`docs/acceptance-checklist.md`](docs/acceptance-checklist.md)を参照してください。

## ライセンス

ソースコードは[MIT License](LICENSE)で公開します。
