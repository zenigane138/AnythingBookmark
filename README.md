AnythingBookmark
============
日本語版の README.md は[こちら](/README_ja.md)。  

Overview
---
AnythingBookmark is an editor extension window that allows you to bookmark not only Unity assets but also various elements.  

![](https://img.shields.io/badge/Unity-2017.1%20or%20later-lightgrey)
[![](https://img.shields.io/badge/license-MIT-orange)](https://github.com/zenigane138/AnythingBookmark/blob/main/LICENSE.md)
[![](https://img.shields.io/badge/readme-%E6%97%A5%E6%9C%AC%E8%AA%9E%E7%89%88-red)](/README_ja.md)
[![openupm](https://img.shields.io/npm/v/com.okanegames.anythingbookmark?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.okanegames.anythingbookmark/)
[![](https://img.shields.io/badge/Follow-FFFFFF.svg?logo=twitter&style=flat)](https://twitter.com/intent/follow?screen_name=zenigane138)

![image](https://user-images.githubusercontent.com/36072156/232230899-52835490-8a8b-4ad8-8c1d-b2d1a5d78a67.png)

Elements that can be bookmarked
---
- Various assets under the Assets/ folder
- Folders outside the project
- Files outside the project
- MenuItem shortcuts (BuildSettings, Profiler, etc.)
- Camera coordinates and directions in the SceneView
- GameObjects in the Hierarchy window

Features
---
- Registration is possible by dragging and dropping assets
- Favorites registration
- Tab to display only favorites
- Tab for each type of bookmark
- Sort and filter
- Export/import bookmark data (.abd)

Video
---
https://user-images.githubusercontent.com/36072156/232078688-7189e87f-be92-434d-b06e-f3053e5a24b4.mp4

Requirement
---
- Required
  - Unity 2017.1 or later
- Recommended
  - Unity 2020.1 or later

Installation
---
- unitypackage
  - Download the unitypackage from the Releases page.
  - https://github.com/zenigane138/AnythingBookmark/releases
- PackageManager
  - Open the window from Window -> PackageManager
  - Select "Add package from git URL..." from the +▼ button and set the following URL.
  - https://github.com/zenigane138/AnythingBookmark.git?path=Assets/AnythingBookmark

How to Use
---
- Open the window from the Unity menu.
  - OkaneGames -> AnythingBookmark
  - Window -> OkaneGames -> AnythingBookmark
- Register by clicking the add button or by dragging and dropping assets.

Data Storage Location
---
EditorUserSettings is used.
If you want to share bookmark data with other projects or people, please use the export/import function.
Please note that folders and files outside the project are managed by their full paths.

License
---
See [LICENSE.md](/LICENSE.md).
