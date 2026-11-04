using Microsoft.Extensions.Logging;
using SGSFramework.Core.Paginations;

namespace SGSFramework.Core.FileManagement;

/// <summary>
/// 檔案管理服務之 Fallback (Null Object) 實作。
/// 當系統未載入 SGS.Modules.FMS 外掛模組時由此服務接管：寫入 / 讀取一律回應 503（模組未安裝），查詢回傳空結果。
/// </summary>
public sealed class NullFileManagementService(ILogger<NullFileManagementService> logger) : IFileManagementService
{
    private const string NotInstalled = "檔案管理模組 (SGS.Modules.FMS) 未安裝或未載入。";

    public Task<FileMetadataDto> UploadAsync(FileUploadRequest request, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default) =>
        throw Unavailable(nameof(UploadAsync));

    public Task<FileMetadataDto?> GetAsync(Guid id, bool includeDeleted = false, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default) =>
        throw Unavailable(nameof(GetAsync));

    public Task<PagedResult<FileMetadataDto>> SearchAsync(FileQueryRequest request, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        LogWarning(nameof(SearchAsync));
        return Task.FromResult(new PagedResult<FileMetadataDto>([], 0, request.PageIndex, request.PageSize));
    }

    public Task<FileDownload?> OpenDownloadAsync(Guid id, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default) =>
        throw Unavailable(nameof(OpenDownloadAsync));

    public Task<FileImageContent?> GetImageAsync(Guid id, int? width, int? height, string? mode, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default) =>
        throw Unavailable(nameof(GetImageAsync));

    public Task<bool> DeleteAsync(Guid id, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default) =>
        throw Unavailable(nameof(DeleteAsync));

    public Task<FilePurgeResult> PurgeDeletedAsync(CancellationToken cancellationToken = default) =>
        throw Unavailable(nameof(PurgeDeletedAsync));

    private FileRejectedException Unavailable(string method)
    {
        LogWarning(method);
        return new FileRejectedException(NotInstalled, 503);
    }

    private void LogWarning(string method) =>
        logger.LogWarning("[NullFileManagementService] 呼叫 {Method} 失敗：{Reason}", method, NotInstalled);
}
