using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary> 
    /// 使用者資訊回應 DTO 
    /// </summary> 
    public sealed record UserResponse
    {
        public Guid Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
        public Guid? PrimaryLabId { get; init; }
    }
}
