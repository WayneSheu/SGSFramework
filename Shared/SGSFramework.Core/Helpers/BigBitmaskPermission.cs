namespace SGSFramework.Core.Helpers;

using System;

/// <summary>
/// 大容量位元遮罩權限計算器 (支援超過 64 位元之多位元權限點比對與全域授權)
/// </summary>
public class BigBitmaskPermission
{
    private const int DefaultTotalBits = 1024; // 預設支援 1024 個權限點 (128 Bytes)
    private readonly byte[] _buffer;

    /// <summary>
    /// 取得目前配置之總位元數
    /// </summary>
    public int TotalBits { get; }

    /// <summary>
    /// 建構大容量位元遮罩物件
    /// </summary>
    /// <param name="baseHexMask">既有之十六進位遮罩字串 (可為 null)</param>
    /// <param name="totalBits">權限點總位元數 (必須為 8 的倍數)</param>
    public BigBitmaskPermission(string? baseHexMask = null, int totalBits = DefaultTotalBits)
    {
        if (totalBits <= 0 || totalBits % 8 != 0)
        {
            throw new ArgumentException("總位元數必須為大於 0 且可被 8 整除之正整數。", nameof(totalBits));
        }

        TotalBits = totalBits;
        _buffer = new byte[TotalBits / 8];

        if (!string.IsNullOrWhiteSpace(baseHexMask))
        {
            LoadFromHex(baseHexMask);
        }
    }

    /// <summary>
    /// 開啟指定索引之權限位元
    /// </summary>
    /// <param name="bitIndex">權限點位元索引 (0-based)</param>
    public void SetPermission(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex >= TotalBits)
        {
            throw new ArgumentOutOfRangeException(nameof(bitIndex), $"權限位元索引超出範圍 (0 ~ {TotalBits - 1})");
        }

        int byteIndex = bitIndex / 8;
        int bitOffset = bitIndex % 8;
        _buffer[byteIndex] |= (byte)(1 << bitOffset);
    }

    /// <summary>
    /// 關閉指定索引之權限位元
    /// </summary>
    /// <param name="bitIndex">權限點位元索引 (0-based)</param>
    public void ClearPermission(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex >= TotalBits)
        {
            throw new ArgumentOutOfRangeException(nameof(bitIndex), $"權限位元索引超出範圍 (0 ~ {TotalBits - 1})");
        }

        int byteIndex = bitIndex / 8;
        int bitOffset = bitIndex % 8;
        _buffer[byteIndex] &= (byte)~(1 << bitOffset);
    }

    /// <summary>
    /// 最高管理者開啟全區段位元，確保通過全系統所有權限檢查
    /// </summary>
    public void SetAllPermissions()
    {
        try
        {
            Array.Fill(_buffer, (byte)0xFF);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("執行全區段位元授權時發生未預期錯誤。", ex);
        }
    }

    /// <summary>
    /// 檢查是否具備指定索引之權限點
    /// </summary>
    /// <param name="bitIndex">權限點位元索引 (0-based)</param>
    public bool HasPermission(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex >= TotalBits)
        {
            return false;
        }

        int byteIndex = bitIndex / 8;
        int bitOffset = bitIndex % 8;
        return (_buffer[byteIndex] & (1 << bitOffset)) != 0;
    }

    /// <summary>
    /// 輸出 Hex 十六進位加密字串，用於 JWT Claim (perm_bits) 傳輸與持久化
    /// </summary>
    public override string ToString()
    {
        return Convert.ToHexString(_buffer);
    }

    private void LoadFromHex(string hex)
    {
        try
        {
            byte[] bytes = Convert.FromHexString(hex);
            int copyLength = Math.Min(bytes.Length, _buffer.Length);
            Array.Copy(bytes, _buffer, copyLength);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("傳入之十六進位位元遮罩字串格式無效。", nameof(hex), ex);
        }
    }
}