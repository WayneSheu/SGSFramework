using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Modules
{
    public interface IModuleEntity
    {
        Guid Id { get; set; }
        string ModuleName { get; set; }
        string Version { get; set; }
        string AssemblyPath { get; set; }
        bool IsActive { get; set; }            // 是否啟動此模組
        DateTime LastLoadedAt { get; set; }
        string Checksum { get; set; }          // 用於安全性校驗
    }
}
