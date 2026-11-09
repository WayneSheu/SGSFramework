using SGS.Modules.FMS.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Application.Services;

/// <summary>實體 → 契約 DTO（不含容器、儲存鍵等實體儲存屬性）</summary>
public static class FileMetadataMapping
{
    public static FileMetadataDto ToDto(this FileMetadata e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new FileMetadataDto(
            e.Id, e.OriginalFileName, e.Extension, e.ContentType, e.FileSize, e.Sha256, e.Category, e.StorageTarget,
            e.ReferenceType, e.ReferenceId, e.ImageWidth, e.ImageHeight, e.IsDeleted, e.DeletedAtUtc, e.CreatedBy, e.CreatedAtUtc,
            IsEncrypted: e.EncryptionVersion is not null);
    }
}
