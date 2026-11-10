# =====================================================================================
# PhysLIMS 方案 A：單一 Windows 容器（API 同時託管 Blazor WASM 前端，路徑 /blazor）
#
# 為何使用 Windows 容器：
#   外掛模組 (plugins/*.dll) 由 AssemblySecurityVerifier 以 Authenticode
#   (X509Certificate.CreateFromSignedFile) 驗證簽章與憑證指紋，僅 Windows 支援。
#
# 本檔「不在映像內建置」：簽署憑證位於開發機 Cert:\CurrentUser\My，無法進入映像。
# 請先執行 publish.ps1 於 Windows 主機發佈並簽署，產出 artifacts\publish，再 docker compose build。
# 基底映像須為 windowsservercore（nanoserver 無 wintrust，簽章驗證會失敗）。
# =====================================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022

WORKDIR C:/app
COPY artifacts/publish/ ./

# 金鑰環 (DataProtection) 與日誌必須放在掛載的 volume，不可留在容器暫存層
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DataProtection__KeysPath=C:/data/keys \
    DataProtection__Mode=Certificate \
    DataProtection__Certificate__PfxPath=C:/secrets/dataprotection.pfx

EXPOSE 8080
ENTRYPOINT ["dotnet", "PhysLIMS.API.dll"]
