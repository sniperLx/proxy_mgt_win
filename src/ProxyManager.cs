using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ProxyEnvManager
{
    internal sealed class ProxyDiscoveryResult
    {
        public bool Available { get; set; }
        public string HttpProxy { get; set; }
        public string HttpsProxy { get; set; }
        public string NoProxy { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }

        public static ProxyDiscoveryResult Found(
            string httpProxy,
            string httpsProxy,
            string noProxy,
            string status,
            string detail)
        {
            return new ProxyDiscoveryResult
            {
                Available = true,
                HttpProxy = httpProxy,
                HttpsProxy = httpsProxy,
                NoProxy = noProxy,
                Status = status,
                Detail = detail
            };
        }

        public static ProxyDiscoveryResult Unavailable(string status, string detail)
        {
            return new ProxyDiscoveryResult
            {
                Available = false,
                HttpProxy = String.Empty,
                HttpsProxy = String.Empty,
                NoProxy = String.Empty,
                Status = status,
                Detail = detail
            };
        }
    }

    internal sealed class EnvironmentVariableState
    {
        public bool HttpFound { get; set; }
        public bool HttpsFound { get; set; }
        public bool NoProxyFound { get; set; }
        public string HttpValue { get; set; }
        public string HttpsValue { get; set; }
        public string NoProxyValue { get; set; }
    }

    internal sealed class EnvironmentSaveResult
    {
        public bool BroadcastSucceeded { get; set; }
    }

    internal static class ProxyUrlNormalizer
    {
        public static bool TryNormalize(string rawValue, out string normalizedValue, out string error)
        {
            normalizedValue = String.Empty;
            error = String.Empty;

            string value = (rawValue ?? String.Empty).Trim();
            if (value.Length == 0)
            {
                error = "代理地址不能为空。";
                return false;
            }

            if (value.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                value = "http://" + value;
            }

            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                error = "代理地址格式无效，示例：http://127.0.0.1:7897";
                return false;
            }

            if (!String.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                && !String.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                error = "仅支持 HTTP 或 HTTPS 代理地址。";
                return false;
            }

            if (String.IsNullOrWhiteSpace(uri.Host))
            {
                error = "代理地址缺少主机名或 IP。";
                return false;
            }

            if (!String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment))
            {
                error = "代理地址不能包含查询参数或片段。";
                return false;
            }

            if (!String.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
            {
                error = "代理地址不能包含路径。";
                return false;
            }

            try
            {
                UriBuilder builder = new UriBuilder(uri);
                builder.Path = String.Empty;
                builder.Query = String.Empty;
                builder.Fragment = String.Empty;
                normalizedValue = builder.Uri.AbsoluteUri.TrimEnd('/');
                return true;
            }
            catch (UriFormatException)
            {
                error = "代理地址格式无效，示例：http://127.0.0.1:7897";
                return false;
            }
        }
    }

    internal static class NoProxyNormalizer
    {
        public const string DefaultValue = "localhost,127.0.0.1,::1";

        public static string Normalize(string rawValue)
        {
            return Normalize(rawValue, false);
        }

        internal static string Normalize(string rawValue, bool includeDefaultsWhenBlank)
        {
            string value = (rawValue ?? String.Empty).Trim();
            if (value.Length == 0)
            {
                return includeDefaultsWhenBlank ? DefaultValue : String.Empty;
            }

            string[] parts = value.Split(
                new[] { ',', ';', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            List<string> normalizedParts = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool includesLocal = parts.Any(delegate(string part)
            {
                return String.Equals(part.Trim(), "<local>", StringComparison.OrdinalIgnoreCase);
            });

            if (includesLocal)
            {
                AddPart(normalizedParts, seen, "localhost");
                AddPart(normalizedParts, seen, "127.0.0.1");
                AddPart(normalizedParts, seen, "::1");
            }

            foreach (string rawPart in parts)
            {
                string part = rawPart.Trim();
                if (part.Length == 0
                    || String.Equals(part, "<local>", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddPart(normalizedParts, seen, part);
            }

            if (normalizedParts.Count == 0 && includeDefaultsWhenBlank)
            {
                return DefaultValue;
            }

            return String.Join(",", normalizedParts.ToArray());
        }

        private static void AddPart(ICollection<string> parts, ISet<string> seen, string value)
        {
            if (seen.Add(value))
            {
                parts.Add(value);
            }
        }
    }

    internal static class SystemProxyReader
    {
        private const string InternetSettingsKey =
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        public static ProxyDiscoveryResult Discover()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey, false))
                {
                    if (key == null)
                    {
                        return ProxyDiscoveryResult.Unavailable(
                            "无法读取 Windows 系统代理",
                            "当前用户不存在 Internet Settings 注册表项。");
                    }

                    int proxyEnabled = ReadDword(key, "ProxyEnable");
                    if (proxyEnabled != 1)
                    {
                        return ProxyDiscoveryResult.Unavailable(
                            "Windows 系统代理未启用",
                            "请先在 Windows 设置或 Clash Verge 中开启系统代理。本工具只读取，不会修改系统代理开关。");
                    }

                    string proxyServer = Convert.ToString(key.GetValue("ProxyServer", String.Empty));
                    string autoConfigUrl = Convert.ToString(key.GetValue("AutoConfigURL", String.Empty));
                    if (String.IsNullOrWhiteSpace(proxyServer))
                    {
                        if (!String.IsNullOrWhiteSpace(autoConfigUrl))
                        {
                            return ProxyDiscoveryResult.Unavailable(
                                "系统使用 PAC/自动配置",
                                "PAC 脚本不提供固定的 HTTP 代理地址，请改用 Clash Verge 或手动输入。");
                        }

                        return ProxyDiscoveryResult.Unavailable(
                            "未找到系统代理地址",
                            "ProxyEnable 已开启，但 ProxyServer 为空。");
                    }

                    string httpProxy;
                    string httpsProxy;
                    string error;
                    if (!TryParseProxyServer(proxyServer, out httpProxy, out httpsProxy, out error))
                    {
                        return ProxyDiscoveryResult.Unavailable("系统代理地址不可用", error);
                    }

                    return ProxyDiscoveryResult.Found(
                        httpProxy,
                        httpsProxy,
                        ParseProxyOverride(Convert.ToString(key.GetValue("ProxyOverride", String.Empty))),
                        "已读取 Windows 系统代理",
                        "来源：HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings");
                }
            }
            catch (Exception ex)
            {
                return ProxyDiscoveryResult.Unavailable("读取系统代理失败", ex.Message);
            }
        }

        internal static bool TryParseProxyServer(
            string proxyServer,
            out string httpProxy,
            out string httpsProxy,
            out string error)
        {
            httpProxy = String.Empty;
            httpsProxy = String.Empty;
            error = String.Empty;

            string server = (proxyServer ?? String.Empty).Trim();
            if (server.Length == 0)
            {
                error = "系统代理地址为空。";
                return false;
            }

            string baseServer = String.Empty;
            Dictionary<string, string> protocols =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] entries = server.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawEntry in entries)
            {
                string entry = rawEntry.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                int separator = entry.IndexOf('=');
                if (separator < 0)
                {
                    baseServer = entry;
                    continue;
                }

                string protocol = entry.Substring(0, separator).Trim();
                string address = entry.Substring(separator + 1).Trim();
                if (protocol.Length > 0 && address.Length > 0)
                {
                    protocols[protocol] = address;
                }
            }

            string httpSource;
            string httpsSource;
            if (!TryGetProtocolValue(protocols, "http", baseServer, out httpSource))
            {
                TryGetProtocolValue(protocols, "https", baseServer, out httpSource);
            }

            if (!TryGetProtocolValue(protocols, "https", baseServer, out httpsSource))
            {
                httpsSource = httpSource;
            }

            if (String.IsNullOrWhiteSpace(httpSource) || String.IsNullOrWhiteSpace(httpsSource))
            {
                error = "系统代理中没有 HTTP/HTTPS 协议地址，可能只配置了 SOCKS。";
                return false;
            }

            if (!ProxyUrlNormalizer.TryNormalize(httpSource, out httpProxy, out error))
            {
                error = "HTTP 系统代理无效：" + error;
                return false;
            }

            if (!ProxyUrlNormalizer.TryNormalize(httpsSource, out httpsProxy, out error))
            {
                error = "HTTPS 系统代理无效：" + error;
                return false;
            }

            return true;
        }

        internal static string ParseProxyOverride(string proxyOverride)
        {
            return NoProxyNormalizer.Normalize(proxyOverride, true);
        }

        private static bool TryGetProtocolValue(
            IDictionary<string, string> protocols,
            string protocol,
            string fallback,
            out string value)
        {
            if (protocols.TryGetValue(protocol, out value) && !String.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!String.IsNullOrWhiteSpace(fallback))
            {
                value = fallback;
                return true;
            }

            value = String.Empty;
            return false;
        }

        private static int ReadDword(RegistryKey key, string name)
        {
            object value = key.GetValue(name, 0);
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    internal static class ClashProxyReader
    {
        private static readonly string[] InstallDirectoryNames =
        {
            "io.github.clash-verge-rev.clash-verge-rev",
            "io.github.clash-verge-rev",
            "clash-verge-rev",
            "clash-verge"
        };

        private static readonly string[] ProcessNames =
        {
            "clash-verge",
            "clash-verge-service",
            "verge-mihomo",
            "clash",
            "mihomo"
        };

        private static readonly Regex TopLevelYamlValueRegex = new Regex(
            @"^(?<key>[A-Za-z0-9_-]+)\s*:\s*(?<value>[^#\r\n]*)",
            RegexOptions.Compiled);

        private static readonly Regex NetstatListeningRegex = new Regex(
            @"^\s*TCP\s+\S+:(\d+)\s+\S+\s+LISTENING\s+(\d+)\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public static ProxyDiscoveryResult Discover()
        {
            try
            {
                HashSet<int> processIds = GetClashProcessIds();
                bool isRunning = processIds.Count > 0;
                List<string> roots = GetCandidateConfigDirectories();

                HashSet<int> listeningPorts = isRunning
                    ? GetListeningPortsForProcesses(processIds)
                    : new HashSet<int>();

                foreach (string root in roots)
                {
                    ProxyDiscoveryResult configResult = ReadConfigDirectory(root);
                    if (!configResult.Available)
                    {
                        continue;
                    }

                    ExtractPort(configResult.HttpProxy, listeningPorts, ref configResult);
                    configResult.Status = isRunning
                        ? "Clash Verge 运行中，已自动读取代理"
                        : "已从 Clash Verge 配置读取代理";
                    return configResult;
                }

                if (isRunning)
                {
                    return ProxyDiscoveryResult.Unavailable(
                        "Clash Verge 正在运行，但未找到端口配置",
                        "已检测到 Clash Verge/Mihomo 进程，但没有在常见配置目录中找到 mixed-port 或 port。请检查 Clash Verge 设置，或手动输入代理地址。");
                }

                if (roots.Count > 0)
                {
                    return ProxyDiscoveryResult.Unavailable(
                        "已找到 Clash Verge 配置，但没有可用端口",
                        "配置目录：" + String.Join("；", roots.ToArray()));
                }

                return ProxyDiscoveryResult.Unavailable(
                    "未检测到 Clash Verge",
                    "已检查当前用户常见配置目录。请确认已安装并至少启动一次 Clash Verge，或改用手动输入。");
            }
            catch (Exception ex)
            {
                return ProxyDiscoveryResult.Unavailable("读取 Clash Verge 配置失败", ex.Message);
            }
        }

        internal static ProxyDiscoveryResult ReadConfigDirectory(string root)
        {
            if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return ProxyDiscoveryResult.Unavailable("配置目录不存在", root ?? String.Empty);
            }

            Dictionary<string, Dictionary<string, int>> portsByFile =
                new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

            string[] files =
            {
                "clash-verge.yaml",
                "config.yaml",
                "verge.yaml"
            };

            foreach (string file in files)
            {
                string fullPath = Path.Combine(root, file);
                if (File.Exists(fullPath))
                {
                    portsByFile[file] = ReadTopLevelPorts(fullPath);
                }
            }

            string[,] candidates =
            {
                { "clash-verge.yaml", "mixed-port" },
                { "config.yaml", "mixed-port" },
                { "verge.yaml", "verge_mixed_port" },
                { "clash-verge.yaml", "port" },
                { "config.yaml", "port" },
                { "verge.yaml", "verge_http_port" },
                { "verge.yaml", "verge_port" }
            };

            for (int index = 0; index < candidates.GetLength(0); index++)
            {
                string file = candidates[index, 0];
                string key = candidates[index, 1];
                Dictionary<string, int> ports;
                int port;
                if (portsByFile.TryGetValue(file, out ports)
                    && ports.TryGetValue(key, out port)
                    && port > 0
                    && port <= 65535)
                {
                    string url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
                    return ProxyDiscoveryResult.Found(
                        url,
                        url,
                        NoProxyNormalizer.DefaultValue,
                        "已读取 Clash Verge 配置",
                        "来源：" + Path.Combine(root, file) + Environment.NewLine + "字段：" + key);
                }
            }

            return ProxyDiscoveryResult.Unavailable(
                "Clash Verge 配置中没有可用端口",
                "配置目录：" + root + Environment.NewLine +
                "已检查 mixed-port、port、verge_mixed_port、verge_http_port 和 verge_port。");
        }

        private static void ExtractPort(
            string url,
            ISet<int> listeningPorts,
            ref ProxyDiscoveryResult result)
        {
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri)
                && listeningPorts.Contains(uri.Port))
            {
                result.Detail += Environment.NewLine + "端口监听：已确认";
            }
            else if (listeningPorts.Count > 0)
            {
                result.Detail += Environment.NewLine + "端口监听：未在 Clash 进程监听的端口中确认";
            }
        }

        private static Dictionary<string, int> ReadTopLevelPorts(string path)
        {
            Dictionary<string, int> ports =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in File.ReadAllLines(path))
            {
                Match match = TopLevelYamlValueRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string key = match.Groups["key"].Value;
                if (!String.Equals(key, "mixed-port", StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(key, "port", StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(key, "verge_mixed_port", StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(key, "verge_http_port", StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(key, "verge_port", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string rawValue = match.Groups["value"].Value.Trim().Trim('"', '\'');
                int port;
                if (Int32.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
                {
                    ports[key] = port;
                }
            }

            return ports;
        }

        private static HashSet<int> GetClashProcessIds()
        {
            HashSet<int> processIds = new HashSet<int>();
            foreach (string processName in ProcessNames)
            {
                try
                {
                    foreach (Process process in Process.GetProcessesByName(processName))
                    {
                        using (process)
                        {
                            processIds.Add(process.Id);
                        }
                    }
                }
                catch (Exception)
                {
                    // Process discovery is best effort; config-based discovery still works.
                }
            }

            return processIds;
        }

        private static List<string> GetCandidateConfigDirectories()
        {
            List<string> candidates = new List<string>();
            string[] baseDirectories =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            foreach (string baseDirectory in baseDirectories)
            {
                if (String.IsNullOrWhiteSpace(baseDirectory))
                {
                    continue;
                }

                foreach (string directoryName in InstallDirectoryNames)
                {
                    candidates.Add(Path.Combine(baseDirectory, directoryName));
                }
            }

            return candidates
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static HashSet<int> GetListeningPortsForProcesses(ISet<int> processIds)
        {
            HashSet<int> ports = new HashSet<int>();
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "netstat.exe"),
                    Arguments = "-ano -p tcp",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return ports;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(2000);

                    foreach (Match match in NetstatListeningRegex.Matches(output))
                    {
                        int processId;
                        int port;
                        if (Int32.TryParse(match.Groups[2].Value, out processId)
                            && Int32.TryParse(match.Groups[1].Value, out port)
                            && processIds.Contains(processId))
                        {
                            ports.Add(port);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Port confirmation is optional and must not block config discovery.
            }

            return ports;
        }
    }

    internal static class UserEnvironmentStore
    {
        private const string EnvironmentKeyPath = @"Environment";
        private const string HttpProxyName = "http_proxy";
        private const string HttpsProxyName = "https_proxy";
        private const string NoProxyName = "no_proxy";
        private const int HwndBroadcast = 0xffff;
        private const uint WmSettingChange = 0x001A;
        private const uint SmtoAbortIfHung = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint msg,
            UIntPtr wParam,
            string lParam,
            uint flags,
            uint timeout,
            out UIntPtr result);

        public static EnvironmentVariableState Read()
        {
            EnvironmentVariableState state = new EnvironmentVariableState();
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(EnvironmentKeyPath, false))
            {
                if (key == null)
                {
                    return state;
                }

                string httpName = FindValueName(key, HttpProxyName);
                if (httpName != null)
                {
                    state.HttpFound = true;
                    state.HttpValue = Convert.ToString(key.GetValue(httpName, String.Empty));
                }

                string httpsName = FindValueName(key, HttpsProxyName);
                if (httpsName != null)
                {
                    state.HttpsFound = true;
                    state.HttpsValue = Convert.ToString(key.GetValue(httpsName, String.Empty));
                }

                string noProxyName = FindValueName(key, NoProxyName);
                if (noProxyName != null)
                {
                    state.NoProxyFound = true;
                    state.NoProxyValue = Convert.ToString(key.GetValue(noProxyName, String.Empty));
                }
            }

            return state;
        }

        public static EnvironmentSaveResult Save(string httpProxy, string httpsProxy, string noProxy)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(EnvironmentKeyPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开当前用户环境变量注册表项。");
                }

                SetEnvironmentValue(key, HttpProxyName, httpProxy);
                SetEnvironmentValue(key, HttpsProxyName, httpsProxy);
                SetEnvironmentValue(key, NoProxyName, noProxy);
            }

            return new EnvironmentSaveResult { BroadcastSucceeded = BroadcastEnvironmentChange() };
        }

        public static EnvironmentSaveResult Clear()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(EnvironmentKeyPath, true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开当前用户环境变量注册表项。");
                }

                DeleteValueIgnoreCase(key, HttpProxyName);
                DeleteValueIgnoreCase(key, HttpsProxyName);
                DeleteValueIgnoreCase(key, NoProxyName);
            }

            return new EnvironmentSaveResult { BroadcastSucceeded = BroadcastEnvironmentChange() };
        }

        private static void SetEnvironmentValue(RegistryKey key, string name, string value)
        {
            string existingName = FindValueName(key, name);
            key.SetValue(existingName ?? name, value, RegistryValueKind.String);
        }

        private static void DeleteValueIgnoreCase(RegistryKey key, string name)
        {
            string existingName = FindValueName(key, name);
            if (existingName != null)
            {
                key.DeleteValue(existingName, false);
            }
        }

        private static string FindValueName(RegistryKey key, string desiredName)
        {
            foreach (string valueName in key.GetValueNames())
            {
                if (String.Equals(valueName, desiredName, StringComparison.OrdinalIgnoreCase))
                {
                    return valueName;
                }
            }

            return null;
        }

        private static bool BroadcastEnvironmentChange()
        {
            try
            {
                UIntPtr result;
                IntPtr response = SendMessageTimeout(
                    new IntPtr(HwndBroadcast),
                    WmSettingChange,
                    UIntPtr.Zero,
                    "Environment",
                    SmtoAbortIfHung,
                    5000,
                    out result);
                return response != IntPtr.Zero;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
