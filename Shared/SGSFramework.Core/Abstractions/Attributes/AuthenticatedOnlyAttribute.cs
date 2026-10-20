using System;

namespace SGSFramework.Core.Abstractions.Attributes;

/// <summary>
/// 僅需登入即可存取，略過 Controller 層級的 <see cref="RequiresPermissionAttribute"/> 檢查。
/// 用於「取得自身資料」類端點（例如使用者自己的選單樹），避免被管理功能的權限點連帶擋下。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuthenticatedOnlyAttribute : Attribute
{
}
