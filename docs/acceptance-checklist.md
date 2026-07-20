# MVP受け入れチェックリスト

## 自動確認との対応

| 受け入れ対象 | 主な自動確認 | 状態 |
|---|---|---|
| カラム定義・型検証 | `FieldDefinitionTests`、`FieldValueTests`、`DomainModelValidatorTests` | 済 |
| レコード登録・編集・削除 | `RecordApplicationServiceTests`、`JsonAssetManagerDataStoreTests` | 済 |
| 種類・タグ・カスタムカラム | `CatalogApplicationServiceTests`、`FieldApplicationServiceTests` | 済 |
| パス登録・重複・存在確認 | `WindowsPathTests`、`PathCheckServiceTests`、`PhysicalWindowsPathFileSystemTests` | 済 |
| 検索・保存済み条件・100件追加読み込み | `RecordSearchEngineTests`、`RecordSearchSessionTests`、`JsonViewConfigurationStoreTests` | 済 |
| 一括編集・矩形コピー／貼り付け | `GridSelectionNormalizerTests`、`GridBatchEditPlannerTests`、`GridClipboardServiceTests` | 済 |
| アンドゥ／リドゥ | `UndoRedoServiceTests`、`FileUndoHistoryTests` | 済 |
| ライセンス警告・状態アイコン | `LicenseWarningTests`、`RecordIndicatorEvaluatorTests` | 済 |
| 12項目のライセンス条件・旧データ移行・定型ライセンスの保存と適用 | `RecordIndicatorEvaluatorTests`、`DataSetInitializerTests`、`FieldEditorViewModelTests`、`CoreRepositoryTests` | 済 |
| 設定保存・データルート変更 | `JsonAppSettingsStoreTests`、`DataRootMigrationServiceTests` | 済 |
| 不正JSON・型不一致・リンク切れ・権限不足 | `DataSetInitializerTests`、`DomainModelValidatorTests`、`PathCheckServiceTests` | 済 |
| 複数ファイル更新のロールバック | `TransactionAndMigrationTests`、`AtomicJsonFileStoreTests` | 済 |
| 25・250・1000件の起動・検索・追加読み込み | `ScaleAcceptanceTests` | 済 |
| 外部送信ライブラリを参照しない | `LayerDependencyTests.CoreAssembliesDoNotReferenceExternalTransmissionLibraries` | 済 |

## 手動確認項目

以下はWPFの見た目、マウス操作、OSダイアログ、操作感を確認するため、Release版で実施する。

1. 起動後に左検索・中央一覧・右詳細の3ペインが崩れず表示される
2. 新規レコードでファイルを選択すると、パス・素材名・種類が空欄時だけ自動入力される
3. 日付をカレンダーから選択でき、日本円入力では数値以外が拒否され「円」が表示される
4. 種類・タグ・状態・お気に入りと各カラム条件で検索できる
5. 非連続行と連続カラム範囲を選択し、コピー・貼り付け・一括編集・アンドゥが期待どおり動作する
6. 12個のライセンス条件が指定順で表示され、詳細ペイン、定型ライセンス管理、一覧アイコンのツールチップに概要と説明が表示される
7. カラム・種類・タグ・タグ分類・定型ライセンスの管理画面で追加・編集・削除できる
8. 設定画面で起動時パス確認と警告日数を保存できる
9. 空の一時フォルダーへのデータ保存先変更を行い、再起動後に新しい保存先が使われる
10. ボタン、右クリックメニュー、`Ctrl+N`、`Ctrl+S`、`Ctrl+C`、`Ctrl+V`、`Ctrl+Z`、`Ctrl+Y`、`Delete`、`F5`を確認する
11. 管理レコードを削除しても、登録した素材の実ファイルが残る
12. 定型ライセンスを選択すると全ライセンス条件が反映され、条件を手動変更すると定型選択が解除される

## 公開前監査

- Git追跡ファイルに実データルート、ログ、アンドゥ履歴、トランザクション一時データを含めない
- 個人ユーザー名、ローカル作業パス、秘密鍵、APIキー、クライアントシークレット、アクセストークンを含めない
- エラー詳細はローカルログだけへ記録し、外部送信しない
- Release成果物へテスト用データやローカル設定を含めない
