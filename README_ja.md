AnythingBookmark
============
The English version of README.md is available [here](/README.md).

概要
---
AnythingBookmark は Unity のアセットだけでなく様々な要素をブックマーク出来るエディタ拡張ウィンドウです。  
https://video.twimg.com/ext_tw_video/1635634642534694913/pu/vid/1280x720/OM6WocdkImxRnDPg.mp4

登録可能な要素
---
- Assets/ フォルダ以下の各種アセット
- プロジェクト外のフォルダ
- プロジェクト外のファイル
- MenuItem のショートカット (BuildSettings や Profiler など)
- SceneView のカメラの座標や向き
- Hierarchy ウィンドウの GameObject

機能
---
- アセットのドラッグアンドドロップによる登録が可能
- お気に入り登録
- お気に入りのみ表示するタブ
- ブックマークの種類別のタブ
- ソートやフィルタ
- ブックマークデータ(.abd)のエクスポート/インポート機能

動作環境
---
- 必須
  - C# 6.0 以降
- 推奨
  - Unity 2020.1 以降

インストール方法
---
- unitypackage
  - Releases ページから unitypackage をダウンロード
  - https://github.com/zenigane138/AnythingBookmark/releases
- PackageManager
  - Window -> PackageManager でウィンドウを開く
  - +▼ボタンから "Add package from git URL..." を選択し下記URLを設定
  - https://github.com/zenigane138/AnythingBookmark.git?path=Assets/AnythingBookmark

使い方
---
- ウィンドウを Unity のメニューから開いて下さい。
  - OkaneGames -> AnythingBookmark
  - Window -> OkaneGames -> AnythingBookmark
- アセットなどを追加ボタンもしくはドラッグアンドドロップで登録

データの保存先
---
EditorUserSettings を使用しています。  
ブックマークデータを他のプロジェクトや他人と共有したい場合は、エクスポート/インポート機能を使ってください。  
プロジェクト外のフォルダとファイルはフルパスで管理されている点にはご注意下さい。

License
---
[LICENSE.md](/LICENSE.md) をご確認下さい。
