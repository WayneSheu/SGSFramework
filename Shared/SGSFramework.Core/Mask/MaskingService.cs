using SGSFramework.Core.Abstractions.Attributes;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SGSFramework.Core.Mask
{

    /// <summary>
    /// 負責所有的資料識別與格式化邏輯。
    /// </summary>
    public class MaskingService : IMaskService
    {
        // --- Regex 規則庫 (預編譯以提升效能) ---
        // 信用卡格式: 13-16 位數字，允許空格或連字號分隔
        private static readonly Regex CreditCardRegex = new(@"\b(?:\d[ -]*?){13,16}\b", RegexOptions.Compiled);
        // 台灣身分證格式: 1 個英文字母 + 1 個數字 (1 或 2) + 8 個數字
        private static readonly Regex TaiwanIdRegex = new(@"\b[A-Z][12]\d{8}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // 台灣統一編號格式: 8 位數字
        private static readonly Regex TaxIdRegex = new(@"\b\d{8}\b", RegexOptions.Compiled);
        // 銀行帳號格式: 10-14 位數字，允許空格或連字號分隔
        private static readonly Regex BankAccountRegex = new(@"\b\d{3}[- ]?\d{3,4}[- ]?\d{4,7}\b", RegexOptions.Compiled);
        // 台灣手機格式: 09 開頭 + 8 位數字，允許空格或連字號分隔
        private static readonly Regex MobileRegex = new(@"\b09\d{2}[- ]?\d{3}[- ]?\d{3}\b", RegexOptions.Compiled);
        // 載具格式：/******* (確保前後為空白或行首行尾)
        private static readonly Regex CarrierRegex = new(@"^/[0-9A-Z.+-]{7}$|(?<=\s)/[0-9A-Z.+-]{7}(?=\s|$)", RegexOptions.Compiled);
        // 電子郵件格式: 簡化版，適用於一般情況
        private static readonly Regex EmailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
        // 台灣地址格式: 可選的 "台灣" 或 "臺灣" + 2 個字的縣市 + 2 個字的市區鄉鎮 (簡化版)
        private static readonly Regex AddressRegex = new(@"(?:台灣|臺灣)?(..[縣市])(..[市區鄉鎮]).*", RegexOptions.Compiled);

        /// <summary>
        /// 全域脫敏入口：根據字串內容自動識別並遮蔽
        /// </summary>
        /// <param name="input">內容</param>
        /// <param name="propertyName">屬性名稱 (若提供可觸發姓名遮罩)</param>
        public string MaskString(string input, string propertyName = "")
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            string result = input;

            // 1. 手機號碼 (優先處理，支援 0937-236-048, 0937 236 048, 0937236048)
            // 使用 Group 抓取，確保不論有無分隔符都能精準定位中間三碼
            result = Regex.Replace(result, @"(?<!\d)(09\d{2})[- ]?(\d{3})[- ]?(\d{3})(?!\d)", m =>
            {
                // 統一遮蔽格式為 09xx***xxx
                return MaskPhone(input);
            });

            // 2. 信用卡 (Luhn 驗證)
            result = CreditCardRegex.Replace(result, m =>
            {
                string clean = Regex.Replace(m.Value, @"\D", "");
                return IsValidLuhn(clean) ? MaskCreditCard(clean) : m.Value;
            });

            // 3. 台灣身分證
            result = TaiwanIdRegex.Replace(result, m =>
                IsValidTaiwanId(m.Value.ToUpper()) ? MaskIdCard(m.Value) : m.Value
            );

            // 4. 銀行帳號 (頭 3 末 4)(排除掉已經被處理過的手機格式)
            result = BankAccountRegex.Replace(result, m =>
            {
                string clean = Regex.Replace(m.Value, @"\D", "");
                // 排除 09 開頭且長度為 10 的純數字 (避免誤殺手機)
                if (clean.StartsWith("09") && clean.Length == 10) return m.Value;

                return (clean.Length >= 10 && clean.Length <= 14)
                    ? $"{clean[..3]}*******{clean[^4..]}" : m.Value;
            });
            // 5. 統一編號 (加權驗證)
            result = TaxIdRegex.Replace(result, m =>
                IsValidTaiwanTaxId(m.Value) ? $"*****{m.Value[^3..]}" : m.Value
            );
            // 6.電子發票載具 (格式: /*******)
            result = CarrierRegex.Replace(result, "/*******");
            // 7. Email 遮罩 (保留首位與 Domain)
            result = EmailRegex.Replace(result, m => MaskEmail(m.Value));
            //9. 地址遮罩 (保留行政區)
            result = AddressRegex.Replace(result, m => MaskAddress(m.Value));

            return result;

        }

        #region 遮罩驗證

        // 判斷是否為潛在姓名 (2-4字中文，且排除常見系統詞)
        //中文姓名驗證
        public bool IsPotentialName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var systemKeywords = new[] { "成功", "失敗", "錯誤", "登入", "更新" };
            return Regex.IsMatch(input, @"^[\u4e00-\u9fa5]{2,4}$") && !systemKeywords.Contains(input);
        }
        //LUHH 驗證
        private bool IsValidLuhn(string number)
        {
            int sum = 0; bool alt = false;
            for (int i = number.Length - 1; i >= 0; i--)
            {
                int n = number[i] - '0';
                if (alt) { n *= 2; if (n > 9) n -= 9; }
                sum += n; alt = !alt;
            }
            return (sum % 10 == 0);
        }
        //身份證字號驗證
        private bool IsValidTaiwanId(string id)
        {
            string alphabet = "ABCDEFGHJKLMNPQRSTUVXYWZIO";
            int n = alphabet.IndexOf(id[0]) + 10;
            int sum = (n / 10) + (n % 10) * 9;
            int[] weights = { 8, 7, 6, 5, 4, 3, 2, 1 };
            for (int i = 0; i < 8; i++) sum += (id[i + 1] - '0') * weights[i];
            sum += (id[9] - '0');
            return sum % 10 == 0;
        }

        //統一編號驗證
        public bool IsValidTaiwanTaxId(string taxId)
        {
            int[] weights = { 1, 2, 1, 2, 1, 2, 4, 1 };
            int sum = 0;
            for (int i = 0; i < 8; i++)
            {
                int p = (taxId[i] - '0') * weights[i];
                sum += (p / 10) + (p % 10);
            }
            return (sum % 10 == 0) || (taxId[6] == '7' && (sum + 1) % 10 == 0);
        }

        #endregion

        /// <summary>
        /// 動態屬性名稱清單 (針對 Key 進行遮罩)
        /// </summary>
        public HashSet<string> MaskedPropertyNames { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "Name","Password", "Token", "Secret", "ApiKey", "密碼", "授權碼"
        };

        // 針對物件的反射遮罩 (支援 Attribute 與 名稱比對)
        public void MaskObject<T>(T obj) where T : class
        {
            if (obj == null) return;
            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                if (prop.PropertyType != typeof(string) || !prop.CanWrite) continue;

                var attr = prop.GetCustomAttribute<SensitiveDataAttribute>();
                bool nameMatch = MaskedPropertyNames.Contains(prop.Name);

                if (attr != null || nameMatch)
                {
                    var originalValue = (string)prop.GetValue(obj);
                    if (string.IsNullOrEmpty(originalValue)) continue;

                    // 根據標註型別執行特定演算法
                    string maskedVal = attr.Format switch
                    {
                        MaskFormat.IdCard => MaskIdCard(originalValue),
                        MaskFormat.Name => MaskName(originalValue),
                        MaskFormat.Phone => MaskPhone(originalValue),
                        MaskFormat.Email => MaskEmail(originalValue),
                        MaskFormat.Address => MaskAddress(originalValue),
                        MaskFormat.CreditCard => MaskCreditCard(originalValue),
                        MaskFormat.BankAccount => MaskBankAccount(originalValue),
                        MaskFormat.TaxId => MaskTaxId(originalValue),
                        MaskFormat.InvoiceCarrier => MaskInvoiceCarrier(originalValue),
                        _ => "***MASKED***"
                    };

                    // 若無特定格式，則根據名稱啟用通用遮罩 (保留首尾各 1 字)
                    prop.SetValue(obj, maskedVal);

                }
            }
        }

        #region 具體遮罩演算法
        //身分證遮罩 (保留前 3 後 3)
        public string MaskIdCard(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 10) return id;
            return $"{id[..3]}***{id[^3..]}";
        }
        //姓名遮罩 (保留首位與末位，其他以 ○ 替代)
        public string MaskName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            int len = name.Length;
            if (len == 2) return $"{name[0]}○";
            if (len == 3) return $"{name[0]}○{name[2]}";
            if (len >= 4) return $"{name[0]}{new string('○', len - 2)}{name[^1]}";
            return name;
        }
        //手機遮罩 (保留前 4 後 3)
        public string MaskPhone(string phone)
        {
            // 支援 XXXX-XXX-XXX 或 XXXXXXXXXX
            string clean = Regex.Replace(phone, @"\D", "");
            if (clean.Length != 10) return phone;
            return $"{clean[..4]}***{clean[^3..]}";
        }
        //信箱遮罩 (保留首位與 Domain)
        public string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            return parts[0].Length <= 1 ? $"*@{parts[1]}" : $"{parts[0][0]}***@{parts[1]}";
        }
        //地址遮罩 (保留行政區)
        public string MaskAddress(string address)
        {
            string[] keys = { "路", "街", "大道", "邨", "里" };
            int split = -1;
            foreach (var k in keys)
            {
                int i = address.IndexOf(k);
                if (i != -1) { split = i + 1; break; }
            }
            if (split == -1) split = Math.Min(address.Length, 6);
            return $"{address[..split]}*****";
        }
        //信用卡遮罩(保留後 4)
        public string MaskCreditCard(string card)
        {
            return $"****-****-****-{card[^4..]}";
        }

        //銀行帳號遮罩 (保留前 3 後 4)
        public string MaskBankAccount(string account)
        {
            // 支援 10-14 位數字，允許空格或連字號分隔
            return $"{account[..3]}*******{account[^4..]}";
        }

        //統一編號遮罩保留末三碼 (例如：53080009 -> *****009)
        public string MaskTaxId(string taxId)
        {
            return IsValidTaiwanTaxId(taxId) ? $"*****{taxId[^3..]}" : taxId;

        }

        //發票載具遮罩 (格式: /*******)
        public string MaskInvoiceCarrier(string carrier)
        {
            return "/*******";
        }

        #endregion

    }
}
