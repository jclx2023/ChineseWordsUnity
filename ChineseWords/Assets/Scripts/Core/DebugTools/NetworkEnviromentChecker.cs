using UnityEngine;

public class NetworkEnvironmentChecker : MonoBehaviour
{
    /// <summary>
    /// 检测网络环境和潜在问题
    /// </summary>
    public static void CheckNetworkEnvironment()
    {
        Debug.Log("=== 网络环境检测 ===");

        // 1. 检测CFW进程
        CheckClashForWindows();

        // 2. 检测防火墙状态
        CheckFirewallStatus();

        // 3. 检测端口可用性
        CheckPortAvailability(7777);

        // 4. 检测网络接口
        CheckNetworkInterfaces();

        // 5. 测试UDP连通性
        TestUDPConnectivity();
    }

    private static void CheckClashForWindows()
    {
        try
        {
            var clashProcesses = System.Diagnostics.Process.GetProcessesByName("Clash");
            var cfwProcesses = System.Diagnostics.Process.GetProcessesByName("ClashForWindows");

            if (clashProcesses.Length > 0 || cfwProcesses.Length > 0)
            {
                Debug.LogWarning("⚠️ 检测到CFW正在运行，可能影响网络连接");
                Debug.LogWarning("建议：");
                Debug.LogWarning("1. 临时关闭CFW测试连接");
                Debug.LogWarning("2. 或在CFW中排除游戏程序");
                Debug.LogWarning("3. 或使用不同的端口");
            }
            else
            {
                Debug.Log("✅ 未检测到CFW进程");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"CFW检测失败: {e.Message}");
        }
    }

    private static void CheckFirewallStatus()
    {
        Debug.Log("🔥 防火墙状态检测（需要管理员权限）");
        Debug.Log("请手动检查：");
        Debug.Log("1. Windows防火墙是否允许UDP 7777端口");
        Debug.Log("2. 第三方防火墙设置");
        Debug.Log("3. 杀毒软件网络保护");
    }

    private static void CheckPortAvailability(int port)
    {
        try
        {
            // 检测TCP端口（用于参考）
            var tcpListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
            tcpListener.Start();
            tcpListener.Stop();
            Debug.Log($"✅ TCP端口 {port} 可用");

            // 检测UDP端口
            var udpClient = new System.Net.Sockets.UdpClient(port);
            udpClient.Close();
            Debug.Log($"✅ UDP端口 {port} 可用");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 端口 {port} 不可用: {e.Message}");
            Debug.LogError("建议尝试其他端口：7778, 7779, 8888");
        }
    }

    private static void CheckNetworkInterfaces()
    {
        Debug.Log("🌐 网络接口信息：");

        foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            {
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        Debug.Log($"📱 {nic.Name}: {addr.Address}");
                        if (addr.Address.ToString().StartsWith("192.168") ||
                            addr.Address.ToString().StartsWith("10.") ||
                            addr.Address.ToString().StartsWith("172."))
                        {
                            Debug.Log($"   ↳ 内网地址，其他人应连接此IP");
                        }
                    }
                }
            }
        }
    }

    private static void TestUDPConnectivity()
    {
        Debug.Log("🔌 UDP连通性测试建议：");
        Debug.Log("1. 让同学ping你的IP地址");
        Debug.Log("2. 使用telnet测试：虽然telnet是TCP，但可以测试基本连通性");
        Debug.Log("3. 临时关闭所有防火墙测试");
        Debug.Log("4. 尝试不同的端口号");
    }
}