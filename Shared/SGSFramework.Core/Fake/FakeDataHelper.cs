using Bogus;

namespace SGSFramework.Core.Fake
{
    public static class FakeDataHelper
    {
        /// <summary>
        /// 泛型模擬資料生成器
        /// </summary>
        /// <typeparam name="T">DTO 或 Entity 類型</typeparam>
        /// <param name="rowCount">生成筆數</param>
        /// <param name="configureRules">自定義 Bogus 規則</param>
        /// <param name="locale">語系，預設繁體中文</param>
        public static IEnumerable<T> Generate<T>(
            int rowCount,
            Action<Faker<T>> configureRules,
            string locale = "zh_TW") where T : class, new()
        {
            // 1. 初始化 Faker
            var faker = new Faker<T>(locale);

            // 2. 執行外部傳入的規則設定
            configureRules(faker);

            // 3. 使用迭代器進行流式生成，避免 60 萬筆數據瞬間撐爆記憶體
            for (int i = 0; i < rowCount; i++)
            {
                yield return faker.Generate();
            }
        }
    }


}
