#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.ReportCategories
{
    using System;

    /// <summary>
    /// 報告類別授權動作（可組合）
    /// </summary>
    [Flags]
    public enum ReportActions
    {
        None = 0,
        /// <summary>瀏覽</summary>
        View = 1,
        /// <summary>編輯 / 數據登錄</summary>
        Edit = 2,
        /// <summary>審核（核可 / 退回）</summary>
        Review = 4,
        /// <summary>准發 / 簽署</summary>
        Sign = 8,
        /// <summary>作廢 / 重發核准</summary>
        Void = 16,
        /// <summary>下載正式報告</summary>
        Download = 32,
        /// <summary>檢視稽核軌跡</summary>
        ViewAudit = 64,
        /// <summary>檢視盲樣之客戶 / 來源識別資訊</summary>
        ViewBlindIdentity = 128
    }

    /// <summary>
    /// 報告法規密級
    /// </summary>
    public enum ConfidentialityLevel
    {
        /// <summary>常規報告</summary>
        Normal = 0,
        /// <summary>機密報告</summary>
        Confidential = 1,
        /// <summary>特檢 / 高度機密（僅限明確指派人員）</summary>
        Restricted = 2
    }

    /// <summary>
    /// 授權資料範圍
    /// </summary>
    public enum ReportDataScope
    {
        /// <summary>僅限被指派（經手）之報告</summary>
        Assigned = 0,
        /// <summary>所屬區域實驗室（分公司）內全部報告</summary>
        Lab = 1,
        /// <summary>跨分公司全域</summary>
        Global = 2
    }

    /// <summary>
    /// 授權對象類型
    /// </summary>
    public enum GrantPrincipalType
    {
        User = 0,
        Role = 1
    }

    /// <summary>
    /// 報告生命週期狀態
    /// </summary>
    public enum ReportLifecycleStatus
    {
        Draft = 0,
        InReview = 1,
        Reviewed = 2,
        Published = 3,
        Voided = 4
    }
}
