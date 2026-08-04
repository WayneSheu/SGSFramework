namespace SGSFramework.Core.HttpAuditProviders
{
    /// <summary>
    /// 定義用於提供稽核相關資訊的介面，
    /// 包括使用者識別、追蹤識別碼與遠端 IP 位址等屬性。此介面可用於稽核記錄、追蹤或安全性相關的應用場景。
    /// </summary>
    /// <remarks>實作此介面時，應確保屬性值能正確反映目前操作的使用者與請求來源。
    /// 部分屬性（如 UserName）為選用，僅在需要顯示名稱時提供。</remarks>
    public interface IAuditProvider
    {
        string? UserId { get; }
        string? UserName { get; }
        string? TraceId { get; }
        string? RemoteIp { get; }
        string? DeviceId { get; }      // 修正：補齊系統升級所需之設備識別碼
        string? LaboratoryId { get; }  // 修正：補齊系統升級所需之實驗室識別碼

        ///// <summary>
        ///// 取得當前操作者識別碼
        ///// </summary>
        ///// <returns>使用者識別碼，若無法識別則傳回 null</returns>
        //string? GetCurrentUserId();

        ///// <summary>
        ///// 取得當前操作者名稱
        ///// </summary>
        ///// <returns>使用者名稱，若無法識別則傳回 null</returns>
        //string? GetCurrentUserName();

    }
}
