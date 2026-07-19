# AssetManager

AssetManagerは、購入またはダウンロードした制作素材について、実ファイルを取り込まずに保存場所と管理情報を一元管理するWindows向けデスクトップアプリケーションです。

現在はMVPの開発初期段階です。素材レコード、動的カラム、JSON保存、検索、ライセンス条件、ローカルパスの状態などを段階的に実装します。

## 動作環境

- Windows 10またはWindows 11（x64）
- 開発時のみ [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

MVPの配布版は自己完結形式とし、.NETランタイムの事前インストールを不要にする予定です。

## ビルドとテスト

リポジトリのルートで次のコマンドを実行します。

```powershell
dotnet restore AssetManager.sln
dotnet build AssetManager.sln --configuration Debug --no-restore
dotnet test AssetManager.sln --configuration Debug --no-build --no-restore
```

## 実行

```powershell
dotnet run --project src/AssetManager.App/AssetManager.App.csproj
```

現段階では、プロジェクト基盤を確認するための初期画面が表示されます。

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

詳しい仕様と設計は [`docs/specification.md`](docs/specification.md) と [`docs/architecture.md`](docs/architecture.md) を参照してください。

## データとプライバシー

- 素材の実ファイルはコピー、移動、削除、アップロードしません。
- 管理情報はローカルのJSONファイルへ保存します。
- 利用統計、エラー情報、個人データを外部へ自動送信しません。
- リポジトリへ実際の素材情報やローカル固有パスを追加しないでください。

## ライセンス

ソースコードは [MIT License](LICENSE) で公開します。
