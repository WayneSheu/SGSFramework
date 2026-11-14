namespace SGSFramework.Identity.DTOs;

/// <summary>
/// 使用者顯示名稱（僅姓名與帳號，不含 Email 等其他欄位）。供所有已登入使用者把使用者識別碼解析成可讀名稱，
/// 不需要「使用者管理」檢視權限。
/// </summary>
public sealed class UserNameDto
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
}

/// <summary>批次查詢使用者名稱的請求（一次最多 200 筆）。</summary>
public sealed class UserNamesRequest
{
    public List<Guid> UserIds { get; set; } = [];
}
