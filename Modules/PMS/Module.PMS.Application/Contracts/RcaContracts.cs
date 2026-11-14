using System.Text.Json.Serialization;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Contracts;

/// <summary>目前使用者對此 RCA 可執行的動作（供 UI 顯示按鈕；伺服器端仍會重新驗證）。</summary>
public sealed record RcaActionsDto(bool CanEdit, bool CanSubmit, bool CanSend);

/// <summary>
/// 問題單的 RCA 狀態。<see cref="Exists"/> 為 false 表示尚未建立：此時只有 <see cref="EligibleTriggers"/> 與問題單資訊有意義，
/// 供 UI 顯示「新增 RCA」表單；不存在也回 200（避免 UI 以 404 判斷）。
/// </summary>
public sealed record RcaDto(
    bool Exists, Guid? Id, Guid TicketId, string TicketNo, string TicketTitle, Guid ProjectId, string ProjectCode, string ProjectName,
    IReadOnlyList<RcaTrigger> EligibleTriggers, RcaTrigger? Trigger, RcaStatus? Status,
    string Summary, DateTimeOffset? RecoveredAt, DateTimeOffset? DueAt, bool IsOverdue,
    IReadOnlyList<RcaTimelineItem> Timeline, string RootCause, string ShortTermAction, string LongTermPrevention,
    IReadOnlyList<string> Warnings, Guid? SubmittedBy, DateTimeOffset? SubmittedAt, Guid? SentBy, DateTimeOffset? SentAt, string? SentNote,
    string? SentTo, bool HasSentPdf, bool CanEmail, RcaActionsDto Actions, string? RowVersion);

/// <param name="RowVersion">更新既有 RCA 時必填；新建時留空。</param>
public sealed record UpsertRcaRequest(
    RcaTrigger Trigger, DateTimeOffset RecoveredAt, string? Summary, IReadOnlyList<RcaTimelineItem>? Timeline,
    string? RootCause, string? ShortTermAction, string? LongTermPrevention, string? RowVersion);

public sealed record SubmitRcaRequest(string RowVersion);

/// <summary>寄送方式：系統代寄（Email＋PDF 附件）或僅記錄（PM 已自行寄出）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RcaSendMode
{
    Email = 0,
    RecordOnly = 1
}

/// <param name="Mode">省略時為僅記錄（維持原行為）。</param>
/// <param name="To">Email 模式的收件人（必填，最多 10 位）。</param>
/// <param name="Cc">Email 模式的副本（選填，最多 10 位）。</param>
/// <param name="Message">Email 模式附在固定說明前的補充文字（選填，最多 1000 字）。</param>
public sealed record SendRcaRequest(
    string? Note, string RowVersion, RcaSendMode Mode = RcaSendMode.RecordOnly,
    IReadOnlyList<string>? To = null, IReadOnlyList<string>? Cc = null, string? Message = null);

/// <summary>RCA PDF 檔案內容。</summary>
public sealed record RcaPdfFile(byte[] Content, string FileName);
