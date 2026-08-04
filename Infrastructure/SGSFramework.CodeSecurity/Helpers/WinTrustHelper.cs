using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace SES.CodeSecurity.Helpers
{
    public static partial class WinTrustHelper
    {
        private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("{00AAC56B-CD44-11D0-8CC2-00C04FC295EE}");
        private static IntPtr? _actionIdPtr;

        [LibraryImport("wintrust.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int WinVerifyTrust(IntPtr hwnd, IntPtr pgActionID, IntPtr pWintrustData);

        /// <summary>
        /// 驗證檔案數位簽章是否通過 Authenticode 驗證，並符合指定的發行者名稱。
        /// </summary>
        public static bool VerifySignature(string filePath, string expectedPublisher)
        {
            if (!File.Exists(filePath)) return false;

            // 1. 執行 Windows Authenticode 核心驗證
            if (!ExecuteWinVerifyTrust(filePath)) return false;

            // 2. 額外比對憑證發行者資訊 (檢查 Subject 是否包含預期的發行者名稱)
            try
            {
                using var signer = X509Certificate.CreateFromSignedFile(filePath);
                using var cert = new X509Certificate2(signer);

                return cert.Subject.Contains(expectedPublisher, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool ExecuteWinVerifyTrust(string filePath)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            IntPtr pFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
            Marshal.StructureToPtr(fileInfo, pFileInfo, false);

            var wintrustData = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)),
                dwUnionChoice = 1, // WTD_CHOICE_FILE
                fileInfo = pFileInfo,
                dwUIChoice = 2,    // WTD_UI_NONE
                fdwRevocationChecks = 0,
                dwStateAction = 0,
                dwProvFlags = 0x00000010 | 0x00000200 // WTD_REVOCATION_CHECK_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL
            };

            IntPtr pWintrustData = Marshal.AllocHGlobal(Marshal.SizeOf(wintrustData));
            Marshal.StructureToPtr(wintrustData, pWintrustData, false);

            try
            {
                // 呼叫 Win32 API 驗證
                return WinVerifyTrust(IntPtr.Zero, GetActionIdPtr(), pWintrustData) == 0;
            }
            finally
            {
                Marshal.FreeHGlobal(pFileInfo);
                Marshal.FreeHGlobal(pWintrustData);
            }
        }

        public static IntPtr GetActionIdPtr()
        {
            if (_actionIdPtr == null)
            {
                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(WINTRUST_ACTION_GENERIC_VERIFY_V2));
                Marshal.StructureToPtr(WINTRUST_ACTION_GENERIC_VERIFY_V2, ptr, false);
                _actionIdPtr = ptr;
            }
            return _actionIdPtr.Value;
        }
    }

    // 附屬結構體定義
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr fileInfo;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }
}
