AnythingBookmark
============
The English version of README.md is available [here](/README.md).

概要
---
AnythingBookmark は Unity のアセットだけでなく様々な要素をブックマーク出来るエディタ拡張ウィンドウです。  

![](https://img.shields.io/badge/Unity-2017.1%20or%20later-lightgrey)
[![](https://img.shields.io/badge/license-MIT-orange)](https://github.com/zenigane138/AnythingBookmark/blob/main/LICENSE.md)
[![](https://img.shields.io/badge/readme-English-red)](/README_ja.md)
[![openupm](https://img.shields.io/npm/v/com.okanegames.anythingbookmark?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.okanegames.anythingbookmark/)
[![](https://img.shields.io/badge/Follow-FFFFFF.svg?logo=twitter&style=flat)](https://twitter.com/intent/follow?screen_name=zenigane138)

![image](https://user-images.githubusercontent.com/36072156/232230899-52835490-8a8b-4ad8-8c1d-b2d1a5d78a67.png)

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

動画  
---
https://user-images.githubusercontent.com/36072156/232078688-7189e87f-be92-434d-b06e-f3053e5a24b4.mp4

動作環境
---
- 必須
  - Unity 2017.1 以降
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
