using System.Text;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

public static class McpDiagnostics
{
    private const string PrefUseHttpTransport = "MCPForUnity.UseHttpTransport";
    private const string PrefAutoStartOnLoad = "MCPForUnity.AutoStartOnLoad";
    private const string PrefSetupCompleted = "MCPForUnity.SetupCompleted";

    [MenuItem("Tools/MCP/Diagnose Connection")]
    public static void DiagnoseConnection()
    {
        var report = BuildReport();
        Debug.Log(report);
    }

    [MenuItem("Tools/MCP/Apply Recommended Settings")]
    public static void ApplyRecommendedSettings()
    {
        EditorPrefs.SetBool(PrefUseHttpTransport, true);
        EditorPrefs.SetBool(PrefAutoStartOnLoad, true);
        EditorPrefs.SetBool(PrefSetupCompleted, true);
        EditorConfigurationCache.Instance.Refresh();

        Debug.Log(
            "[McpDiagnostics] Applied recommended settings: UseHttpTransport=true, AutoStartOnLoad=true, SetupCompleted=true"
        );
    }

    [MenuItem("Tools/MCP/Start HTTP Server")]
    public static void StartHttpServer()
    {
        bool started = MCPServiceLocator.Server.StartLocalHttpServer(quiet: true);
        Debug.Log("[McpDiagnostics] Start HTTP Server: " + (started ? "launch initiated" : "failed (see Console)"));
    }

    [MenuItem("Tools/MCP/Connect Session")]
    public static void ConnectSession()
    {
        EditorApplication.delayCall += RunConnectSessionDeferred;
    }

    private static async void RunConnectSessionDeferred()
    {
        EditorApplication.delayCall -= RunConnectSessionDeferred;

        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[McpDiagnostics] Skip connect while scripts are compiling.");
            return;
        }

        try
        {
            bool started = await MCPServiceLocator.Bridge.StartAsync();
            var verify = await MCPServiceLocator.Bridge.VerifyAsync();
            Debug.Log(
                "[McpDiagnostics] Connect session\n" +
                $"Started={started}\n" +
                $"VerifySuccess={verify.Success}\n" +
                $"Message={verify.Message}"
            );
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[McpDiagnostics] Connect session failed: " + ex.Message);
        }
    }

    [MenuItem("Tools/MCP/Restart Bridge")]
    public static void RestartBridge()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[McpDiagnostics] Skip restart while scripts are compiling.");
            return;
        }

        // Defer until after the current MCP menu command finishes dispatching.
        EditorApplication.delayCall += RunRestartBridgeDeferred;
    }

    private static async void RunRestartBridgeDeferred()
    {
        EditorApplication.delayCall -= RunRestartBridgeDeferred;

        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[McpDiagnostics] Skip restart while scripts are compiling.");
            return;
        }

        try
        {
            await MCPServiceLocator.Bridge.StopAsync();
            await Task.Delay(750);
            bool started = await MCPServiceLocator.Bridge.StartAsync();
            var verify = await MCPServiceLocator.Bridge.VerifyAsync();

            Debug.Log(
                "[McpDiagnostics] Restart bridge\n" +
                $"Started={started}\n" +
                $"VerifySuccess={verify.Success}\n" +
                $"Message={verify.Message}"
            );
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[McpDiagnostics] Restart bridge failed: " + ex.Message);
        }
    }

    private static string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("MCP Connection Report");
        sb.AppendLine("---------------------");
        sb.AppendLine("Cursor config: http://127.0.0.1:8080/mcp");
        sb.AppendLine($"UseHttpTransport: {EditorPrefs.GetBool(PrefUseHttpTransport, true)}");
        sb.AppendLine($"AutoStartOnLoad: {EditorPrefs.GetBool(PrefAutoStartOnLoad, false)}");
        sb.AppendLine($"SetupCompleted: {EditorPrefs.GetBool(PrefSetupCompleted, false)}");
        sb.AppendLine($"Editor isCompiling: {EditorApplication.isCompiling}");
        sb.AppendLine($"Bridge IsRunning: {MCPServiceLocator.Bridge.IsRunning}");
        sb.AppendLine($"Bridge CurrentPort: {MCPServiceLocator.Bridge.CurrentPort}");

        TransportState httpState = MCPServiceLocator.TransportManager.GetState(TransportMode.Http);
        sb.AppendLine($"HTTP Connected: {httpState.IsConnected}");
        if (!string.IsNullOrEmpty(httpState.Error))
        {
            sb.AppendLine($"HTTP Error: {httpState.Error}");
        }

        if (!string.IsNullOrEmpty(httpState.Details))
        {
            sb.AppendLine($"HTTP Details: {httpState.Details}");
        }

        sb.AppendLine();
        sb.AppendLine("Notes:");
        sb.AppendLine("- WebSocket warnings during script compile/domain reload are usually expected.");
        sb.AppendLine("- Do not use modal Editor dialogs in MCP menu handlers; they block ping/keep-alive.");
        sb.AppendLine("- If Cursor cannot connect, run Restart Bridge and reopen the MCP server in Cursor.");
        sb.AppendLine("- execute_code cannot reference project scripts until they are compiled.");

        return sb.ToString();
    }
}
