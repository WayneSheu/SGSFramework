/* =============================================================================
   PhysLIMS 日誌庫年度換庫（冷熱分層）：熱庫 PhysLIMSLogDB → 冷庫 PhysLIMSLogDB_<年>，並重建全新熱庫
   執行身分：DBA (sysadmin)    建議於維護時段每年執行一次（例如年初）

   為什麼要換庫：日誌資料表為 SQL Ledger APPEND_ONLY，資料庫禁止 DELETE，舊資料無法從熱庫搬出，
   只能整庫換新、舊庫轉為唯讀冷庫（READ_ONLY，僅供查閱）。

   流程（本腳本）：
     1. 前置檢查（熱庫存在、冷庫名稱未被使用、備份目錄存在）
     2. 完整備份熱庫並以 CHECKSUM 驗證備份檔
     3. 將備份還原為冷庫（以 MOVE 指定新的實體檔名，避免與熱庫檔案衝突）
     4. 核對冷庫三張日誌表筆數與熱庫一致，並驗證冷庫 Ledger 完整性
     5. 冷庫設為 READ_ONLY
     6. 【不可逆，需 @DropHot = 1】刪除舊熱庫（僅在步驟 1~5 全數成功時才會執行）
   刪除舊熱庫後的後續步驟（腳本結尾會列出）：
     - 在 appsettings / 環境變數登記冷庫：LogArchive__Databases__N__Key / __Database / __DisplayName
     - 建立全新熱庫：執行 20261105_create_log_database.sql（或由 API 首次啟動自動建庫），重啟 API
     - 換庫期間日誌寫入失敗時，系統 / 資安日誌暫存於 Log_Buffer、資料日誌暫存於 Audit_Failures，重啟後自動回補
     - 資料日誌雜湊鏈：新熱庫某 Schema 的第一筆會自動以最新冷庫該 Schema 最後一筆 StoredHash 銜接，鏈跨庫連續

   安全機制：
     - @Execute = 0（預設）只列印將執行的動作，不做任何變更；@DropHot 預設 0
     - 任何一步失敗（備份目錄不存在、備份 / 還原錯誤、筆數不符、Ledger 驗證失敗）立即中止整個批次，不會繼續往下、更不會刪除熱庫
       （SET XACT_ABORT ON + TRY...CATCH；T-SQL 一般錯誤預設不會中止批次，不能只靠 SSMS 訊息判斷成敗）
     - 筆數核對對 NULL 保守處理（查詢失敗視為不一致）
     - 刪除熱庫前再次確認：冷庫存在、為 READ_ONLY，且本次所有核對皆已通過
   冷庫轉為唯讀後不再備份排程（排程只備份熱庫）；請於本流程的備份檔另行保存並做一次還原演練。
   ============================================================================= */
USE [master];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* ----- 參數（請依實際環境修改） ----- */
DECLARE @HotDb     sysname       = N'PhysLIMSLogDB';
DECLARE @ColdDb    sysname       = N'PhysLIMSLogDB_2026';    -- 冷庫名稱：建議 PhysLIMSLogDB_<被封存的年度>
DECLARE @BackupDir nvarchar(260) = NULL;                    -- NULL = 使用此執行個體的預設備份目錄；或指定 SQL Server「主機」上已存在的目錄
DECLARE @Execute   bit           = 0;                        -- 0 = 僅預覽；1 = 實際執行備份 / 還原 / 核對 / 唯讀
DECLARE @DropHot   bit           = 0;                        -- 1 = 以上全數通過後刪除舊熱庫（不可逆）

-- 未指定備份目錄時使用執行個體預設備份目錄（SQL Server 服務帳號必定可寫）；補上結尾的 \
SET @BackupDir = ISNULL(NULLIF(@BackupDir, N''), CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(260)));
IF @BackupDir IS NOT NULL AND RIGHT(@BackupDir, 1) <> N'\' SET @BackupDir = @BackupDir + N'\';

DECLARE @bak nvarchar(520) = @BackupDir + @HotDb + N'_before_rotation_' + FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmmss') + N'.bak';
DECLARE @sql nvarchar(max);
DECLARE @verified bit = 0;   -- 步驟 1~5 全數成功後才會設為 1
DECLARE @resume   bit = 0;   -- 續跑模式：冷庫已存在且為 READ_ONLY、@DropHot = 1 時，略過備份 / 還原，只核對筆數後刪除舊熱庫

