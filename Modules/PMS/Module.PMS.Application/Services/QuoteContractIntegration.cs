using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.ProjectContracts;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// Core <see cref="IQuoteContractIntegration"/> 的 PMS 實作（報價管理 QTN → 專案合約）：以報價業務身分建立「報價評估」狀態的合約，
/// 帶入客戶、總價（未稅）、保固、IP 歸屬與擬制驗收 10 個工作日（報價附錄 A），並指派合約 PM。
/// 付款期程（PMS 規定簽約後才能設定）與 WBS 執行基準寫入合約備註，作為簽約後設定付款期程與建立專案基準的依據。
/// 冪等：備註含 [QTN:{報價版本識別碼}] 標記，同一報價版本只建立一次。
/// </summary>
public sealed class QuoteContractIntegration(ContractService contracts, PMSDbContext db) : IQuoteContractIntegration
{
    private const int MaxNotes = 2000;

    public bool IsAvailable => true;

    public async Task<QuoteContractResult> CreateContractAsync(QuoteContractRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tag = $"[QTN:{request.QuoteVersionId:N}]";
        var existing = await db.Contracts.AsNoTracking().Where(c => c.Notes != null && c.Notes.Contains(tag))
            .Select(c => new { c.Id, c.ContractNo }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null) return QuoteContractResult.Success(existing.Id, existing.ContractNo);

        var terms = new ContractTermsDto(
            Title: Truncate(request.Title, 200), CustomerName: Truncate(request.CustomerName, 200),
            CustomerContactName: request.CustomerContactName is { Length: > 100 } n ? n[..100] : request.CustomerContactName,
            CustomerContactEmail: request.CustomerContactEmail,
            Type: ContractType.CustomApp, Industry: ContractIndustry.General, TotalAmount: request.TotalAmount,
            QuoteDate: request.QuoteDate, StartDate: request.StartDate, DeliveryDate: request.DeliveryDate,
            OpenEndedScope: false, ReviewDeadlineWorkdays: 7, MaxReviewRounds: 2, DeemedPassOnTimeout: true,
            IpRule: request.IpBuyout ? IpRule.AllToCustomer : IpRule.SplitFramework, ScanRule: ScanRule.HighMediumOnly,
            DeemedAcceptanceWorkdays: 10, FinalPaymentHeldForMinorDefects: false,
            LiabilityCap: LiabilityCapType.TotalPrice, LiabilityCapAmount: null, ExcludesIndirectLoss: true,
            WarrantyMonths: request.WarrantyMonths, IncidentNotifyHours: 24, AuditNoticeDays: 14, AuditPerYear: 1, AuditCostOnVendor: false,
            ServiceHours: "週一至週五 09:00–18:00（不含國定假日）", SupportChannel: null, Notes: BuildNotes(request, tag));

        var created = await contracts.CreateAsync(request.SalesUserId, request.Actor, new CreateContractRequest(terms), cancellationToken).ConfigureAwait(false);
        if (!created.IsSuccess) return QuoteContractResult.Failure(created.Error!.Detail, created.Error.Status);

        var contract = created.Value!;
        if (request.PmUserId is { } pm && pm != request.SalesUserId)
        {
            var members = await contracts.SetMembersAsync(request.SalesUserId, request.Actor, contract.Id, new SetContractMembersRequest(
                [new ContractMemberAssignment(request.SalesUserId, ContractMemberRole.Sales), new ContractMemberAssignment(pm, ContractMemberRole.Pm)]),
                cancellationToken).ConfigureAwait(false);
            if (!members.IsSuccess) return QuoteContractResult.Failure($"合約 {contract.ContractNo} 已建立，但指派合約 PM 失敗：{members.Error!.Detail}", members.Error.Status);
        }

        return QuoteContractResult.Success(contract.Id, contract.ContractNo);
    }

    private static string BuildNotes(QuoteContractRequest r, string tag)
    {
        var sb = new StringBuilder();
        sb.Append(tag).Append(' ').AppendLine(r.Notes ?? $"由報價 {r.QuoteNo} {r.VersionLabel} 建立");
        sb.AppendLine("【付款里程碑（簽約後於履約管理設定付款期程）】");
        foreach (var m in r.Milestones)
            sb.Append(CultureInfo.InvariantCulture, $"{m.Seq}. {m.Name} {m.Percent:0.##}%（NT$ {m.Amount:N0} 未稅）：{m.Trigger}").AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"【WBS 執行基準】共 {r.Wbs.Count} 項，合計 {r.Wbs.Sum(w => w.QuotedDays):0.##} 人天").AppendLine();
        foreach (var w in r.Wbs)
        {
            var line = string.Create(CultureInfo.InvariantCulture, $"{w.WbsCode} {w.Name}（{w.Phase}）{w.QuotedDays:0.##} 人天");
            if (sb.Length + line.Length + 20 > MaxNotes)
            {
                sb.AppendLine("…（其餘請見報價單）");
                break;
            }

            sb.AppendLine(line);
        }

        var text = sb.ToString().Trim();
        return text.Length > MaxNotes ? text[..MaxNotes] : text;
    }

    private static string Truncate(string v, int max) => string.IsNullOrWhiteSpace(v) ? "—" : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();
}
