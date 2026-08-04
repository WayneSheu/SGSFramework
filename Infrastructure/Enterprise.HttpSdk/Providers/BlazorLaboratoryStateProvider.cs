using Enterprise.HttpSdk.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Providers
{
    /// <summary>
    /// 適用於 Blazor WebAssembly 記憶體狀態與動態事件通知的實驗室狀態提供者實作
    /// </summary>
    public sealed class BlazorLaboratoryStateProvider : ILaboratoryStateProvider
    {
        private readonly ILogger<BlazorLaboratoryStateProvider> _logger;
        private string _currentLaboratoryId = string.Empty;

        public BlazorLaboratoryStateProvider(ILogger<BlazorLaboratoryStateProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event Action<string>? OnLaboratoryChanged;

        /// <summary>
        /// 獲取當前執行緒記憶體中鎖定的租戶實驗室識別碼
        /// </summary>
        public string GetCurrentLaboratoryId()
        {
            return _currentLaboratoryId;
        }

        /// <summary>
        /// 變更實驗室識別碼，並透過多播委派原子廣播給所有訂閱的 UI 元件
        /// </summary>
        public void SetCurrentLaboratoryId(string laboratoryId)
        {
            ArgumentNullException.ThrowIfNull(laboratoryId);

            string trimmedId = laboratoryId.Trim();
            if (_currentLaboratoryId == trimmedId) return;

            _currentLaboratoryId = trimmedId;
            _logger.LogInformation("系統租戶上下文已切換至實驗室: {LaboratoryId}", _currentLaboratoryId);

            try
            {
                // 安全觸發事件通知前端元件刷新 UI 狀態
                OnLaboratoryChanged?.Invoke(_currentLaboratoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "廣播實驗室變更事件時發生未預期異常");
            }
        }
    }
}
