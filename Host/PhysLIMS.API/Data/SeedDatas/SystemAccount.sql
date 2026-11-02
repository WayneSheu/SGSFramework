USE [PhysLIMSDB]
GO
INSERT [core].[SystemAccounts] ([Id], [ClientId], [SecretHash], [SecretHint], [Name], [Description], [Status], [AccessTokenLifetimeMinutes], [AllowedIpRanges], [SecretExpiresAt], [SecretRotatedAt], [TokensNotBefore], [LastUsedAt], [LastUsedIp], [CreatedAt], [CreatedByName], [UpdatedAt], [UpdatedByName]) VALUES (N'sys_scheduler', N'physlims-scheduler', N'AQAAAAIAAYagAAAAEJ/ecGFtiBJU1reu+H4aHRUQmocscmAY9NFo6V7VNyyT92juvd0ASn4mGEBhPCOZtA==', N'…hOT0', N'排程服務 (Hangfire)', N'SGSFramework.Scheduler 週期作業：資料庫備份、定期權限覆核', 1, 15, NULL, CAST(N'2027-10-28T02:04:06.6864418+00:00' AS DateTimeOffset), CAST(N'2026-10-28T02:04:06.6864418+00:00' AS DateTimeOffset), CAST(N'2026-10-28T05:26:13.1750392+00:00' AS DateTimeOffset), CAST(N'2026-11-02T01:44:18.5589340+00:00' AS DateTimeOffset), N'::1', CAST(N'2026-10-28T02:04:06.6864418+00:00' AS DateTimeOffset), N'sysadmin', CAST(N'2026-10-28T05:26:13.1750392+00:00' AS DateTimeOffset), N'sysadmin')
GO
INSERT [core].[SystemAccountScopes] ([SystemAccountId], [Scope], [GrantedAt]) VALUES (N'sys_scheduler', N'accessreview.campaign.schedule', CAST(N'2026-10-28T02:04:06.6864418+00:00' AS DateTimeOffset))
GO
INSERT [core].[SystemAccountScopes] ([SystemAccountId], [Scope], [GrantedAt]) VALUES (N'sys_scheduler', N'database.backup', CAST(N'2026-10-28T02:04:06.6864418+00:00' AS DateTimeOffset))
GO
INSERT [core].[SystemAccountScopes] ([SystemAccountId], [Scope], [GrantedAt]) VALUES (N'sys_scheduler', N'logs.cleanup', CAST(N'2026-10-28T05:26:13.1750392+00:00' AS DateTimeOffset))
GO
