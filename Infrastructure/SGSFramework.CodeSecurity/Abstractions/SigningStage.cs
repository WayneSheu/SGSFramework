using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Abstractions
{
    /// <summary>
    /// 簽署階段定義
    /// </summary>
    public enum SigningStage 
    {
        /// <summary>
        /// 簽署前階段
        /// </summary>
        PreBuild,

        /// <summary>
        /// 簽署中階段
        /// </summary>
        PostDeployment
    }
}
