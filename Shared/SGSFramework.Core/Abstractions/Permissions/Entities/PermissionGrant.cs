using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Controller;
using System;
using System.Collections.Generic;
using System.Text;
using SGSFramework.Core.Abstractions.Entities.Base;

namespace SGSFramework.Core.Abstractions.Permissions.Identities
{
    /// <summary>
    /// 角色權限授權實體，表示特定角色對特定功能或資源的存取權限。
    /// 多維度權限維度矩陣-實驗室隔離維度
    /// 角色於特定實驗室維度下的 BitMask 授權實體
    /// </summary>
    public class PermissionGrant 
    {
        public Guid Id { get; set; }

        public Guid RoleId { get; set; }

        /// <summary>
        /// 跨實驗室隔離維度 ID
        /// </summary>
        public Guid LabId { get; set; }

        /// <summary>
        /// 動態 BitMask 權限位元組陣列 (預設 64 bytes = 512 個權限點)
        /// </summary>
        public byte[] PermissionVector { get; set; } = new byte[64];

        /// <summary>
        /// 檢查特定 BitPosition 是否有權限
        /// </summary>
        public bool HasPermission(int bitPosition)
        {
            int byteIndex = bitPosition / 8;
            int bitOffset = bitPosition % 8;
            if (byteIndex >= PermissionVector.Length) return false;
            return (PermissionVector[byteIndex] & (1 << bitOffset)) != 0;
        }

        /// <summary>
        /// 設定特定 BitPosition 的權限狀態
        /// </summary>
        public void SetPermission(int bitPosition, bool isGranted)
        {
            int byteIndex = bitPosition / 8;
            int bitOffset = bitPosition % 8;
            if (byteIndex >= PermissionVector.Length)
            {
                Array.Resize(ref _permissionVector, byteIndex + 1);
            }

            if (isGranted)
                PermissionVector[byteIndex] |= (byte)(1 << bitOffset);
            else
                PermissionVector[byteIndex] &= (byte)~(1 << bitOffset);
        }

        private byte[] _permissionVector = new byte[64];
    }


    public class PermissionGrantConfiguration : IEntityTypeConfiguration<PermissionGrant>
    {
        public void Configure(EntityTypeBuilder<PermissionGrant> builder)
        {
            builder.ToTable("PermissionGrants");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.RoleId)
                   .IsRequired();

            builder.Property(x => x.LabId)
                   .IsRequired();

            // 儲存為 MSSQL varbinary 格式，支援變長位元組向量
            builder.Property(x => x.PermissionVector)
                   .HasColumnType("varbinary(64)")
                   .IsRequired();

            // 複合唯一索引：確保同一角色在同一實驗室只有一筆 BitMask 向量紀錄
            builder.HasIndex(x => new { x.RoleId, x.LabId })
                   .IsUnique()
                   .HasDatabaseName("IX_PermissionGrant_Role_Lab");
        }
    }
}
