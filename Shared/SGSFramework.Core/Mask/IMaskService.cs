namespace SGSFramework.Core.Mask
{
    public interface IMaskService
    {
        // 遮罩字串方法，接受一個字串輸入，返回遮罩後的字串
        string MaskString(string input, string? propertyName = "");

        // 泛型方法，接受任何類型的物件，並對其進行遮罩處理
        void MaskObject<T>(T obj) where T : class;

    }
}
