using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OkaneGames.AnythingBookmark.Editor
{
    /// <summary>
    /// だいたいブックマークできるエディタ拡張ウィンドウ 
    /// </summary>
    public class AnythingBookmark : EditorWindow
    {
        private static readonly string WindowTitle = "AnythingBookmark";
        private static readonly Vector2 WindowMinSize = new Vector2(350, 200);

        private static readonly string BookmarkEditorUserSettingsKey = WindowTitle + "_BookmarkJSON";
        private static readonly string SortTypeEditorUserSettingsKey = WindowTitle + "_SortType";
        private static readonly string SettingsEditorUserSettingsKey = WindowTitle + "_Settings";

        private static readonly string SeparatorForSpecialPath = "/";

        //private GUID _windowGuid;
        private Event _event;
        private Vector2 _scroll;
        private Color _tempBGColor;
        private string _inputtedText;

        // タブのインデックス管理をしている TabController はシリアライズ不可な作りにしたのでリストア用の変数を別途持つ
        private int _tabControllerCurrentIndexForRestore;

        private Setting _setting = null;
        private Setting _Setting
        {
            get
            {
                if (_setting == null)
                {
                    _setting = Setting.Load();
                }
                return _setting;
            }
        }

        private List<Bookmark> _mainBookmarkList = null;
        private List<Bookmark> _MainBookmarkList
        {
            get
            {
                if (_mainBookmarkList == null)
                {
                    _mainBookmarkList = LoadBookmarkList();
                }
                return _mainBookmarkList;
            }
        }
        
        private List<Bookmark> _favBookmarkList = null;
        private List<Bookmark> _FavBookmarkList
        {
            get
            {
                if (_favBookmarkList == null)
                {
                    _favBookmarkList = UpdateFavBookmarkList();
                }
                return _favBookmarkList;
            }
        }

        private List<Bookmark> _subTabBookmarkList = null;
        private List<Bookmark> _SubTabBookmarkList
        {
            get
            {
                if (_subTabBookmarkList == null)
                {
                    _subTabBookmarkList = UpdateSubTabBookmarkList();
                }
                return _subTabBookmarkList;
            }
        }


        private SortHelper _sortHelper = null;
        private SortHelper _SortHelper
        {
            get
            {
                if (_sortHelper == null)
                {
                    _sortHelper = new SortHelper();
                }
                return _sortHelper;
            }
        }

        private ObjectTypeFilterHelper _filterHelper = null;
        private ObjectTypeFilterHelper _FilterHelper
        {
            get
            {
                if (_filterHelper == null)
                {
                    _filterHelper = new ObjectTypeFilterHelper();
                }
                return _filterHelper;
            }
        }

        private TabController _tabController = null;
        private TabController _TabController
        {
            get
            {
                if (_tabController == null)
                {
                    _tabController = new TabController();
                }
                return _tabController;
            }
        }

#if UNITY_2017_1_OR_NEWER
        [MenuItem("Window/OkaneGames/", priority = Int32.MaxValue)]
#endif
        [MenuItem("Window/OkaneGames/AnythingBookmark")]
        [MenuItem("OkaneGames/AnythingBookmark")]
        private static void CreateWindow()
        {
            var window = CreateInstance<AnythingBookmark>();
            //window._windowGuid = GUID.Generate();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = WindowMinSize;
            window._tabControllerCurrentIndexForRestore = -1;
            window.Show();
        }

        private void OnDestroy()
        {
            // ウィンドウを閉じる時だけ呼ばれる
        }

        private void OnDisable()
        {
            // ウィンドウを閉じる時だけでなくプレイモードに入る時も呼ばれる
        }

        private void OnGUI()
        {
            _event = Event.current;

            // タブID変更検出時の処理の登録
            if (_TabController.OnTabIdChanged == null)
            {
                _TabController.OnTabIdChanged += (currentId, newId) =>
                {
                    if (newId == Tab.Id.Main)
                    {
                        _SortHelper.Execute(_MainBookmarkList);
                    }
                    else if (newId == Tab.Id.Fav)
                    {
                        UpdateFavBookmarkList();
                    }
                    else if (newId == Tab.Id.Settings)
                    {
                    }
                    //else if (newId is Tab.Id.Assets or Tab.Id.ExternalDir or Tab.Id.ExternalFile or Tab.Id.MenuItem or Tab.Id.SceneViewCamera or Tab.Id.GameObjectInHierarchy)
                    else if (newId == Tab.Id.Assets || newId == Tab.Id.ExternalDir || newId == Tab.Id.ExternalFile 
                        || newId == Tab.Id.MenuItem || newId == Tab.Id.SceneViewCamera || newId == Tab.Id.GameObjectInHierarchy)
                    {
                        UpdateSubTabBookmarkList();
                    }
                };
            }

            // タブインデックスのリストア処理
            if(_TabController.GetCurrentIndex() == -1)
            {
                // _tabControllerCurrentIndexForRestore も -1 なのは起動時、エディタ再生開始時は 0 以上の値
                var restoredIndex = _tabControllerCurrentIndexForRestore != -1 ? _tabControllerCurrentIndexForRestore : 0;
                _TabController.ChangeIndexForce(restoredIndex);
                _TabController.ChangeTab(_TabController.GetCurrentTabId());
                if(_tabControllerCurrentIndexForRestore != -1) Debug.Log("[" + WindowTitle + "] Restored!! index:" + _tabControllerCurrentIndexForRestore);
            }

            // タブ(Toolbar)の描画と結果受け取り
            _TabController.ReceiveToolbarResult(
                GUILayout.Toolbar(_TabController.GetCurrentIndex(), _TabController.GetGUIContents(), EditorStyles.toolbarButton)
                );
            _tabControllerCurrentIndexForRestore = _TabController.GetCurrentIndex();


            EditorGUILayout.Space();

            // 各種タブのコンテンツ
            var tabId = _TabController.GetCurrentTabId();
            if (tabId == Tab.Id.Main)
            {
                DrawDropArea();
                DrawAddBookmarkButton();
                DrawMain(_MainBookmarkList);
            }
            else if (tabId == Tab.Id.Fav)
            {
                DrawDropArea();
                DrawMain(_FavBookmarkList);
            }
            else if (tabId == Tab.Id.Settings)
            {
                DrawDropArea();
                DrawSettings();
                DrawSettingsFooter();
            }
            else if (tabId == Tab.Id.Assets || tabId == Tab.Id.ExternalDir || tabId == Tab.Id.ExternalFile || tabId == Tab.Id.MenuItem || tabId == Tab.Id.SceneViewCamera || tabId == Tab.Id.GameObjectInHierarchy)
            {
                DrawDropArea();
                DrawMain(_SubTabBookmarkList);
            }
        }

        private void DrawDropArea()
        {
            // 現状のGUIとドラッグ&ドロップ処理は競合していないのでドロップ可能エリアの制限はなし
            // 必要になったら Rect の Contains にマウス座標を渡して判定すればOK

            // メインタブとそれ以外で分ける
            var isMainTab = _TabController.GetCurrentTabId() == Tab.Id.Main;

            // メインのみ
            if (isMainTab) EditorGUILayout.LabelField("Bookmarks can be added with a button click or drag-and-drop.");

            if (_event.type == EventType.DragUpdated)
            {
                // メイン以外のタブにドラッグしてきた時は強制的にメインへ移動
                if (!isMainTab) _TabController.ChangeTab(Tab.Id.Main);

                // ドラッグ時のマウスカーソル変更
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                Event.current.Use();
            }
            else if (_event.type == EventType.DragPerform)
            {
                if (!isMainTab) return;

                DragAndDrop.AcceptDrag();

                // ドロップされた物は、パスとして処理を試みた後に、オブジェクトとして処理を試みる流れにする
                // 「パスとして処理された物はオブジェクトとしては処理してはいけない」にする予定だったけど
                // 「オブジェクトで処理するのはヒエラルキー内のゲームオブジェクトのみ」にした事で不要になった
                foreach (var path in DragAndDrop.paths)
                {
                    // プロジェクト内のパスは Assets で始まる
                    if (path.StartsWith("Assets"))
                    {
                        BookmarkAsset(AssetDatabase.AssetPathToGUID(path));
                    }
                    // 外部ディレクトリ
                    //else if(File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                    else if ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        BookmarkExternalDirectory(path);
                    }
                    // 外部ファイル
                    else
                    {
                        BookmarkExternalFile(path);
                    }
                    //Debug.Log(path);
                }
                foreach (var unityObject in DragAndDrop.objectReferences)
                {
                    //Debug.Log(unityObject);

                    // .unityで終わっていたら（シーンのパスが取れていたら）Hierarchy上のオブジェクトであると判定する
                    var path = AssetDatabase.GetAssetOrScenePath(unityObject);
                    if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity"))
                    {
                        if (unityObject.GetType().ToString() == "UnityEngine.GameObject")
                        {
                            // ヒエラルキー上のゲームオブジェクト確定
                            BookmarkGameObjectInHierarchy(GetGameObjectSpecialPath(unityObject as GameObject, SeparatorForSpecialPath));
                        }
                    }
                }
                Event.current.Use();

                // 最後にまとめて一度だけセーブ
                SaveBookmarkList();
                _SortHelper.Execute(_MainBookmarkList);
            }
        }

        private void DrawAddBookmarkButton()
        {
            GUILayout.BeginHorizontal();
            {
                var content = new GUIContent("Selected\nAssets", "Bookmark selected asset in ProjectWindow.");
                if (GUILayout.Button(content, GUILayout.Width(60), GUILayout.Height(40)))
                {
                    OnClickAddBookmarkAssetButton();
                }

                ChangeBackgroundColor(Color.yellow);
                var content2 = new GUIContent("External\nDir", "Bookmark external directory.");
                if (GUILayout.Button(content2, GUILayout.Width(55), GUILayout.Height(40)))
                {
                    OnClickAddBookmarkExternalDirectoryButton();
                }
                ReturnBackgroundColor();

                ChangeBackgroundColor(Color.cyan);
                var content3 = new GUIContent("External\nFile", "Bookmark external file.");
                if (GUILayout.Button(content3, GUILayout.Width(55), GUILayout.Height(40)))
                {
                    OnClickAddBookmarkExternalFileButton();
                }
                ReturnBackgroundColor();

                ChangeBackgroundColor(Color.green);
                var content4 = new GUIContent("Menu\nItem", "(Add Button Only)\nBookmark Unity menu item.");
                if (GUILayout.Button(content4, GUILayout.Width(45), GUILayout.Height(40)))
                {
                    OnClickAddBookmarkMenuItemButton();
                }
                ReturnBackgroundColor();

                ChangeBackgroundColor(Color.magenta);
                var content5 = new GUIContent("SceneView\nCamera", "(Add Button Only)\nBookmark Camera in SceneView.");
                if (GUILayout.Button(content5, GUILayout.Width(75), GUILayout.Height(40)))
                {
                    OnClickAddBookmarkSceneViewCameraButton();
                }
                ReturnBackgroundColor();

                ChangeBackgroundColor(Color.red);
                var content6 = new GUIContent("GameObj in\nHierarchy", "[Experimental]\nBookmark GameObject in HierarchyWindow.\nGameObject names containing / will not work.");
                if (GUILayout.Button(content6, GUILayout.Width(75), GUILayout.Height(40)))
                {
                    OnClickAddBookmarkGameObjectInHierarchyButton();
                }
                ReturnBackgroundColor();

            }
            GUILayout.EndHorizontal();
        }

        private void DrawMain(List<Bookmark> bookmarkList)
        {
            // ソートUI
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Order by", GUILayout.Width(50));
                var index = EditorGUILayout.Popup(_SortHelper.Index, _SortHelper.DisplayNames, GUILayout.Width(155));
                // 別の種類が選択された時のみソート
                if (index != _SortHelper.Index)
                {
                    _SortHelper.Index = index;
                    _SortHelper.Execute(bookmarkList);
                }
                // 起動時は一度のみソート
                if (!_SortHelper.WasCalledExecuteOnStartup)
                {
                    _SortHelper.Execute(bookmarkList);
                    _SortHelper.WasCalledExecuteOnStartup = true;
                }
            }
            GUILayout.EndHorizontal();

            // タブがUnityAssetsの場合のみ
            if (_TabController.GetCurrentTabId() == Tab.Id.Assets)
            {
                // フィルターUI
                GUILayout.BeginHorizontal();
                {
                    //_FilterHelper
                    EditorGUILayout.LabelField("Filter ", GUILayout.Width(50));
                    //var index = EditorGUILayout.Popup(_FilterHelper.Index, _FilterHelper.DisplayNames, GUILayout.Width(155));
                    var mask = EditorGUILayout.MaskField(_FilterHelper.Mask, _FilterHelper.DisplayNames, GUILayout.Width(155));
                    // 別の種類が選択された時のみソート
                    if (mask != _FilterHelper.Mask)
                    {
                        _FilterHelper.Mask = mask;
                        UpdateSubTabBookmarkList();
                        //_SortHelper.Execute(bookmarkList);
                    }
                }
                GUILayout.EndHorizontal();
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            {
                // foreach 実行中の削除予約用リスト
                // ※ Repaint 挙動を弄っていないエディタ拡張ウィンドウに限って言えば削除予約なんて使わなくても
                // ※ foreach 実行中に削除されたら break して次の Repaint に任せてしまってもほとんどのケースで問題にならない
                List<Bookmark> removeList = null;

                foreach (var bookmark in bookmarkList)
                {
                    GUILayout.BeginHorizontal();
                    {
                        // 行単位の描画処理
                        var hasPushedRemove = DrawRow(bookmark);

                        // 削除ボタンが押されていたらリストに追加
                        if (hasPushedRemove)
                        {
                            if (removeList == null) removeList = new List<Bookmark>();
                            removeList.Add(bookmark);
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                // 削除予約実行
                if (removeList != null)
                {
                    foreach (var target in removeList) RemoveBookmark(target);
                    SaveBookmarkList();
                    if (_TabController.GetCurrentTabId() == Tab.Id.Fav) UpdateFavBookmarkList();
                    else if (_TabController.GetCurrentTabId() != Tab.Id.Main) UpdateSubTabBookmarkList();
                }
            }
            GUILayout.EndScrollView();
        }

        private bool DrawRow(Bookmark bookmark)
        {
            // 削除ボタンはブックマークの内容を問わず共通で描画
            bool hasPushedRemove = false;
            if (GUILayout.Button(new GUIContent("-", "Remove bookmark."), GUILayout.ExpandWidth(false)))
            {
                hasPushedRemove = true;
            }

            // お気に入り関連
            var favButtonStyle = new GUIStyle(GUI.skin.button);
            if (bookmark.fav == 0)
            {
                favButtonStyle.fontStyle = FontStyle.Bold;
                if (GUILayout.Button(new GUIContent("☆", "Add fav."), favButtonStyle, GUILayout.ExpandWidth(false)))
                {
                    bookmark.fav = 1;
                    SaveBookmarkList();
                }
            }
            else
            {
                favButtonStyle.normal.textColor = Color.yellow;
                favButtonStyle.fontStyle = FontStyle.Bold;
                if (GUILayout.Button(new GUIContent("★", "Remove fav."), favButtonStyle, GUILayout.ExpandWidth(false)))
                {
                    bookmark.fav = 0;
                    SaveBookmarkList();
                }
            }

            // ブックマーク描画
            DrawBookmark(bookmark);

            return hasPushedRemove;
        }

        private void DrawBookmark(Bookmark bookmark)
        {
            // タイプ別でブックマーク描画

            if (bookmark.type == Bookmark.Type.Asset)
            {
                if (GUILayout.Button(new GUIContent("！", "Highlighting asset."), GUILayout.ExpandWidth(false)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(bookmark.path);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                    else Debug.Log("[" + WindowTitle + "] Not found Asset. Path:" + bookmark.path);
                }

                var content = new GUIContent(bookmark.name, AssetDatabase.GetCachedIcon(bookmark.path), bookmark.path);
                var style = new GUIStyle(GUI.skin.button);
                style.alignment = TextAnchor.MiddleLeft;
                if (GUILayout.Button(content, style, GetMaxWidth(), GUILayout.Height(18)))
                {
                    OnClickBookmarkCommon(bookmark);

                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(bookmark.path);
                    if (asset != null) OpenAsset(bookmark);
                    else Debug.Log("[" + WindowTitle + "] Not found Asset. Path:" + bookmark.path);
                }
            }
            else if (bookmark.type == Bookmark.Type.ExternalDir)
            {
                ChangeBackgroundColor(Color.yellow);
                var style = new GUIStyle(GUI.skin.button);
                style.alignment = TextAnchor.MiddleLeft;
                if (GUILayout.Button(new GUIContent(bookmark.path, bookmark.path + "\n\nLeft-Click:Open in associated applications.\nRight-Click:Copy path to clipboard."), style, GetMaxWidth(68)))
                {
                    if (Event.current.button == 0)
                    {
                        OnClickBookmarkCommon(bookmark);
                        if (Directory.Exists(bookmark.path)) OpenExternalDirectory(bookmark.path);
                        else Debug.Log("[" + WindowTitle + "] Not found directory. Path:" + bookmark.path);

                    }
                    else if (Event.current.button == 1)
                    {
                        EditorGUIUtility.systemCopyBuffer = bookmark.path;
                    }
                }
                ReturnBackgroundColor();
            }
            else if (bookmark.type == Bookmark.Type.ExternalFile)
            {
                ChangeBackgroundColor(Color.cyan);
                var style = new GUIStyle(GUI.skin.button);
                style.alignment = TextAnchor.MiddleLeft;
                if (GUILayout.Button(new GUIContent(bookmark.path, bookmark.path + "\n\nLeft-Click:Open in associated applications.\nRight-Click:Copy path to clipboard."), style, GetMaxWidth(68)))
                {
                    if (Event.current.button == 0)
                    {
                        OnClickBookmarkCommon(bookmark);
                        if (File.Exists(bookmark.path)) OpenExternalFile(bookmark.path);
                        else Debug.Log("[" + WindowTitle + "] Not found file. Path:" + bookmark.path);
                    }
                    else if (Event.current.button == 1)
                    {
                        EditorGUIUtility.systemCopyBuffer = bookmark.path;
                    }
                }
                ReturnBackgroundColor();
            }
            else if (bookmark.type == Bookmark.Type.MenuItem)
            {
                ChangeBackgroundColor(Color.green);
                var style = new GUIStyle(GUI.skin.button);
                style.alignment = TextAnchor.MiddleCenter;
                if (GUILayout.Button(new GUIContent(bookmark.path, bookmark.path+"\n\nLeft-Click:Open window.\nRight-Click:Copy path to clipboard."), style, GetMaxWidth(68)))
                {
                    if (Event.current.button == 0)
                    {
                        OnClickBookmarkCommon(bookmark);
                        var result = EditorApplication.ExecuteMenuItem(bookmark.path);
                        if (!result) Debug.Log("[" + WindowTitle + "] Not found Unity MenuItem path. Path:" + bookmark.path);
                    }
                    else if (Event.current.button == 1)
                    {
                        EditorGUIUtility.systemCopyBuffer = bookmark.path;
                    }
                }
                ReturnBackgroundColor();
            }
            else if (bookmark.type == Bookmark.Type.SceneViewCamera)
            {
                ChangeBackgroundColor(Color.magenta);
                var style = new GUIStyle(GUI.skin.button);
                style.alignment = TextAnchor.MiddleLeft;
                if (GUILayout.Button(new GUIContent(bookmark.path, "Saved values are reflected in SceneViewCamera."), style, GetMaxWidth(68)))
                {
                    OnClickBookmarkCommon(bookmark);
                    var sceneViewCamera = JsonUtility.FromJson<SceneViewCamera>(bookmark.serialized);
                    var sceneView = SceneView.lastActiveSceneView;
                    sceneView.pivot = sceneViewCamera.pivot;
                    sceneView.rotation = sceneViewCamera.rotation;
                    sceneView.size = sceneViewCamera.size;
                }
                ReturnBackgroundColor();
            }
            else if (bookmark.type == Bookmark.Type.GameObjectInHierarchy)
            {
                if (GUILayout.Button(new GUIContent("！", "Highlighting GameObject."), GUILayout.ExpandWidth(false)))
                {
                    var go = FindGameObjectBySpecialPath(bookmark.path, SeparatorForSpecialPath);
                    // ヒエラルキー上のゲームオブジェクトだけ PingObject が反応したりしなかったりする。最小構成でも起きたのでUnity側のバグ？
                    if (go != null) EditorGUIUtility.PingObject(go);
                    else Debug.Log("[" + WindowTitle + "] Not found GameObject in Hierarchy. Path:" + bookmark.path);
                }

                ChangeBackgroundColor(Color.red);
                var style = new GUIStyle(GUI.skin.button);
                style.alignment = TextAnchor.MiddleLeft;
                if (GUILayout.Button(new GUIContent(bookmark.path, bookmark.path + "\n\nSelect gameObject."), style, GetMaxWidth()))
                {
                    OnClickBookmarkCommon(bookmark);
                    var go = FindGameObjectBySpecialPath(bookmark.path, SeparatorForSpecialPath);
                    if (go != null) Selection.activeGameObject = go;
                    else Debug.Log("[" + WindowTitle + "] Not found GameObject in Hierarchy. Path:" + bookmark.path);
                }
                ReturnBackgroundColor();
            }
        }

        private void OpenExternalDirectory(string path)
        {
            // RevealInFinder()で開こうとするとエクスプローラー系ソフトによっては気持ち悪い挙動になったので不採用
            //EditorUtility.RevealInFinder(path);
            System.Diagnostics.Process.Start(path);
        }

        private void OpenExternalFile(string path)
        {
            var tempCurrentDirectory = System.Environment.CurrentDirectory;
            //var ext = Path.GetExtension(path);
            // 当初カレントディレクトリ操作はバッチファイル限定にしてたけど問題が起きるまで全部通す
            //if (ext == ".bat" || ext == ".ps1")
            {
                //Debug.Log(Path.GetDirectoryName(path));
                System.Environment.CurrentDirectory = Path.GetDirectoryName(path);
            }

            // OpenWithDefaultApp()でも普通に動きはする
            //EditorUtility.OpenWithDefaultApp(path);
            System.Diagnostics.Process.Start(path);

            //if (ext == ".bat" || ext == ".ps1")
            {
                System.Environment.CurrentDirectory = tempCurrentDirectory;
            }
        }

        private GUILayoutOption GetMaxWidth(float sub = 95f)
        {
            return GUILayout.MaxWidth(position.width - sub);
        }

        private void DrawSettings()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            {
                DrawSettingsTabSettings();
                DrawSeparator();
                DrawSettingsExportSettings();
                DrawSeparator();
                DrawSettingsImportSettings();
                DrawSeparator();
                EditorGUILayout.Space();

                // セーブデータ全削除
                ChangeBackgroundColor(Color.red);
                if (GUILayout.Button(new GUIContent("Remove All Save Data", "Remove all " + WindowTitle + " related save data.")))
                {
                    if (EditorUtility.DisplayDialog("Final Confirmation", "Are you sure you want me to remove it?", "Remove", "Cancel"))
                    {
                        RemoveAllSaveData();
                        // 一応ダイアログ出してから終了
                        if (EditorUtility.DisplayDialog("Remove Result", "Successed!!\n\nExit " + WindowTitle + ".", "OK")) Close();
                    }
                }
                ReturnBackgroundColor();
            }
            GUILayout.EndScrollView();
        }

        private void DrawSettingsTabSettings()
        {
            EditorGUILayout.LabelField("■ Tab Settings");

            foreach(var tabSetting in _Setting.TabSettingList)
            {
                DrawToggleButton(ref tabSetting.isShow, tabSetting.GUIContent, true);
            }
        }

        private void DrawToggleButton(ref bool settingFlag, GUIContent guiContent, bool isTabSetting)
        {
            EditorGUILayout.BeginHorizontal();
            if (settingFlag != EditorGUILayout.Toggle(settingFlag, GUILayout.Width(15)) ||
                GUILayout.Button(guiContent, new GUIStyle(GUI.skin.label)))
            {
                // タブ設定用なら
                if(isTabSetting)
                {
                    // インデックスが変わる前にタブIDを取得しておく
                    var tabId = _TabController.GetCurrentTabId();

                    // 保存
                    settingFlag ^= true;
                    _Setting.Save();
                    // 保存された物から更新する
                    _TabController.UpdateTabEnbaled(_Setting);

                    // タブの数が変わってインデックスが変わるのでそれの対応
                    _TabController.ChangeIndexForce(_TabController.GetIndex(tabId));
                }
                // タブ設定用以外なら
                else
                {
                    // 保存
                    settingFlag ^= true;
                    _Setting.Save();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettingsExportSettings()
        {
            EditorGUILayout.LabelField("■ Export Settings");
            DrawToggleButton(ref _Setting.IsShowNoticeWhenExport, new GUIContent("Show Unimportant Notice", "Show unimportant notices when exporting."), false);

            // ブックマークのエクスポート
            if (GUILayout.Button(new GUIContent("Export Bookmark Data", "Export as .abd file.")))
            {
                // 警告は出すけど何もしない
                if (_Setting.IsShowNoticeWhenExport)
                {
                    string message = "";
                    message += "When sharing exported data with others, please be careful that your bookmarked path does not contain any personal information.";
                    message += "\n\nBecause AnythingBookmark uses the GUID of the Asset to process bookmarks, it may not work depending on the configuration of the project to which you are importing the bookmarks.";
                    // DisplayDialogの内容はコピペできないので翻訳サイトにかけたい人用にログも出力、ダイアログを閉じないとログ出力されないのは最悪なので今後の課題
                    Debug.Log("[" + WindowTitle + "] " + message);
                    if (EditorUtility.DisplayDialog("Notice", message, "OK"))
                    {
                    }
                }

                var path = EditorUtility.SaveFilePanel("", "", "bookmark", "abd");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        string json = JsonUtility.ToJson(new SerializableBookmarkList(_MainBookmarkList));
                        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
                        Debug.Log("[" + WindowTitle + "] Export succeeded. path:" + path);
                        // エクスポート後にフォルダを開くか確認
                        if (EditorUtility.DisplayDialog("Export Result", "Successed!!\n\nOpen " + path + "?", "Open", "Cancel"))
                        {
                            EditorUtility.RevealInFinder(path);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
        }

        private void DrawSettingsImportSettings()
        {
            EditorGUILayout.LabelField("■ Import Common Settings");
            DrawToggleButton(ref _Setting.IsShowDifferentProjectGuidNoticeWhenImport, new GUIContent("Show Different ProjectGUID Notice", "Show Notice for different ProjectGUID when importing. Recommended to keep this setting enabled."), false);

            EditorGUILayout.LabelField("■ Import (Add) Settings");
            DrawToggleButton(ref _Setting.IsResetRegistrationTimeWhenAddImport, new GUIContent("Reset RegistrationTime", "Reset registration time when additional importing. Recommended to keep this setting enabled."), false);
            DrawToggleButton(ref _Setting.IsResetLastAccessTimeWhenAddImport, new GUIContent("Reset LastAccessTime", "Reset last access time when additional importing. Recommended to keep this setting enabled."), false);
            DrawToggleButton(ref _Setting.IsResetFavWhenAddImport, new GUIContent("Reset FavFlag", "Reset favorite flag when additional importing. Recommended to keep this setting enabled."), false);

            // ブックマークの追加インポート
            if (GUILayout.Button(new GUIContent("Import Bookmark Data (Add)", "Import .abd file.\nIt will be added to the current bookmark data.")))
            {
                // インポート前にエクスポートしたか確認
                var message = "It will be added to the current bookmark data.\nWe recommend exporting before importing.\n\nWould you like to continue?";
                Debug.Log("[" + WindowTitle + "] " + message);
                if (EditorUtility.DisplayDialog("Confirmation", message, "Continue", "Cancel"))
                {
                    var path = EditorUtility.OpenFilePanel("", "", "abd");
                    if (!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                            var serializableBookmarkList = JsonUtility.FromJson<SerializableBookmarkList>(json);
                            // 何かしらで失敗
                            if (serializableBookmarkList == null)
                            {
                                Debug.LogWarning("[" + WindowTitle + "] Import failed. path:" + path);
                            }
                            // 成功
                            else
                            {
                                // インポートしてきたデータが今のプロジェクトのGUIDと違う場合
                                if (serializableBookmarkList.Guid != PlayerSettings.productGUID.ToString())
                                {
                                    // 警告は出すけど何もしない
                                    if (_Setting.IsShowDifferentProjectGuidNoticeWhenImport)
                                    {
                                        message = "Data with a different ProjectGUID has been imported.\nAssets-related bookmarks may not work properly.";
                                        Debug.Log("[" + WindowTitle + "] " + message);
                                        if (EditorUtility.DisplayDialog("ProjectGUID Notice", message, "OK"))
                                        {
                                        }
                                    }
                                }

                                // メインに反映して保存して再起動
                                foreach (var bookmark in serializableBookmarkList.List)
                                {
                                    // 追加インポート時に登録時間、最終アクセス時間をリセットするか
                                    if (_Setting.IsResetRegistrationTimeWhenAddImport) bookmark.time = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                                    if (_Setting.IsResetLastAccessTimeWhenAddImport) bookmark.lastAccess = 0;
                                    if (_Setting.IsResetFavWhenAddImport) bookmark.fav = 0;
                                    _mainBookmarkList.Add(bookmark);
                                }
                                SaveBookmarkList();

                                Debug.Log("[" + WindowTitle + "] Import successed. path:" + path);
                                // 再起動
                                if (EditorUtility.DisplayDialog("Import Result", "Successed!!\n\nReboot " + WindowTitle + ".", "OK"))
                                {
                                    Close();
                                    CreateWindow();
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                        }
                    }
                }

            }

            // ブックマークの上書きインポート
            ChangeBackgroundColor(Color.red);
            if (GUILayout.Button(new GUIContent("Import Bookmark Data (OverWrite)", "Import .abd file.\nThe current bookmark data will be lost when importing.")))
            {
                // インポート前にエクスポートしたか確認
                var message = "The current bookmark data will be lost when importing.\nWe recommend exporting before importing.\n\nWould you like to continue?";
                Debug.Log("[" + WindowTitle + "] " + message);
                if (EditorUtility.DisplayDialog("Confirmation", message, "Continue", "Cancel"))
                {
                    var path = EditorUtility.OpenFilePanel("", "", "abd");
                    if (!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                            var serializableBookmarkList = JsonUtility.FromJson<SerializableBookmarkList>(json);
                            // 何かしらで失敗
                            if (serializableBookmarkList == null)
                            {
                                Debug.LogWarning("[" + WindowTitle + "] Import failed. path:" + path);
                            }
                            // 成功
                            else
                            {
                                // インポートしてきたデータが今のプロジェクトのGUIDと違う場合
                                if (serializableBookmarkList.Guid != PlayerSettings.productGUID.ToString())
                                {
                                    // 警告は出すけど何もしない
                                    message = "Data with a different ProjectGUID has been imported.\nAssets-related bookmarks may not work properly.";
                                    Debug.Log("[" + WindowTitle + "] " + message);
                                    if (EditorUtility.DisplayDialog("ProjectGUID Notice", message, "OK"))
                                    {
                                    }
                                }

                                // メインに反映して保存して再起動
                                _mainBookmarkList = serializableBookmarkList.List;
                                SaveBookmarkList();

                                Debug.Log("[" + WindowTitle + "] Import successed. path:" + path);
                                // 再起動
                                if (EditorUtility.DisplayDialog("Import Result", "Successed!!\n\nReboot " + WindowTitle + ".", "OK"))
                                {
                                    Close();
                                    CreateWindow();
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                        }
                    }
                }

            }
            ReturnBackgroundColor();
        }


        private void DrawSettingsFooter()
        {
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("(C) 2023 OkaneGames / zenigane");
                if (GUILayout.Button(new GUIContent("GitHub", ""), GUILayout.Width(50)))
                {
                    Application.OpenURL("https://github.com/zenigane138");
                }
                if (GUILayout.Button(new GUIContent("Blog", ""), GUILayout.Width(35)))
                {
                    Application.OpenURL("https://zenigane138.hateblo.jp/?from=ab1");
                }
                if (GUILayout.Button(new GUIContent("Twitter", ""), GUILayout.Width(55)))
                {
                    Application.OpenURL("https://twitter.com/zenigane138");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSeparator()
        {
            var splitterRect = EditorGUILayout.GetControlRect(false, GUILayout.Height(1));
            splitterRect.x = 0;
            splitterRect.width = position.width;
            EditorGUI.DrawRect(splitterRect, Color.Lerp(Color.gray, Color.black, 0.7f));
        }

        // ゲームオブジェクトの特殊パス（シーンルートから自身まで辿っていくセパレーター区切りパス）の取得
        // 例) Text のゲームオブジェクトを渡したら Canvas/Button/Text のパスが返ってくるイメージ
        private string GetGameObjectSpecialPath(GameObject gameObject, string separator)
        {
            return GetTransformSpecialPathRecursively(gameObject.transform, separator, "");
        }
        private string GetTransformSpecialPathRecursively(Transform transform, string separator, string path)
        {
            return transform.parent == null ? transform.name : GetTransformSpecialPathRecursively(transform.parent, separator, path) + separator + transform.name;
        }

        // 特殊パスとセパレーターで行う特殊なFind
        // 非アクティブ対応、セパレーターのエスケープ非対応
        private GameObject FindGameObjectBySpecialPath(string path, string separator)
        {
            // 全て非アクティブでも取得したいのでシーンルートからTransform.Findで辿っていく
            // セパレーターなしはルートをとっておしまい
            if(!path.Contains(separator))
            {
                return FindRootGameObject(path);
            }

            // セパレーター有りは再帰で処理
            var paths = path.Split(separator.ToCharArray());
            var rootGameObject = FindRootGameObject(paths[0]);
            var result = FindTransformRecursively(rootGameObject == null? null: rootGameObject.transform, 1, paths);
            return result == null ? null : result.gameObject;
        }
        private GameObject FindRootGameObject(string name)
        {
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                foreach (var rootGameObject in scene.GetRootGameObjects())
                {
                    if(name == rootGameObject.name)
                    {
                        return rootGameObject;
                    }
                }
            }
            return null;
        }
        private Transform FindTransformRecursively(Transform transform, int depth, string[] paths)
        {
            if (depth >= paths.Length || transform == null) return transform;
            var result = transform.Find(paths[depth]);
            return result == null ? null : FindTransformRecursively(result, depth + 1, paths);
        }




        private void OnClickAddBookmarkAssetButton()
        {
            if (Selection.assetGUIDs.Length <= 0)
            {
                Debug.Log("[" + WindowTitle + "] No assets selected in ProjectView.");
                return;
            }

            foreach (string assetGuid in Selection.assetGUIDs)
            {
                BookmarkAsset(assetGuid);
            }
            SaveBookmarkList();
            _SortHelper.Execute(_MainBookmarkList);
        }
        private bool BookmarkAsset(string assetGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            if (string.IsNullOrEmpty(path)) return false;

            var bookmark = new Bookmark();
            bookmark.type = Bookmark.Type.Asset;
            bookmark.time = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            bookmark.guid = assetGuid;
            bookmark.path = path;
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(bookmark.path);
            bookmark.name = asset.name;
            bookmark.objectType = asset.GetType().ToString();

            _MainBookmarkList.Add(bookmark);
            return true;
        }

        private void OnClickAddBookmarkExternalDirectoryButton()
        {
            var path = EditorUtility.OpenFolderPanel("", "", "");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            BookmarkExternalDirectory(path);
            SaveBookmarkList();
            _SortHelper.Execute(_MainBookmarkList);
        }
        private bool BookmarkExternalDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var bookmark = new Bookmark();
            bookmark.type = Bookmark.Type.ExternalDir;
            bookmark.path = path;
            bookmark.time = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            _MainBookmarkList.Add(bookmark);
            return true;
        }

        private void OnClickAddBookmarkExternalFileButton()
        {
            var path = EditorUtility.OpenFilePanel("", "", "");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            BookmarkExternalFile(path);
            SaveBookmarkList();
            _SortHelper.Execute(_MainBookmarkList);
        }
        private bool BookmarkExternalFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var bookmark = new Bookmark();
            bookmark.type = Bookmark.Type.ExternalFile;
            bookmark.path = path;
            bookmark.time = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            _MainBookmarkList.Add(bookmark);
            return true;
        }

        private void OnClickAddBookmarkMenuItemButton()
        {
            _inputtedText = "";
            string message = "Input menu item path.\n\n[Fix]Enter\n[Cancel]Esc, Click outside window.\n\nExamples : Edit/Project Settings...\nExamples : Window/Package Manager";
            TextFieldPopup.Show(Event.current.mousePosition, "Edit/Project Settings...", x => _inputtedText = x, () => BookmarkMenuItem(), message, 300);
        }
        private void BookmarkMenuItem()
        {
            var path = _inputtedText;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            //Debug.Log(path);

            var bookmark = new Bookmark();
            bookmark.type = Bookmark.Type.MenuItem;
            bookmark.path = path;
            bookmark.time = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            _MainBookmarkList.Add(bookmark);

            SaveBookmarkList();
            _SortHelper.Execute(_MainBookmarkList);
        }

        private void OnClickAddBookmarkSceneViewCameraButton()
        {
            _inputtedText = "";
            string message = "Saves the current SceneViewCamera state with a name.\n\n[Fix]Enter\n[Cancel]Esc, Click outside window.\n\nExamples : SceneViewCamera01\nExamples : SceneViewCamera02";
            TextFieldPopup.Show(Event.current.mousePosition, "SceneViewCamera01", x => _inputtedText = x, () => BookmarkSceneViewCamera(), message, 300);
        }
        private void BookmarkSceneViewCamera()
        {
            var path = _inputtedText;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            //Debug.Log(path);

            var bookmark = new Bookmark();
            bookmark.type = Bookmark.Type.SceneViewCamera;
            bookmark.path = path;
            bookmark.time = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            bookmark.serialized = JsonUtility.ToJson(new SceneViewCamera(SceneView.lastActiveSceneView));

            _MainBookmarkList.Add(bookmark);

            SaveBookmarkList();
            _SortHelper.Execute(_MainBookmarkList);
        }

        private void OnClickAddBookmarkGameObjectInHierarchyButton()
        {
            _inputtedText = "";
            string message = "Input GameObject path in Hierarchy.\nUse / for child path.\nGameObject names containing / will not work.\n\n[Fix]Enter\n[Cancel]Esc, Click outside window.\n\nExamples : Canvas/Text\nExamples : Directional Light";
            TextFieldPopup.Show(Event.current.mousePosition, "Canvas/Text", x => _inputtedText = x, () => BookmarkGameObjectInHierarchy(_inputtedText, true), message, 300);
        }
        private void BookmarkGameObjectInHierarchy(string path, bool isSave = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            //Debug.Log(path);

            var bookmark = new Bookmark();
            bookmark.type = Bookmark.Type.GameObjectInHierarchy;
            bookmark.path = path;
            bookmark.time = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            _MainBookmarkList.Add(bookmark);
            if (isSave)
            {
                SaveBookmarkList();
                _SortHelper.Execute(_MainBookmarkList);
            }
        }


        private void RemoveBookmark(Bookmark bookmark, bool isSave = false)
        {
            _MainBookmarkList.Remove(bookmark);
            if (isSave) SaveBookmarkList();
        }

        private void OnClickBookmarkCommon(Bookmark bookmark)
        {
            bookmark.lastAccess = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        private void OpenAsset(Bookmark bookmark)
        {
            // アセット以外は処理しない
            if (bookmark.type != Bookmark.Type.Asset) return;

            // ※ UnityEditor.DefaultAsset : エクセル等のデフォルトではUnity非対応のファイル、ディレクトリ
            if (bookmark.objectType != "UnityEditor.DefaultAsset")
            {
                // 特になし
            }

            // シーンファイル
            if (Path.GetExtension(bookmark.path).Equals(".unity"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(bookmark.path, OpenSceneMode.Single);
                }
                return;
            }

            // シーン以外は全部これ
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(bookmark.path);
            AssetDatabase.OpenAsset(asset);
        }


        private void SaveBookmarkList()
        {
            string json = JsonUtility.ToJson(new SerializableBookmarkList(_MainBookmarkList));
            EditorUserSettings.SetConfigValue(BookmarkEditorUserSettingsKey, json);
            AssetDatabase.SaveAssets();
            //Debug.Log(json);
        }

        private List<Bookmark> LoadBookmarkList()
        {
            var json = EditorUserSettings.GetConfigValue(BookmarkEditorUserSettingsKey);
            if (json == null) return new List<Bookmark>();

            var serializableBookmarkList = JsonUtility.FromJson<SerializableBookmarkList>(json);
            if (serializableBookmarkList == null)
            {
                // ここを通るならフォーマット変更等があって使えないデータになったので空で保存しなおしてもいいかも
                return new List<Bookmark>();
            }
            return serializableBookmarkList.List;
        }

        private List<Bookmark> UpdateFavBookmarkList()
        {
            if (_favBookmarkList == null) _favBookmarkList = new List<Bookmark>();
            _favBookmarkList.Clear();
            foreach (var bookmark in _MainBookmarkList)
            {
                // メインリストからお気に入りのみ抽出
                if (bookmark.fav == 1) _favBookmarkList.Add(bookmark);
            }
            _SortHelper.Execute(_favBookmarkList);
            return _favBookmarkList;
        }

        private List<Bookmark> UpdateSubTabBookmarkList()
        {
            if (_subTabBookmarkList == null) _subTabBookmarkList = new List<Bookmark>();

            _subTabBookmarkList.Clear();
            foreach (var bookmark in _MainBookmarkList)
            {
                // メインリストから現在のタブIDに合わせた条件で抽出
                var id = _TabController.GetCurrentTabId();
                if (id == Tab.Id.Assets)
                {
                    // アセットだけアセットのタイプ別フィルタリング入れるかもしれない
                    if (bookmark.type == Bookmark.Type.Asset)
                    {
                        if(_FilterHelper.IsAddableBookmark(_FilterHelper.Mask, bookmark)) _subTabBookmarkList.Add(bookmark);
                    }
                }
                else if (id == Tab.Id.ExternalDir)
                {
                    if (bookmark.type == Bookmark.Type.ExternalDir) _subTabBookmarkList.Add(bookmark);
                }
                else if (id == Tab.Id.ExternalFile)
                {
                    if (bookmark.type == Bookmark.Type.ExternalFile) _subTabBookmarkList.Add(bookmark);
                }
                else if (id == Tab.Id.MenuItem)
                {
                    if (bookmark.type == Bookmark.Type.MenuItem) _subTabBookmarkList.Add(bookmark);
                }
                else if (id == Tab.Id.SceneViewCamera)
                {
                    if (bookmark.type == Bookmark.Type.SceneViewCamera) _subTabBookmarkList.Add(bookmark);
                }
                else if (id == Tab.Id.GameObjectInHierarchy)
                {
                    if (bookmark.type == Bookmark.Type.GameObjectInHierarchy) _subTabBookmarkList.Add(bookmark);
                }

            }

            _SortHelper.Execute(_subTabBookmarkList);
            return _subTabBookmarkList;
        }

        private void RemoveAllSaveData()
        {
            EditorUserSettings.SetConfigValue(BookmarkEditorUserSettingsKey, "");
            EditorUserSettings.SetConfigValue(SortTypeEditorUserSettingsKey, "");
            EditorUserSettings.SetConfigValue(SettingsEditorUserSettingsKey, "");
            AssetDatabase.SaveAssets();

            _mainBookmarkList = null;
            _favBookmarkList = null;
            _subTabBookmarkList = null;
        }

        private void ChangeBackgroundColor(Color color)
        {
            _tempBGColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
        }

        private void ReturnBackgroundColor()
        {
            GUI.backgroundColor = _tempBGColor;
        }


        /// <summary>
        /// 設定クラス
        /// </summary>
        [Serializable]
        public class Setting : ISerializationCallbackReceiver
        {
            // エクスポートインポート時に情報を出すかどうか
            public bool IsShowNoticeWhenExport = false;
            public bool IsShowDifferentProjectGuidNoticeWhenImport = true;
            // 追加インポート時に登録時間、最終アクセス時間をリセットするか
            public bool IsResetRegistrationTimeWhenAddImport = true;
            public bool IsResetLastAccessTimeWhenAddImport = true;
            public bool IsResetFavWhenAddImport = true;

            [SerializeField]
            private List<TabSetting> _tabSettingList = null;
            public List<TabSetting> TabSettingList
            {
                get
                {
                    if (_tabSettingList == null) _tabSettingList = new List<TabSetting>();
                    return _tabSettingList;
                }
            }

            [Serializable]
            public class TabSetting
            {
                public Tab.Id id;
                public bool isShow;
                //[NonSerialized]
                //public GUIContent guiContent;
                private GUIContent guiContent;
                public GUIContent GUIContent
                {
                    get
                    {
                        // string.IsNullOrEmpty(guiContent.text) はエディタ再生開始時に guiContent は null にはならないけど中身が空になる対策
                        if (guiContent == null || string.IsNullOrEmpty(guiContent.text)) CreateGUIContent();
                        return guiContent;
                    }
                }

                public TabSetting(Tab.Id id, bool isShow)
                {
                    this.id = id;
                    this.isShow = isShow;
                }

                public void CreateGUIContent()
                {
                    if (false) { }
                    else if (id == Tab.Id.Assets) guiContent = new GUIContent("Show Assets", "Enable tabs to show only Assets.");
                    else if (id == Tab.Id.ExternalDir) guiContent = new GUIContent("Show ExternalDir", "Enable tabs to show only ExternalDir.");
                    else if (id == Tab.Id.ExternalFile) guiContent = new GUIContent("Show ExternalFile", "Enable tabs to show only ExternalFile.");
                    else if (id == Tab.Id.MenuItem) guiContent = new GUIContent("Show MenuItem", "Enable tabs to show only MenuItem.");
                    else if (id == Tab.Id.SceneViewCamera) guiContent = new GUIContent("Show SceneViewCamera", "Enable tabs to show only SceneViewCamera.");
                    else if (id == Tab.Id.GameObjectInHierarchy) guiContent = new GUIContent("Show GameObjectInHierarchy", "Enable tabs to show only GameObjectInHierarchy.");
                }
            }

            private Setting()
            {
                // 追加順が設定での並び順になる
                TabSettingList.Add(new TabSetting(Tab.Id.Assets, false));
                TabSettingList.Add(new TabSetting(Tab.Id.ExternalDir, false));
                TabSettingList.Add(new TabSetting(Tab.Id.ExternalFile, false));
                TabSettingList.Add(new TabSetting(Tab.Id.MenuItem, false));
                TabSettingList.Add(new TabSetting(Tab.Id.SceneViewCamera, false));
                TabSettingList.Add(new TabSetting(Tab.Id.GameObjectInHierarchy, false));
            }

            public TabSetting GetTabSetting(Tab.Id id)
            {
                foreach (var tabSetting in TabSettingList)
                {
                    if (id == tabSetting.id) return tabSetting;
                }
                return null;
            }

            public void OnAfterDeserialize()
            {
            }

            public void OnBeforeSerialize()
            {
            }

            public void Save()
            {
                var json = JsonUtility.ToJson(this);
                //Debug.Log(json);
                EditorUserSettings.SetConfigValue(SettingsEditorUserSettingsKey, json);
                AssetDatabase.SaveAssets();
            }

            public static Setting Load()
            {
                var data = EditorUserSettings.GetConfigValue(SettingsEditorUserSettingsKey);
                var setting = JsonUtility.FromJson<Setting>(data);
                if (setting == null)
                {
                    setting = new Setting();
                }
                // GUIContent は保存してないのでここで生成しておく
                foreach (var tabSetting in setting.TabSettingList) tabSetting.CreateGUIContent();
                return setting;
            }
        }


        /// <summary>
        /// タブ。タブコントローラーありきで作ったから色々と微妙
        /// </summary>
        public class Tab
        {
            public enum Id
            {
                None = -1,
                Main = 0,
                Fav,
                Settings,
                Assets,
                ExternalDir,
                ExternalFile,
                MenuItem,
                SceneViewCamera,
                GameObjectInHierarchy,
            }

            public Id id;
            public int staticIndex;
            public int dynamicIndex;
            public GUIContent guiContent;
            public bool enabled;

            public Tab(Id id, string text, string tooltip)
            {
                this.id = id;
                guiContent = new GUIContent(text, tooltip);
                UpdateEnabled(null);
            }

            public void UpdateEnabled(Setting setting)
            {
                // メイン、お気に入り、設定は常に有効
                if (id == Id.Main || id == Id.Fav || id == Id.Settings)
                {
                    enabled = true;
                }
                else
                {
                    // 常に有効なタブ以外は設定を参照して決める
                    // 引数の setting がなければとりあえず無効
                    if (setting == null) enabled = false;
                    else
                    {
                        //if (id is Id.Assets) enabled = setting.GetTabSetting(Id.Assets).isShow;
                        enabled = setting.GetTabSetting(id).isShow;
                    }
                }
            }
        }


        /// <summary>
        /// タブコントローラー
        /// </summary>
        private class TabController
        {
            // 生成時に固定されて以降は参照にしか使わない全てのタブリスト
            private List<Tab> _fixedAllTabList;

            // 設定によってタブの数が変動するのでフレキシブル
            private List<Tab> _flexibleTabList;
            private GUIContent[] _flexibleGuiContents;
            // flexible系変数で使うインデックス
            private int _currentIndex = -1;

            // 引数:現在のID、新しいID
            public Action<Tab.Id, Tab.Id> OnTabIdChanged;

            public TabController()
            {
                if (_fixedAllTabList == null)
                {
                    // 追加順がツールバーでの並び順
                    _fixedAllTabList = new List<Tab>();
                    _fixedAllTabList.Add(new Tab(Tab.Id.Main, "Main", "Main tab."));
                    _fixedAllTabList.Add(new Tab(Tab.Id.Fav, "★", "Starred bookmark tab."));
                    //_fixedAllTabList.Add(new Tab(Tab.Id.Settings, "Settings", "Settings tab."));
                    _fixedAllTabList.Add(new Tab(Tab.Id.Assets, "Assets", "Unity Assets tab."));
                    _fixedAllTabList.Add(new Tab(Tab.Id.ExternalDir, "ExtDir", "External Dir tab."));
                    _fixedAllTabList.Add(new Tab(Tab.Id.ExternalFile, "ExtFile", "External File tab"));
                    _fixedAllTabList.Add(new Tab(Tab.Id.MenuItem, "MenuItem", "Unity MenuItem tab."));
                    _fixedAllTabList.Add(new Tab(Tab.Id.SceneViewCamera, "Camera", "SceneView Camera tab."));
                    _fixedAllTabList.Add(new Tab(Tab.Id.GameObjectInHierarchy, "GOinH", "GameObject in Hierarchy tab."));
                    _fixedAllTabList.Add(new Tab(Tab.Id.Settings, "Settings", "Settings tab."));

                    // TabController のコンストラクタに Setting のインスタンスを渡そうとするとどちらもプロパティから生成する都合で順番が重要になる
                    // 後々のメンテ時にプロパティのアクセス順縛りを忘れてハマりそう予感がするので、不格好だけどこのタイミングだけは設定ファイルから読み込む
                    var setting = Setting.Load();
                    foreach (var tab in _fixedAllTabList)
                    {
                        tab.UpdateEnabled(setting);
                    }
                }
                if (_flexibleTabList == null)
                {
                    _flexibleTabList = new List<Tab>();
                }

                UpdateFlexibleVariable();
            }

            public GUIContent[] GetGUIContents()
            {
                if (_flexibleGuiContents == null) UpdateFlexibleVariable();
                return _flexibleGuiContents;
            }

            public void ReceiveToolbarResult(int newIndex)
            {
                ChangeIndex(newIndex);
            }

            public Tab.Id GetCurrentTabId()
            {
                return GetTabId(_currentIndex);
            }

            public int GetCurrentIndex()
            {
                return _currentIndex;
            }

            public bool ChangeTab(Tab.Id newId)
            {
                var currentId = GetCurrentTabId();
                if (currentId == newId) return false;

                var newIndex = GetIndex(newId);
                return ChangeIndex(newIndex);
            }

            // 設定変更でタブ数に変化があってインデックス外になってしまう時にだけ使うインデックス変更
            // 正しく処理すればID変更は伴わないので OnTabIdChanged は呼び出さない
            public void ChangeIndexForce(int newIndex)
            {
                _currentIndex = newIndex;
            }

            public void UpdateTabEnbaled(Setting setting)
            {
                // 全タブで更新
                foreach (var tab in _fixedAllTabList)
                {
                    tab.UpdateEnabled(setting);
                }
                // フレキシブルに反映
                UpdateFlexibleVariable();
            }

            public int GetIndex(Tab.Id id)
            {
                for (int i = 0; i < _flexibleTabList.Count; i++)
                {
                    if (id == _flexibleTabList[i].id) return i;
                }
                return -1;
            }

            private void UpdateFlexibleVariable()
            {
                // タブリスト作成
                _flexibleTabList.Clear();
                foreach (var tab in _fixedAllTabList)
                {
                    if (tab.enabled) _flexibleTabList.Add(tab);
                }

                // GUIContent 配列の準備
                if (_flexibleGuiContents == null)
                {
                    _flexibleGuiContents = new GUIContent[_flexibleTabList.Count];
                }
                else
                {
                    if (_flexibleGuiContents.Length != _flexibleTabList.Count)
                    {
                        Array.Resize(ref _flexibleGuiContents, _flexibleTabList.Count);
                    }
                }

                // GUIContent 配列の作成
                int index = 0;
                foreach (var tab in _flexibleTabList)
                {
                    _flexibleGuiContents[index++] = tab.guiContent;
                }
            }

            private Tab.Id GetTabId(int index)
            {
                return _flexibleTabList[index].id;
            }

            private bool ChangeIndex(int newIndex)
            {
                // タブ変更検出したら
                if (_currentIndex != newIndex)
                {
                    // ↓の -1 処理は とりあえず EditorWindow 側で -1 が来ないよう解決したので新たな問題が起きるまでコメントアウト
                    //// _currentIndex == -1 は起動時のみ -1 が入る時の対応処理
                    ////var currentId = _currentIndex == -1 ? Tab.Id.None : GetTabId(_currentIndex);
                    var currentId = GetTabId(_currentIndex);
                    var newId = GetTabId(newIndex);

                    // 新に反映
                    _currentIndex = newIndex;

                    // 変更通知呼び出し
                    if (OnTabIdChanged != null)
                    {
                        OnTabIdChanged(currentId, newId);
                    }
                    return true;
                }
                return false;
            }

        }


        /// <summary>
        /// ブックマーククラス
        /// </summary>
        [Serializable]
        public class Bookmark
        {
            public enum Type
            {
                Empty = 0,
                Asset,
                ExternalDir,
                ExternalFile,
                MenuItem,
                SceneViewCamera,
                GameObjectInHierarchy,
            }
            public Type type;
            public string path;
            public long time;           // unixtime
            public long lastAccess;     // unixtime
            public int fav;
            public string guid = "";
            public string name = "";
            public string objectType = "";
            public string serialized = "";    // Serializableな物をぶちこむ汎用変数
        }


        /// <summary>
        /// ブックマークに使うシリアライズ可能なSceneViewのカメラ
        /// </summary>
        [Serializable]
        private class SceneViewCamera
        {
            public Vector3 pivot;
            public Quaternion rotation;
            public float size;
            public SceneViewCamera(SceneView sceneView)
            {
                pivot = sceneView.pivot;
                rotation = sceneView.rotation;
                size = sceneView.size;
            }
        }


        /// <summary>
        /// シリアライズ可能なブックマークリストクラス
        /// JSONでセーブロード時に一瞬使う
        /// </summary>
        [Serializable]
        private class SerializableBookmarkList : ISerializationCallbackReceiver
        {
            [SerializeField]
            private List<Bookmark> _list = null;
            public List<Bookmark> List
            {
                get { return _list; }
            }

            // PlayerSettings.productGUIDが入る
            // インポート時に別プロジェクトの物だったらAssetのGUIDがほぼ効かなくなるから警告を出す
            [SerializeField]
            private string _guid;
            public string Guid
            {
                get { return _guid; }
            }

            public SerializableBookmarkList(List<Bookmark> list)
            {
                _list = list;
                _guid = PlayerSettings.productGUID.ToString();
            }

            public void OnBeforeSerialize()
            {
            }

            public void OnAfterDeserialize()
            {
            }
        }


        /// <summary>
        /// ソート用クラス
        /// </summary>
        private class SortHelper
        {
            private int _index = -1;
            public int Index
            {
                get
                {
                    if(_index == -1)
                    {
                        // デフォルト
                        _index = 0;
                        // ファイルから読み込む
                        var str = EditorUserSettings.GetConfigValue(SortTypeEditorUserSettingsKey);
                        if (!string.IsNullOrEmpty(str))
                        {
                            Int32.TryParse(str, out _index);
                        }
                    }
                    return _index;
                }
                set
                {
                    _index = value;
                    // ファイルに書き込む
                    EditorUserSettings.SetConfigValue(SortTypeEditorUserSettingsKey, _index.ToString());
                    AssetDatabase.SaveAssets();
                }
            }

            // 起動時にExecuteが呼ばれたかどうか
            private bool _wasCalledExecuteOnStartup = false;
            public bool WasCalledExecuteOnStartup
            {
                get
                {
                    return _wasCalledExecuteOnStartup;
                }
                set
                {
                    _wasCalledExecuteOnStartup = value;
                }
            }

            private string[] _displayNames;
            public string[] DisplayNames
            {
                get { return _displayNames; }
            }

            private Action<List<Bookmark>>[] _actions;
            public void Execute(List<Bookmark> list)
            {
                _actions[_index](list);
            }

            public SortHelper()
            {
                var index = Index;

                _displayNames = new string[]
                {
                    "▲ Registration ASC  (Old -> New)",
                    "▼ Registration DESC (New -> Old)",
                    "▲ Recently used ASC  (Old -> New)",
                    "▼ Recently used DESC (New -> Old)",
                    "▲ Type ASC  (Assets -> ...)",
                    "▼ Type DESC (GameObject in Hierarchy -> ...)",
                    "▲ Name ASC  (A -> Z)",
                    "▼ Name DESC (Z -> A)",
                };

                _actions = new Action<List<Bookmark>>[]
                {
                    // "▲ Registration ASC",
                    list => {
                        list.Sort((a, b) =>
                        {
                            var r = a.time.CompareTo(b.time);
                            return r == 0 ? CompareToFinal(a, b) : r;
                        });
                    },
                    // "▼ Registration DESC",
                    list => {
                        list.Sort((a, b) =>
                        {
                            var r = b.time.CompareTo(a.time);
                            return r == 0 ? CompareToFinal(a, b) : r;
                        });
                    },
                    // "▲ Recently used ASC",
                    list => {
                        list.Sort((a, b) =>
                        {
                            var r = a.lastAccess.CompareTo(b.lastAccess);
                            return r == 0 ? CompareToFinal(a, b) : r;
                        });
                    },
                    // "▼ Recently used DESC",
                    list => {
                        list.Sort((a, b) =>
                        {
                            var r = b.lastAccess.CompareTo(a.lastAccess);
                            return r == 0 ? CompareToFinal(a, b) : r;
                        });
                    },
                    // "▲ Type ASC",
                    list => {
                        list.Sort((a, b) =>
                        {
                            return CompareToFinal(a, b);
                        });
                    },
                    // "▼ Type DESC",
                    list => {
                        list.Sort((a, b) =>
                        {
                            return CompareToFinal(b, a);
                        });
                    },
                    // "▲ Name ASC",
                    list => {
                        list.Sort((a, b) =>
                        {
                            return CompareToName(a, b);
                        });
                    },
                    // "▼ Name DESC",
                    list => {
                        list.Sort((a, b) =>
                        {
                            return CompareToName(b, a);
                        });
                    },
                };
            }

            private int CompareToType(Bookmark a, Bookmark b)
            {
                return a.type == Bookmark.Type.Asset && a.type == b.type ? a.objectType.CompareTo(b.objectType) : a.type.CompareTo(b.type);
            }
            private int CompareToName(Bookmark a, Bookmark b)
            {
                var an = string.IsNullOrEmpty(a.name) ? a.path : a.name;
                var bn = string.IsNullOrEmpty(b.name) ? b.path : b.name;
                return an.CompareTo(bn);
            }
            // 最終と最終の一つ前ソートを行う
            private int CompareToFinal(Bookmark a, Bookmark b)
            {
                // タイプ
                var r = CompareToType(a, b);
                // 一致なら最後は名前
                return r == 0 ? CompareToName(a, b) : r;
            }

        }


        /// <summary>
        /// Unityアセットタイプのフィルター用クラス
        /// </summary>
        private class ObjectTypeFilterHelper
        {
            // 初期値 -1 は MaskField の Everything で使うから避ける
            private int _mask = -2;
            public int Mask
            {
                get
                {
                    if (_mask == -2)
                    {
                        // デフォルト
                        //_mask = 0;
                        // ファイルから読み込む
                        //var str = EditorUserSettings.GetConfigValue(FilterTypeEditorUserSettingsKey);
                        //if (!string.IsNullOrEmpty(str))
                        //{
                        //    Int32.TryParse(str, out _index);
                        //}
                    }
                    return _mask;
                }
                set
                {
                    _mask = value;
                    // ファイルに書き込む
                    //EditorUserSettings.SetConfigValue(FilterTypeEditorUserSettingsKey, _index.ToString());
                    //AssetDatabase.SaveAssets();
                }
            }

            private string[] _displayNames;
            public string[] DisplayNames
            {
                get { return _displayNames; }
            }

            public ObjectTypeFilterHelper()
            {
                var mask = Mask;

                // ここの並び順がそのまま MaskField の表示順
                _displayNames = new string[]
                {
                    "Dir and Uncategorized  :  UnityEditor.DefaultAsset",
                    "Script  :  UnityEditor.MonoScript",
                    "Scene-related  :  UnityEditor.SceneAsset etc",
                    "Prefab  :  UnityEngine.GameObject",
                    "Text  :  UnityEngine.TextAsset",
                    "Font  :  UnityEngine.Font",
                    "Texture-related  :  UnityEngine.Texture2D etc",
                    "Material  :  UnityEngine.Material",
                    "Shader-related  :  UnityEngine.Shader etc",
                    "Audio-related  :  UnityEngine.AudioClip etc",
                    "Animation-related  :  UnityEngine.AnimationClip etc",
                    "Timeline  :  UnityEngine.Timeline.TimelineAsset",
                    "VideoClip  :  UnityEngine.Video.VideoClip",
                    "Others",
                };
            }

            public bool IsAddableBookmark(int mask, Bookmark bookmark, bool forJudjeOthers = false)
            {
                if (bookmark.type != Bookmark.Type.Asset)
                {
                    return false;
                }

                // Everything (-1) を選んだ場合は、ここで管理していない下記のようなアセットも漏れなく表示できるように true を返しておく
                // 例) UnityEditorInternal.AssemblyDefinitionAsset
                // 例) TMPro.TMP_Settings
                // ↑Othersを追加したのでなくても表示できるけど判定はこっちが高速なので残す
                if (!forJudjeOthers && mask == -1) return true;

                var m = (Id)mask;
                var t = bookmark.objectType;

                // 全体的に判定が厳密すぎるからもっとアバウトでもいいかも
                if (HasBitFlag(m, Id.DefaultAsset)) if (t == "UnityEditor.DefaultAsset") return true;
                if (HasBitFlag(m, Id.MonoScript)) if (t == "UnityEditor.MonoScript") return true;
                if (HasBitFlag(m, Id.SceneRelated))
                {
                    if (t == "UnityEditor.SceneAsset" || t == "UnityEditor.SceneTemplate.SceneTemplateAsset") return true;
                }
                if (HasBitFlag(m, Id.GameObject)) if (t == "UnityEngine.GameObject") return true;
                if (HasBitFlag(m, Id.TextAsset)) if (t == "UnityEngine.TextAsset") return true;
                if (HasBitFlag(m, Id.Font)) if (t == "UnityEngine.Font") return true;
                if (HasBitFlag(m, Id.TextureRelated))
                {
                    if (t == "UnityEngine.Texture2D" || t == "UnityEngine.Texture" || t == "UnityEngine.RenderTexture"
                        || t == "UnityEngine.CustomRenderTexture" || t == "UnityEngine.U2D.SpriteAtlas") return true;
                }
                if (HasBitFlag(m, Id.Material)) if (t == "UnityEngine.Material") return true;
                if (HasBitFlag(m, Id.ShaderRelated))
                {
                    if (t == "UnityEngine.Shader" || t == "UnityEditor.ShaderInclude"
                        || t == "UnityEngine.ShaderVariantCollection") return true;
                }
                if (HasBitFlag(m, Id.AudioRelated))
                {
                    if (t == "UnityEngine.AudioClip" || t == "UnityEditor.Audio.AudioMixerController") return true;
                }
                if (HasBitFlag(m, Id.AnimationRelated))
                {
                    if (t == "UnityEngine.AnimationClip" || t == "UnityEditor.Animations.AnimatorController"
                        || t == "UnityEngine.AnimatorOverrideController" || t == "UnityEngine.AvatarMask") return true;
                }
                if (HasBitFlag(m, Id.Timeline)) if (t == "UnityEngine.Timeline.TimelineAsset") return true;
                if (HasBitFlag(m, Id.VideoClip)) if (t == "UnityEngine.Video.VideoClip") return true;
                if (HasBitFlag(m, Id.Others))
                {
                    // Everything (-1) を渡し、上記判定を再度行い false が返ってきたら全ての条件に引っかかってないので反転して true を返す
                    if (!forJudjeOthers) return !IsAddableBookmark(-1, bookmark, true);
                    else return false;
                }

                return false;
            }

            // 32bit分いけるけど実際32個も表示したら操作がだるいのである程度厳選する
            public enum Id
            {
                DefaultAsset = 1 << 0,
                MonoScript = 1 << 1,
                SceneRelated = 1 << 2,
                GameObject = 1 << 3,
                TextAsset = 1 << 4,
                Font = 1 << 5,
                TextureRelated = 1 << 6,
                Material = 1 << 7,
                ShaderRelated = 1 << 8,
                AudioRelated = 1 << 9,
                AnimationRelated = 1 << 10,
                Timeline = 1 << 11,
                VideoClip = 1 << 12,
                Others = 1 << 13,
            
            }
            private static bool HasBitFlag(Id value, Id flag)
            {
                return (value & flag) == flag;
            }
        }


        /// <summary>
        /// ポップアップ入力用クラス（ほぼ↓の通りで★部分だけちょっと改造）
        /// https://light11.hatenadiary.com/entry/2020/02/10/211326
        /// デフォルト値即Enterは反映、Esc終了は未反映、範囲外クリックも未反映、C#4.0対応
        /// </summary>
        private class TextFieldPopup : PopupWindowContent
        {
            private const float WINDOW_PADDING = 8.0f;

            private string _text;
            private string _message;
            private float _width;
            private Action<string> _changed;
            private Action _closed;
            private GUIStyle _messageLabelStyle;
            private Vector2 _windowSize;
            private bool _didFocus = false;

            public static void Show(Vector2 position, string text, Action<string> changed, Action closed, string message = null, float width = 300)
            {
                var rect = new Rect(position, Vector2.zero);
                var content = new TextFieldPopup(text, changed, closed, message, width);
                PopupWindow.Show(rect, content);
            }

            private TextFieldPopup(string text, Action<string> changed, Action closed, string message = null, float width = 300)
            {
                _message = message;
                _text = text;
                _width = width;
                _changed = changed;
                _closed = closed;

                _messageLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                _messageLabelStyle.wordWrap = true;

                // ウィンドウサイズを計算する
                var labelWidth = _width - (WINDOW_PADDING * 2);
                _windowSize = Vector2.zero;
                _windowSize.x = _width;
                _windowSize.y += WINDOW_PADDING; // Space
                _windowSize.y += _messageLabelStyle.CalcHeight(new GUIContent(message), labelWidth); // Message
                _windowSize.y += EditorGUIUtility.standardVerticalSpacing; // Space
                _windowSize.y += EditorGUIUtility.singleLineHeight; // TextField
                _windowSize.y += WINDOW_PADDING; // Space
            }

            public override void OnGUI(Rect rect)
            {
                // Enterで閉じる
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    CloseFromKeyboard(_text);
                }
                // ★Escでキャンセルっぽい挙動
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    CloseFromKeyboard("");
                }

                //var textFieldName = $"{GetType().Name}{nameof(_text)}";
                var textFieldName = "{" + GetType().Name + "}{_text}";
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(WINDOW_PADDING);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        // タイトルを描画
                        EditorGUILayout.LabelField(_message, _messageLabelStyle);
                        // TextFieldを描画
                        using (var ccs = new EditorGUI.ChangeCheckScope())
                        {
                            GUI.SetNextControlName(textFieldName);
                            _text = EditorGUILayout.TextField(_text);
                            if (ccs.changed)
                            {
                                if (_changed != null) _changed.Invoke(_text);
                            }
                        }
                    }
                    GUILayout.Space(WINDOW_PADDING);
                }
                // 最初の一回だけ自動的にフォーカスする
                if (!_didFocus)
                {
                    GUI.FocusControl(textFieldName);
                    _didFocus = true;
                }
            }

            // ★キーボードから閉じる
            private void CloseFromKeyboard(string text)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    if(_changed != null) _changed.Invoke(text);
                }
                editorWindow.Close();
            }

            public override void OnClose()
            {
                if(_closed != null) _closed.Invoke();
                base.OnClose();
            }

            public override Vector2 GetWindowSize()
            {
                return _windowSize;
            }
        }


    }

}

