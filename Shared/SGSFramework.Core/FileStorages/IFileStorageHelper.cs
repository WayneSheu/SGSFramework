namespace SGSFramework.Core.FileStorages
{
    public interface IFileStorageHelper<TEntity> where TEntity : class
    {
        /// <summary>
        /// 儲存檔案並回傳可用於資料庫的 Link
        /// </summary>
        /// <param name="fileStream">檔案串流</param>
        /// <param name="originalFileName">原始檔名</param>
        /// <param name="subFolder">可選的次級資料夾名稱 (如：ActivityData, Evidence...)</param>
        Task<string> SaveAsync(Stream fileStream, string originalFileName, string? subFolder = null);

        /// <summary>
        /// 根據 Link 取得實體物理路徑 (用於解析檔案)
        /// </summary>
        string GetPhysicalPath(string link);
    }
}
