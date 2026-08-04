using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Interfaces
{
    /// <summary>
    /// 實驗室/租戶狀態提供者介面
    /// </summary>
    public interface ILaboratoryStateProvider
    {
        /// <summary>
        /// 獲取當前用戶活動中的實驗室識別碼 (LaboratoryId/TenantId)
        /// </summary>
        /// <returns>當前實驗室 ID，若尚未選擇則回傳 string.Empty</returns>
        string GetCurrentLaboratoryId();

        /// <summary>
        /// 變更當前活絡的實驗室識別碼 (當使用者在 UI 切換實驗室時呼叫)
        /// </summary>
        /// <param name="laboratoryId">目標實驗室識別碼</param>
        void SetCurrentLaboratoryId(string laboratoryId);

        /// <summary>
        /// 當實驗室狀態異動時觸發的事件，供 UI 元件訂閱以實時重繪資料
        /// </summary>
        event Action<string>? OnLaboratoryChanged;
    }
}
