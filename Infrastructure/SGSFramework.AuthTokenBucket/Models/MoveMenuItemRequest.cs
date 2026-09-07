using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    public record MoveMenuItemRequest(
        Guid? TargetParentId,
        int NewOrder
    );
}
