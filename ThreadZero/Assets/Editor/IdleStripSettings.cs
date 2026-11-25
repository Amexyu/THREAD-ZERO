using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreadZero.Editor
{
    /// 11-24 透明挂机条对 Windows 打包的要求：图形接口 D3D11 排第一（D3D12 的交换链透明不了），并关掉 DXGI flip model
    /// （flip model 下窗口拿不到每个像素的透明度）。菜单设一次就存进 ProjectSettings；打 Windows 包时再查一遍，不对就在控制台提醒。
    /// 不设也能打包：游戏里检测到不是 D3D11 会自动用不透明的挂机条（画关卡背景）。
    public static class IdleStripSettings
    {
        [MenuItem("Thread Zero/检查/挂机条：Windows 透明窗口设置")]
        public static void Apply()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12 });
            PlayerSettings.useFlipModelSwapchain = false;
            AssetDatabase.SaveAssets();
            Debug.Log("THREAD ZERO：挂机条透明窗口设置好了。" + Describe());
        }
        public static bool Ok
        {
            get
            {
                if (PlayerSettings.useFlipModelSwapchain) return false;
                if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64)) return false;
                GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
                return apis.Length > 0 && apis[0] == GraphicsDeviceType.Direct3D11;
            }
        }
        public static string Describe()
        {
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            return "Windows 图形接口：" + (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64) ? "自动 " : "") + string.Join(" / ", System.Array.ConvertAll(apis, a => a.ToString()))
                + "；flip model：" + (PlayerSettings.useFlipModelSwapchain ? "开" : "关");
        }

#if UNITY_EDITOR_WIN
        // 11-24 21:11 查「打开时先出右下角小窗」：打包版的本机设置（PlayerPrefs，包括 Unity 自己记的窗口大小 / 全屏方式 / 位置）存在注册表
        // HKCU\Software\公司名\产品名 下。这里只读，写进 Logs/TZBridge/player_prefs.txt。
        [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int RegOpenKeyExW(System.UIntPtr key, string sub, int options, int access, out System.UIntPtr result);
        [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int RegEnumValueW(System.UIntPtr key, int index, System.Text.StringBuilder name, ref int nameLen, System.IntPtr reserved, out int type, byte[] data, ref int dataLen);
        [System.Runtime.InteropServices.DllImport("advapi32.dll")] private static extern int RegCloseKey(System.UIntPtr key);

        [MenuItem("Thread Zero/检查/挂机条：读打包版存在本机的窗口设置")]
        public static void DumpPlayerPrefs()
        {
            string path = "Software\\" + PlayerSettings.companyName + "\\" + PlayerSettings.productName;
            var sb = new System.Text.StringBuilder("HKCU\\" + path + "\n");
            System.UIntPtr key;
            int rc = RegOpenKeyExW(new System.UIntPtr(0x80000001u), path, 0, 0x20019, out key);   // HKEY_CURRENT_USER, KEY_READ
            if (rc != 0) sb.Append("打不开（" + rc + "）：打包版可能还没运行过\n");
            else
            {
                for (int i = 0; i < 4096; i++)
                {
                    var name = new System.Text.StringBuilder(1024); int nameLen = name.Capacity, type, dataLen = 1 << 16; var data = new byte[dataLen];
                    if (RegEnumValueW(key, i, name, ref nameLen, System.IntPtr.Zero, out type, data, ref dataLen) != 0) break;
                    string v = type == 4 ? System.BitConverter.ToInt32(data, 0).ToString() : type == 3 ? System.Text.Encoding.UTF8.GetString(data, 0, System.Math.Max(0, dataLen - 1)) : "（类型 " + type + "）";
                    if (v.Length > 160) v = v.Substring(0, 160) + "…";
                    sb.Append(name).Append(" = ").Append(v).Append('\n');
                }
                RegCloseKey(key);
            }
            string file = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/TZBridge/player_prefs.txt"));
            System.IO.File.WriteAllText(file, sb.ToString());
            Debug.Log("THREAD ZERO：打包版的本机设置写到 " + file);
        }
#endif
    }

    internal sealed class IdleStripBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget t = report.summary.platform;
            if (t != BuildTarget.StandaloneWindows64 && t != BuildTarget.StandaloneWindows) return;
            if (!IdleStripSettings.Ok)
                Debug.LogWarning("THREAD ZERO：这次 Windows 包里的挂机条不会透明（要 D3D11 排第一、关掉 flip model）。先点菜单 Thread Zero/检查/挂机条：Windows 透明窗口设置 再打包。" + IdleStripSettings.Describe());
        }
    }
}
