using SGSFramework.Core.Abstractions.Attributes;

// 標記 LOG 日誌模組 Assembly 層級（ModuleCode 同時決定 DbContext 預設 schema = log）
[assembly: Module("LOG", "SGS.Modules.LOG", "日誌管理", Icon = "fa-solid fa-clipboard-list", Order = 32)]
