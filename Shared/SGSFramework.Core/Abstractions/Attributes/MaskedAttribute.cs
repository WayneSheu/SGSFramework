using SGSFramework.Core.Mask;

namespace SGSFramework.Core.Abstractions.Attributes
{
    /// <summary>
    /// 標註敏感個資欄位：日誌輸出 (Serilog 解構) 與 IMaskService.MaskObject 時依格式遮蔽。
    /// 僅影響「顯示 / 記錄」，不影響資料庫儲存；需落地加密請併用 <see cref="EncryptedAttribute"/>。
    /// </summary>
    /// <example>
    /// [Masked(MaskFormat.Phone)] public string Mobile { get; set; }
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MaskedAttribute : SensitiveDataAttribute
    {
        public MaskedAttribute(MaskFormat format = MaskFormat.Default) : base(format)
        {
        }
    }
}
