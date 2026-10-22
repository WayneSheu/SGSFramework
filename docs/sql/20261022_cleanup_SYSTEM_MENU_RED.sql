/* =====================================================================================
   清除拼字錯誤的權限碼 SYSTEM.MENU.RED (已於 MenuItemController 更正為 SYSTEM.MENU.READ)
   -------------------------------------------------------------------------------------
   影響資料表 (schema: core)
     1. PermissionMetadata  : 刪除 PermissionKey = 'SYSTEM.MENU.RED' 的殘留列
                              (PermissionSeedService 只新增不刪除；ParentId 為 Restrict FK，需先解除子節點參照)
     2. MenuItems           : 由 MenuSeedService 以 PermissionKey 作為 Key 建立的頁面節點
                              - 尚無 'SYSTEM.MENU.READ' 節點 → 直接改名沿用 (保留路由、圖示、排序等手動設定)
                              - 已有 'SYSTEM.MENU.READ' 節點 (API 已以新程式碼重啟過) → 刪除舊節點 (僅限無子節點)
                              - 其他節點的 PermissionKey 為 RED 者 → 改為 READ
     3. ControllerMetadatas : 不處理，API 啟動時 ControllerScannerService 會以新 Attribute 覆寫 PermissionKey

   執行方式
     1. 建議先停止 PhysLIMS.API，並備份資料庫
     2. 先以 @Commit = 0 執行 (預設)：只顯示預覽與異動結果，最後 ROLLBACK
     3. 確認無誤後改為 @Commit = 1 再執行一次
     4. 啟動 PhysLIMS.API：PermissionSeedService 會重新計算 PermissionMetadata 階層 (ParentId / NodePath / Level)
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Commit  bit           = 0;                  -- 0 = 試跑 (ROLLBACK)，1 = 正式提交
DECLARE @OldKey  nvarchar(200) = N'SYSTEM.MENU.RED';
DECLARE @NewKey  nvarchar(200) = N'SYSTEM.MENU.READ';

/* ---------- 0. 預覽 ---------- */
PRINT N'== [預覽] PermissionMetadata 殘留列 ==';
SELECT Id, PermissionKey, ControllerName, ActionName, BitPosition, ParentId, NodePath
FROM core.PermissionMetadata
WHERE PermissionKey = @OldKey;

PRINT N'== [預覽] 以殘留列為父節點的 PermissionMetadata ==';
SELECT c.Id, c.PermissionKey, c.ControllerName, c.ActionName, c.ParentId
FROM core.PermissionMetadata c
WHERE c.ParentId IN (SELECT Id FROM core.PermissionMetadata WHERE PermissionKey = @OldKey);

PRINT N'== [預覽] MenuItems (Key 或 PermissionKey 為新舊權限碼) ==';
SELECT m.Id, m.[Key], m.PermissionKey, m.DisplayName, m.Route, m.IsActive, m.IsVisible,
       (SELECT COUNT(*) FROM core.MenuItems ch WHERE ch.ParentId = m.Id) AS ChildCount
FROM core.MenuItems m
WHERE m.[Key] IN (@OldKey, @NewKey) OR m.PermissionKey IN (@OldKey, @NewKey);

PRINT N'== [參考] 其他仍引用舊權限碼的資料 (不自動修改，請人工判斷) ==';
SELECT N'SodRules' AS [Table], COUNT(*) AS Cnt FROM core.SodRules
 WHERE PermissionKeyA = @OldKey OR PermissionKeyB = @OldKey
UNION ALL
SELECT N'UserGlobalPermissions', COUNT(*) FROM core.UserGlobalPermissions WHERE PermissionKey = @OldKey
UNION ALL
SELECT N'Role_Global_Permissions', COUNT(*) FROM core.Role_Global_Permissions WHERE PermissionKey = @OldKey;

/* ---------- 1~3. 異動 ---------- */
BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. 解除子節點對殘留列的父參照 (API 啟動時會重新計算正確階層)
    UPDATE c
       SET c.ParentId = NULL
      FROM core.PermissionMetadata c
     WHERE c.ParentId IN (SELECT Id FROM core.PermissionMetadata WHERE PermissionKey = @OldKey);
    PRINT N'PermissionMetadata 解除父參照: ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' 筆';

    -- 2. 刪除殘留的權限中繼資料
    DELETE FROM core.PermissionMetadata WHERE PermissionKey = @OldKey;
    PRINT N'PermissionMetadata 刪除: ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' 筆';

    -- 3. 選單節點
    IF NOT EXISTS (SELECT 1 FROM core.MenuItems WHERE [Key] = @NewKey)
    BEGIN
        -- 3a. 尚未建立新節點：沿用舊節點改名，避免下次同步產生重複選單
        UPDATE core.MenuItems
           SET [Key] = @NewKey
         WHERE [Key] = @OldKey;
        PRINT N'MenuItems 改名 Key: ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' 筆';
    END
    ELSE
    BEGIN
        -- 3b. 已有新節點：刪除舊節點 (含子節點時保留，請改由選單管理頁轉移子節點後再處理)
        DELETE m
          FROM core.MenuItems m
         WHERE m.[Key] = @OldKey
           AND NOT EXISTS (SELECT 1 FROM core.MenuItems ch WHERE ch.ParentId = m.Id);
        PRINT N'MenuItems 刪除重複舊節點: ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' 筆';

        IF EXISTS (SELECT 1 FROM core.MenuItems WHERE [Key] = @OldKey)
            PRINT N'[警告] 舊節點仍有子節點，未刪除，請於選單管理頁轉移子節點後再處理。';
    END

    UPDATE core.MenuItems
       SET PermissionKey = @NewKey
     WHERE PermissionKey = @OldKey;
    PRINT N'MenuItems 更新 PermissionKey: ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' 筆';

    /* ---------- 異動後結果 ---------- */
    PRINT N'== [結果] MenuItems ==';
    SELECT m.Id, m.[Key], m.PermissionKey, m.DisplayName, m.Route, m.IsActive, m.IsVisible
    FROM core.MenuItems m
    WHERE m.[Key] IN (@OldKey, @NewKey) OR m.PermissionKey IN (@OldKey, @NewKey);

    IF @Commit = 1
    BEGIN
        COMMIT TRANSACTION;
        PRINT N'>>> 已提交 (COMMIT)。請啟動 PhysLIMS.API 以重新計算權限階層。';
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'>>> 試跑模式，已全部還原 (ROLLBACK)。確認無誤後將 @Commit 設為 1 再執行。';
    END
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'>>> 執行失敗，已還原：' + ERROR_MESSAGE();
    THROW;
END CATCH;
