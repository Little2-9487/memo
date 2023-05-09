# 如何將 Spine 3.7 降至 3.6, 3.5, 3.4 的版本。

首先說明: 使用SkeletonViewer，然後下指令 JsonRollback 的方法，只適用於 3.8 降級到 3.7。
如果在 3.7 的 SkeletonViewer, 下 JsonRollback 的指令，Json 資料會被 Roll back 到不知道哪個版本的資料(我稍微看了一下，推測是 2.1 左右)。

關鍵其實就在於，如果你使用了某版本才有的功能，那麼在舊版你就會打不開這些資料。

而比較好判讀這些新功能的地方，就在 AttachmentType 這個資訊。以下使用 3.7 版 c# SDK 來做說明:

在 3.7 版 c# 的 SDK 裡面， AttachmentType 被記錄在:

        spine-csharp/src/Attachments/AttachmentType.cs 

這個 cs 檔案裡面，內容是:

    public enum AttachmentType {
		Region, Boundingbox, Mesh, Linkedmesh, Path, Point, Clipping
	}

這些資訊，會對應到的是 Spine 輸出的 .json 資源裡面的 "type" : 這個欄位的內容。

如果新版本的資源，使用到了當前版本才有的功能，則在舊版就會無法讀取。

以下舉例說明:

3.6 版本的 AttachmentType, 跟 3.7 一模一樣，所以輸出的資源其實是可以共用的。以下是 3.6 版 AttachmentType 內容: 

https://github.com/EsotericSoftware/spine-runtimes/blob/3.6.53/spine-csharp/src/Attachments/AttachmentType.cs

    public enum AttachmentType {
		Region, Boundingbox, Mesh, Linkedmesh, Path, Point, Clipping
	}

再來 3.5 版的 AttachmentType 內容: 

https://github.com/EsotericSoftware/spine-runtimes/blob/3.5.51/spine-csharp/src/Attachments/AttachmentType.cs

    public enum AttachmentType {
		Region, Boundingbox, Mesh, Linkedmesh, Path
	}


可以看到 3.5 版少了 Point 以及 Clipping, 代表只要你的 3.7(or 3.6) Spine 裡面有用到這兩個功能的話，3.5 版就會讀取不了這個資源。

所以解決的方法很簡單:

1. 用 3.7 版本 Spine Editor 開啟 Spine 檔案，將使用到的新的功能移除 (Point, Clipping)，然後重新輸出檔案。理論上這個不含新功能的資源，就可以被 3.5 讀取了(不論是 editor, 或者是 runtime sdk)

2. 直接修改輸出後的 .json 檔案。直接把有使用到 Point, Clipping 的節點都刪除。關鍵字大概是 "type": "point"或者 "type": "clipping"

3. 如果你只有 binary 的檔案，那也可以先把 binary 導入到 Spine Editor 裡面，然後刪除新功能之後，再使用 Spine Editor 重新輸出。

當然刪除掉新的功能後，Spine 可能會有需要調整某些東西，這些就自己評估了。

# 關於 Spine 3.4 版本重要說明 
這是一個很有問題的版本，能不用就別用了。官方都廢棄了，乾脆不說明有這個版本。

明明 3.5, 3.6, 3.7 都有 Path 功能，但是就在 3.4 版打開的時候，Path 會有問題。

別用了吧。