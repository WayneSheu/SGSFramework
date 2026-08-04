namespace SGSFramework.Core.Abstractions.Attributes
{
    /// <summary>
    /// 用於標記 Controller 或 Method，明確其業務模組名稱。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ModuleAttribute : Attribute
    {
        public string ModuleName { get; }

        public ModuleAttribute(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module Name cannot be empty.", nameof(moduleName));
            ModuleName = moduleName;
        }
    }


    /// <summary>
    /// 用於標記 Method，明確其業務功能名稱。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class FunctionAttribute : Attribute
    {
        public string FunctionName { get; }

        public FunctionAttribute(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("Function Name cannot be empty.", nameof(functionName));
            FunctionName = functionName;
        }
    }

}
