#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using Serilog.Events;

namespace SGSFramework.SystemLog.Readers;

public sealed class LogEventDataReader : IDataReader
{
    private readonly IEnumerator<LogEvent> _enumerator;
    private bool _isClosed;

    public LogEventDataReader(IEnumerable<LogEvent> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _enumerator = items.GetEnumerator();
    }

    public bool Read() => _enumerator.MoveNext();

    public object GetValue(int i)
    {
        var current = _enumerator.Current;
        if (current == null)
        {
            throw new InvalidOperationException("當前 LogEvent 紀錄不可為 Null。");
        }

        string? GetProp(string name) =>
            current.Properties.TryGetValue(name, out var val) ? val.ToString().Trim('"') : null;

        return i switch
        {
            0 => current.Timestamp.DateTime,                       // TimeStamp
            1 => current.Level.ToString(),                         // Level
            2 => current.RenderMessage(),                          // Message
            3 => current.Properties.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(current.Properties)
                : DBNull.Value,                                     // Payload
            4 => (object?)current.Exception?.ToString() ?? DBNull.Value, // Exception
            5 => (object?)GetProp("AlertId") ?? DBNull.Value,       // AlertId
            6 => (object?)GetProp("Fingerprint") ?? DBNull.Value,   // Fingerprint
            7 => (object?)GetProp("CorrelationId") ?? DBNull.Value, // CorrelationId
            8 => (object?)GetProp("TenantId") ?? DBNull.Value,      // TenantId
            9 => (object?)GetProp("UserId") ?? DBNull.Value,        // UserId
            10 => (object?)GetProp("ModuleName") ?? DBNull.Value,   // ModuleName
            11 => (object?)GetProp("Operation") ?? DBNull.Value,    // Operation
            12 => (object?)GetProp("IP") ?? DBNull.Value,           // IP
            13 => (object?)GetProp("Url") ?? DBNull.Value,          // Url
            14 => (object?)GetProp("PrevHash") ?? DBNull.Value,     // PrevHash
            15 => (object?)GetProp("CurrentHash") ?? DBNull.Value,  // CurrentHash
            16 => current.Timestamp.UtcDateTime,                   // CreatedAt (關鍵修正！)
            _ => throw new IndexOutOfRangeException($"Column index {i} out of range.")
        };
    }

    public int GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        int count = Math.Min(values.Length, FieldCount);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public int FieldCount => 17;

    public string GetName(int i) => i switch
    {
        0 => "TimeStamp",
        1 => "Level",
        2 => "Message",
        3 => "Payload",
        4 => "Exception",
        5 => "AlertId",
        6 => "Fingerprint",
        7 => "CorrelationId",
        8 => "TenantId",
        9 => "UserId",
        10 => "ModuleName",
        11 => "Operation",
        12 => "IP",
        13 => "Url",
        14 => "PrevHash",
        15 => "CurrentHash",
        16 => "CreatedAt",
        _ => throw new IndexOutOfRangeException()
    };

    public int GetOrdinal(string name) => name switch
    {
        "TimeStamp" => 0,
        "Level" => 1,
        "Message" => 2,
        "Payload" => 3,
        "Exception" => 4,
        "AlertId" => 5,
        "Fingerprint" => 6,
        "CorrelationId" => 7,
        "TenantId" => 8,
        "UserId" => 9,
        "ModuleName" => 10,
        "Operation" => 11,
        "IP" => 12,
        "Url" => 13,
        "PrevHash" => 14,
        "CurrentHash" => 15,
        "CreatedAt" => 16,
        _ => -1
    };

    public void Close() => _isClosed = true;
    public bool IsClosed => _isClosed;

    public void Dispose()
    {
        _enumerator.Dispose();
        _isClosed = true;
    }

    public bool NextResult() => false;
    public int Depth => 0;
    public DataTable GetSchemaTable() => throw new NotSupportedException();
    public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i));
    public byte GetByte(int i) => Convert.ToByte(GetValue(i));
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public char GetChar(int i) => Convert.ToChar(GetValue(i));
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public IDataReader GetData(int i) => throw new NotSupportedException();
    public string GetDataTypeName(int i) => GetFieldType(i).Name;
    public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i));
    public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));
    public double GetDouble(int i) => Convert.ToDouble(GetValue(i));
    public Type GetFieldType(int i) => i switch
    {
        0 or 16 => typeof(DateTime),
        _ => typeof(string)
    };
    public float GetFloat(int i) => Convert.ToSingle(GetValue(i));
    public Guid GetGuid(int i) => (Guid)GetValue(i);
    public short GetInt16(int i) => Convert.ToInt16(GetValue(i));
    public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
    public long GetInt64(int i) => Convert.ToInt64(GetValue(i));
    public string GetString(int i) => Convert.ToString(GetValue(i)) ?? string.Empty;
    public bool IsDBNull(int i) => GetValue(i) == DBNull.Value;
    public object this[int i] => GetValue(i);
    public object this[string name] => GetValue(GetOrdinal(name));
    public int RecordsAffected => -1;
}