BEGIN TRY
    /* ----- 1. 前置檢查 ----- */
    IF DB_ID(@HotDb) IS NULL THROW 50001, N'熱庫不存在，請確認 @HotDb。', 1;
    IF DB_ID(@ColdDb) IS NOT NULL
    BEGIN
        -- 冷庫已存在：僅在「@DropHot = 1 且冷庫為 READ_ONLY」時進入續跑模式（上一次已成功建立冷庫，現在只差刪除舊熱庫）
        IF @DropHot = 1 AND CAST(DATABASEPROPERTYEX(@ColdDb, 'Updateability') AS nvarchar(20)) = N'READ_ONLY'
            SET @resume = 1;
        ELSE
            THROW 50002, N'冷庫名稱已存在（不可覆寫既有資料庫）。若要刪除舊熱庫，請設 @DropHot = 1（冷庫需為 READ_ONLY）；否則請更換 @ColdDb。', 1;
    END;
    IF @BackupDir IS NULL THROW 50003, N'無法取得預設備份目錄，請明確指定 @BackupDir（SQL Server 主機上已存在的目錄）。', 1;

    DECLARE @fx TABLE (FileExists int, IsDirectory int, ParentExists int);
    INSERT @fx EXEC master.dbo.xp_fileexist @BackupDir;
    IF NOT EXISTS (SELECT 1 FROM @fx WHERE IsDirectory = 1)
        THROW 50006, N'備份目錄不存在（需為 SQL Server 主機上已建立的目錄）。請先建立目錄或修改 @BackupDir。', 1;

    PRINT N'== 計畫 ==';
    IF @resume = 1
        PRINT N'續跑模式：冷庫 ' + @ColdDb + N' 已存在且為 READ_ONLY，略過備份 / 還原，僅核對筆數後刪除舊熱庫 ' + @HotDb;
    ELSE
        PRINT N'熱庫 ' + @HotDb + N' → 備份 ' + @bak + N' → 還原為冷庫 ' + @ColdDb + N'（READ_ONLY）';
    PRINT CASE WHEN @DropHot = 1 THEN N'核對通過後將【刪除】舊熱庫 ' + @HotDb ELSE N'@DropHot = 0：不會刪除舊熱庫' END;

    IF @Execute = 0
    BEGIN
        PRINT N'@Execute = 0：僅預覽，前置檢查通過，未做任何變更。確認參數後設為 1 再執行。';
        RETURN;
    END;

    IF @resume = 0
    BEGIN
    /* ----- 2. 完整備份熱庫並驗證 ----- */
    SET @sql = N'BACKUP DATABASE ' + QUOTENAME(@HotDb) + N' TO DISK = @bak WITH COPY_ONLY, CHECKSUM, INIT, STATS = 10;';
    EXEC sp_executesql @sql, N'@bak nvarchar(520)', @bak = @bak;

    RESTORE VERIFYONLY FROM DISK = @bak WITH CHECKSUM;
    PRINT N'備份完成並驗證：' + @bak;

    /* ----- 3. 還原為冷庫（以 MOVE 指定新的實體檔名） ----- */
    DECLARE @move nvarchar(max) =
    (
        SELECT STRING_AGG(
            N'MOVE N''' + REPLACE(name, '''', '''''') + N''' TO N''' +
            REPLACE(REPLACE(physical_name, @HotDb, @ColdDb), '''', '''''') + N'''', N', ')
        FROM sys.master_files
        WHERE database_id = DB_ID(@HotDb)
    );

    IF @move IS NULL OR @move NOT LIKE N'%' + @ColdDb + N'%'
        THROW 50004, N'無法由熱庫實體檔名推導冷庫檔名（實體檔名未包含熱庫名稱）。請改用手動 RESTORE ... WITH MOVE。', 1;

    SET @sql = N'RESTORE DATABASE ' + QUOTENAME(@ColdDb) + N' FROM DISK = @bak WITH CHECKSUM, ' + @move + N', STATS = 10;';
    EXEC sp_executesql @sql, N'@bak nvarchar(520)', @bak = @bak;

    -- 還原是否真的成功：以資料庫是否存在為準，不依賴訊息文字
    IF DB_ID(@ColdDb) IS NULL THROW 50007, N'還原後找不到冷庫，已中止。', 1;
    PRINT N'已還原冷庫：' + @ColdDb;
    END;

    /* ----- 4. 核對筆數與 Ledger 完整性 ----- */
    DECLARE @tables TABLE (t sysname);
    INSERT @tables VALUES (N'SystemLogs'), (N'SecurityLog'), (N'AuditLogs');

    DECLARE @t sysname, @hotN bigint, @coldN bigint, @cnt nvarchar(max);
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT t FROM @tables;
    OPEN c;
    FETCH NEXT FROM c INTO @t;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @hotN = NULL; SET @coldN = NULL;

        SET @cnt = N'SELECT @n = COUNT_BIG(*) FROM ' + QUOTENAME(@HotDb) + N'.[log].' + QUOTENAME(@t) + N';';
        EXEC sp_executesql @cnt, N'@n bigint OUTPUT', @n = @hotN OUTPUT;
        SET @cnt = N'SELECT @n = COUNT_BIG(*) FROM ' + QUOTENAME(@ColdDb) + N'.[log].' + QUOTENAME(@t) + N';';
        EXEC sp_executesql @cnt, N'@n bigint OUTPUT', @n = @coldN OUTPUT;

        PRINT N'核對 log.' + @t + N'：熱庫 ' + ISNULL(CAST(@hotN AS nvarchar(30)), N'(無法讀取)')
            + N' / 冷庫 ' + ISNULL(CAST(@coldN AS nvarchar(30)), N'(無法讀取)');

        -- NULL 一律視為不一致（避免查詢失敗時 <> 比較為未知而放行）
        IF @hotN IS NULL OR @coldN IS NULL OR @hotN <> @coldN
            THROW 50005, N'冷庫筆數與熱庫不一致或無法核對，已中止（未刪除任何資料）。若 API 仍在寫入日誌，熱庫會比冷庫新：請先停止 API（或應用程式集區）再重新執行，避免刪除熱庫時遺失備份之後寫入的日誌。', 1;

        FETCH NEXT FROM c INTO @t;
    END;
    CLOSE c;
    DEALLOCATE c;

    IF @resume = 0
    BEGIN
    -- Ledger 完整性：產生冷庫目前摘要並驗證（失敗會擲出錯誤並中止）
    -- （須在轉為 READ_ONLY 之前執行：產生摘要會寫入資料庫）
    SET @sql = N'USE ' + QUOTENAME(@ColdDb) + N';
        CREATE TABLE #digest (digest nvarchar(max));
        INSERT INTO #digest EXEC sys.sp_generate_database_ledger_digest;
        DECLARE @d nvarchar(max) = (SELECT TOP 1 digest FROM #digest);
        EXEC sys.sp_verify_database_ledger @d;';
    EXEC (@sql);
    PRINT N'冷庫 Ledger 驗證通過。';

    /* ----- 5. 冷庫轉唯讀 ----- */
    SET @sql = N'ALTER DATABASE ' + QUOTENAME(@ColdDb) + N' SET READ_ONLY WITH ROLLBACK IMMEDIATE;';
    EXEC (@sql);

    IF ISNULL(CAST(DATABASEPROPERTYEX(@ColdDb, 'Updateability') AS nvarchar(20)), N'') <> N'READ_ONLY'
        THROW 50008, N'冷庫未能設為 READ_ONLY，已中止。', 1;
    PRINT N'冷庫已設為 READ_ONLY：' + @ColdDb;
    END;

    IF ISNULL(CAST(DATABASEPROPERTYEX(@ColdDb, 'Updateability') AS nvarchar(20)), N'') <> N'READ_ONLY'
        THROW 50009, N'冷庫不是 READ_ONLY，已中止。', 1;

    SET @verified = 1;   -- 步驟 1~5 全數成功

    /* ----- 6. 刪除舊熱庫（不可逆）：僅在全數核對通過後 ----- */
    IF @DropHot = 1 AND @verified = 1
        AND DB_ID(@ColdDb) IS NOT NULL
        AND CAST(DATABASEPROPERTYEX(@ColdDb, 'Updateability') AS nvarchar(20)) = N'READ_ONLY'
    BEGIN
        SET @sql = N'ALTER DATABASE ' + QUOTENAME(@HotDb) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@HotDb) + N';';
        EXEC (@sql);
        PRINT N'已刪除舊熱庫：' + @HotDb;
    END
    ELSE
        PRINT N'@DropHot = 0：舊熱庫保留。確認冷庫可查閱後，【先停止 API】，再將 @DropHot 設為 1 重新執行（續跑模式：略過備份 / 還原，核對筆數一致後才刪除舊熱庫）。';

    PRINT N'';
    PRINT N'== 後續步驟 ==';
    PRINT N'1) 登記冷庫：LogArchive__Databases__N__Key=<年度>、__Database=' + @ColdDb + N'、__DisplayName=<顯示名稱>（順序由舊到新）';
    PRINT N'2) 建立新熱庫：執行 20261105_create_log_database.sql（建庫、帳號、備份帳號授權），或由 API 啟動自動建庫';
    PRINT N'3) 重啟 API：自動建立新熱庫資料表、回補換庫期間的緩衝日誌；日誌頁出現「冷資料」來源';
    PRINT N'4) 保存本次備份檔並做一次還原演練：' + @bak;
END TRY
BEGIN CATCH
    DECLARE @msg nvarchar(2048) = N'換庫中止：' + ERROR_MESSAGE() + N'（錯誤 ' + CAST(ERROR_NUMBER() AS nvarchar(20)) + N'，第 ' + CAST(ERROR_LINE() AS nvarchar(20)) + N' 行）。'
        + N' 未刪除任何資料；若已建立冷庫 ' + @ColdDb + N'，請確認後手動刪除再重新執行。';
    THROW 50099, @msg, 1;
END CATCH;
GO
