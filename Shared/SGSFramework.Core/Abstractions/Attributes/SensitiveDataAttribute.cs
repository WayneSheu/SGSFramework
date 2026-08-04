using SGSFramework.Core.Mask;

namespace SGSFramework.Core.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SensitiveDataAttribute : Attribute
    {
        public MaskFormat Format { get; set; } = MaskFormat.Default;
        public string CustomMask { get; set; } = "*Mask*";
        public SensitiveDataAttribute(MaskFormat format = MaskFormat.Default) => Format = format;
    }

}
