namespace SGSFramework.Core.ProjectContracts;

/// <summary>報價付款里程碑（轉入合約時作為付款期程依據）。</summary>
public sealed record QuoteContractMilestone(int Seq, string Name, decimal Percent, decimal Amount, string Trigger);

/// <summary>報價 WBS 執行基準項目（SOP 1.2 交付基準對齊原則）。</summary>
public sealed record QuoteContractWbsItem(string WbsCode, string Name, string? Deliverable, string Phase, decimal QuotedDays);

/// <summary>由已成交報價建立專案合約的請求（報價模組 → 專案管理模組）。</summary>
/// <param name="QuoteVersionId">冪等鍵：同一報價版本只會建立一份合約。</param>
/// <param name="TotalAmount">合約總價金（新台幣，未稅）。</param>
/// <param name="SalesUserId">以此業務身分建立合約（成為合約業務成員）。</param>
/// <param name="PmUserId">合約 PM（可為 null）。</param>
public sealed record QuoteContractRequest(
    Guid QuoteVersionId,
    string QuoteNo,
    string VersionLabel,
    string Title,
    string CustomerName,
    string? CustomerContactName,
    string? CustomerContactEmail,
    decimal TotalAmount,
    DateOnly? QuoteDate,
    DateOnly? StartDate,
    DateOnly? DeliveryDate,
    int WarrantyMonths,
    bool IpBuyout,
    IReadOnlyList<QuoteContractMilestone> Milestones,
    IReadOnlyList<QuoteContractWbsItem> Wbs,
    Guid SalesUserId,
    Guid? PmUserId,
    string Actor,
    string? Notes);

public sealed record QuoteContractResult(bool Succeeded, Guid? ContractId, string? ContractNo, string? Error, int StatusCode = 200)
{
    public static QuoteContractResult Success(Guid contractId, string contractNo) => new(true, contractId, contractNo, null);

    public static QuoteContractResult Failure(string error, int statusCode = 400) => new(false, null, null, error, statusCode);
}

/// <summary>
/// 報價 → 專案合約整合（Core 契約）：由 SGS.Modules.PMS 外掛實作，報價模組（SGS.Modules.QTN）只依賴此介面，不直接參照 PMS。
/// PMS 未安裝時由 <see cref="NullQuoteContractIntegration"/> 接管並回報 503。
/// </summary>
public interface IQuoteContractIntegration
{
    bool IsAvailable { get; }

    Task<QuoteContractResult> CreateContractAsync(QuoteContractRequest request, CancellationToken cancellationToken = default);
}

/// <summary>未載入 SGS.Modules.PMS 時的預設實作：不可建立合約（503）。</summary>
public sealed class NullQuoteContractIntegration : IQuoteContractIntegration
{
    public bool IsAvailable => false;

    public Task<QuoteContractResult> CreateContractAsync(QuoteContractRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(QuoteContractResult.Failure("專案管理模組（PMS）未安裝，無法自動建立合約。", 503));
}
