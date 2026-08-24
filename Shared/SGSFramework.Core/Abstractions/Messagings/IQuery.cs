using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Messagings
{
    /// <summary>
    /// CQRS 查詢指令基礎介面
    /// </summary>
    public interface IQuery<out TResponse> : IRequest<TResponse>
    {
    }
}
