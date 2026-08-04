using Microsoft.Extensions.Configuration;

namespace SGSFramework.Core.FileStorages
{
    public class LocalFileStorageHelper<TEntity> : IFileStorageHelper<TEntity> where TEntity : class
    {
        private readonly string _rootPath;

        public LocalFileStorageHelper(IConfiguration config)
        {
            // 從 appsettings.json 讀取根路徑，預設 D:\SES_Uploads
            _rootPath = config["Storage:LocalRoot"] ?? @"C:\SES_Uploads";
        }

        public async Task<string> SaveAsync(Stream fileStream, string originalFileName, string? subFolder = null)
        {
            // 1. 自動定位路徑：Root / EntityName / YearMonth / (subFolder)
            string entityFolder = typeof(TEntity).Name;
            string dateFolder = DateTime.UtcNow.ToString("yyyyMM");
            string relativePath = Path.Combine(entityFolder, dateFolder, subFolder ?? "");
            string targetDirectory = Path.Combine(_rootPath, relativePath);

            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            // 2. 產出唯一檔名
            string extension = Path.GetExtension(originalFileName);
            string uniqueName = $"{Guid.NewGuid():N}{extension}";
            string physicalPath = Path.Combine(targetDirectory, uniqueName);

            // 3. 寫入磁碟
            using (var outputStream = new FileStream(physicalPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(outputStream);
            }

            // 4. 回傳連結 (存入資料庫 RawFileLink)
            // 格式範例: /ActivityData/202604/unique_id.xlsx
            return $"/{relativePath.Replace('\\', '/')}/{uniqueName}";
        }

        public string GetPhysicalPath(string link)
        {
            // 將資料庫的 Link 轉回物理路徑供解析程式讀取
            return Path.Combine(_rootPath, link.TrimStart('/').Replace('/', '\\'));
        }
    }
}
