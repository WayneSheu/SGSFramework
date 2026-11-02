#nullable enable

namespace SGSFramework.Core.Abstractions.ReportAccess
{
    using SGSFramework.Core.Abstractions.Entities.ReportCategories;

    /// <summary>
    /// 報告生命週期狀態 × 動作矩陣：狀態決定當下允許的動作，與人員授權無關。
    /// 已發行報告轉為唯讀，僅可瀏覽、下載或啟動作廢流程。
    /// </summary>
    public static class ReportStatusActionMatrix
    {
        /// <summary>與狀態無關、任何狀態皆可執行之動作</summary>
        private const ReportActions StatusIndependent = ReportActions.ViewAudit | ReportActions.ViewBlindIdentity;

        public static ReportActions AllowedActions(ReportLifecycleStatus status) => StatusIndependent | status switch
        {
            ReportLifecycleStatus.Draft => ReportActions.View | ReportActions.Edit,
            ReportLifecycleStatus.InReview => ReportActions.View | ReportActions.Review,
            ReportLifecycleStatus.Reviewed => ReportActions.View | ReportActions.Sign,
            ReportLifecycleStatus.Published => ReportActions.View | ReportActions.Download | ReportActions.Void,
            ReportLifecycleStatus.Voided => ReportActions.View,
            _ => ReportActions.None
        };

        public static bool Allows(ReportLifecycleStatus status, ReportActions action) =>
            action != ReportActions.None && (AllowedActions(status) & action) == action;
    }
}
