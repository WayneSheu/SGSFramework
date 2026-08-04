using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Mask
{
    // 定義遮罩格式列舉，方便在 Attribute 中指定
    public enum MaskFormat
    {
        [Display(Name = "預設遮蔽 (***MASKED***)")]
        Default,
        [Display(Name = "姓名遮蔽 (陳○明)")]
        Name,
        [Display(Name = "手機遮蔽 (0912***678)")]
        Phone,
        [Display(Name = "信箱遮蔽(g***@google.com)")]
        Email,
        [Display(Name = "地址遮蔽 (台北市信義區*****)")]
        Address,
        [Display(Name = "身分證 (A12***789)")]
        IdCard,
        [Display(Name = "信用卡號")]
        CreditCard,
        [Display(Name = "銀行帳號")]
        BankAccount,
        [Display(Name = "統一編號")]
        TaxId,
        [Display(Name = "發票載具")]
        InvoiceCarrier
    }
}
