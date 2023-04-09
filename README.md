AnythingBookmark
================

日本語版の README.md は[こちら](/README_ja.md)。  

Overview
--------
AnythingBookmark is an editor extension window that allows you to bookmark not only Unity assets but also various elements.  

https://user-images.githubusercontent.com/36072156/229339442-95001899-8b29-4bf7-89d8-5cffb9618a63.mp4"  

Elements that can be bookmarked
-------------------------------
- Various assets under the Assets/ folder
- Folders outside the project
- Files outside the project
- MenuItem shortcuts (BuildSettings, Profiler, etc.)
- Camera coordinates and directions in the SceneView
- GameObjects in the Hierarchy window

Features
--------
- Registration is possible by dragging and dropping assets
- Favorites registration
- Tab to display only favorites
- Tab for each type of bookmark
- Sort and filter
- Export/import bookmark data (.abd)

System Requirements
-------------------
- Required
  - C# 6.0 or later
- Recommended
  - Unity 2020.1 or later

Installation
------------
- unitypackage
  - Download the unitypackage from the Releases page.
  - https://github.com/zenigane138/AnythingBookmark/releases
- PackageManager
  - Open the window from Window -> PackageManager
  - Select "Add package from git URL..." from the +▼ button and set the following URL.
  - https://github.com/zenigane138/AnythingBookmark.git?path=Assets/AnythingBookmark

How to Use
----------
- Open the window from the Unity menu.
  - OkaneGames -> AnythingBookmark
  - Window -> OkaneGames -> AnythingBookmark
- Register by clicking the add button or by dragging and dropping assets.

Data Storage Location
---------------------
EditorUserSettings is used.
If you want to share bookmark data with other projects or people, please use the export/import function.
Please note that folders and files outside the project are managed by their full paths.

License
-------
See [LICENSE.md](/LICENSE.md).
