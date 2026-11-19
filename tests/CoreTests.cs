using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ProxyEnvManager.Tests
{
    internal static class Program
    {
        private static readonly List<string> Failures = new List<string>();

        [STAThread]
        private static int Main()
        {
            TestUrlNormalization();
            TestSystemProxyParsing();
            TestNoProxyNormalization();
            TestClashDiscovery();
            TestMinimumWindowLayout();

            if (Failures.Count == 0)
            {
                Console.WriteLine("All tests passed.");
                return 0;
            }

            foreach (string failure in Failures)
            {
                Console.WriteLine("FAIL: " + failure);
            }

            Console.WriteLine(Failures.Count + " test(s) failed.");
            return 1;
        }

        private static void TestUrlNormalization()
        {
            string value;
            string error;

            Assert(
                ProxyUrlNormalizer.TryNormalize("127.0.0.1:7897", out value, out error)
                && value == "http://127.0.0.1:7897",
                "Bare host should receive http scheme.");

            Assert(
                ProxyUrlNormalizer.TryNormalize("http://127.0.0.1:7897/", out value, out error)
                && value == "http://127.0.0.1:7897",
                "Trailing slash should be removed.");

            Assert(
                ProxyUrlNormalizer.TryNormalize("socks5://127.0.0.1:7897", out value, out error)
                == false,
                "Unsupported scheme should be rejected.");

            Assert(
                ProxyUrlNormalizer.TryNormalize("http://127.0.0.1:7897/path", out value, out error)
                == false,
                "Proxy URL with path should be rejected.");
        }

        private static void TestSystemProxyParsing()
        {
            string http;
            string https;
            string error;

            Assert(
                SystemProxyReader.TryParseProxyServer("127.0.0.1:7897", out http, out https, out error)
                && http == "http://127.0.0.1:7897"
                && https == "http://127.0.0.1:7897",
                "Single Windows proxy server should populate both variables.");

            Assert(
                SystemProxyReader.TryParseProxyServer(
                    "http=127.0.0.1:7891;https=127.0.0.1:7892", out http, out https, out error)
                && http == "http://127.0.0.1:7891"
                && https == "http://127.0.0.1:7892",
                "Protocol-specific Windows proxy values should be preserved.");

            Assert(
                SystemProxyReader.TryParseProxyServer(
                    "socks=127.0.0.1:7897", out http, out https, out error)
                == false,
                "SOCKS-only system proxy should not be converted into an HTTP proxy.");

            string noProxy = SystemProxyReader.ParseProxyOverride(
                "<local>;*.corp.local;10.*;localhost");
            Assert(
                noProxy == "localhost,127.0.0.1,::1,*.corp.local,10.*",
                "Windows proxy override should be converted to no_proxy syntax.");
        }

        private static void TestNoProxyNormalization()
        {
            Assert(
                NoProxyNormalizer.Normalize("localhost; 127.0.0.1,localhost") ==
                "localhost,127.0.0.1",
                "no_proxy separators should be normalized and duplicates removed.");

            Assert(
                NoProxyNormalizer.Normalize("  ") == String.Empty,
                "Blank manual no_proxy should remain empty.");

            Assert(
                SystemProxyReader.ParseProxyOverride(String.Empty) ==
                NoProxyNormalizer.DefaultValue,
                "Missing Windows override list should use local defaults.");
        }

        private static void TestClashDiscovery()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "ProxyEnvManagerTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "clash-verge.yaml"),
                    "mixed-port: 7897\nproxies:\n  - name: nested\n    port: 9999\n",
                    new UTF8Encoding(false));
                File.WriteAllText(
                    Path.Combine(directory, "config.yaml"),
                    "mixed-port: 0\nport: 7899\n",
                    new UTF8Encoding(false));
                File.WriteAllText(
                    Path.Combine(directory, "verge.yaml"),
                    "verge_mixed_port: 7999\nverge_port: 7998\n",
                    new UTF8Encoding(false));

                ProxyDiscoveryResult result = ClashProxyReader.ReadConfigDirectory(directory);
                Assert(
                    result.Available && result.HttpProxy == "http://127.0.0.1:7897",
                    "Clash runtime mixed-port should take precedence.");

                File.WriteAllText(
                    Path.Combine(directory, "clash-verge.yaml"),
                    "mixed-port: 0\n",
                    new UTF8Encoding(false));
                result = ClashProxyReader.ReadConfigDirectory(directory);
                Assert(
                    result.Available && result.HttpProxy == "http://127.0.0.1:7999",
                    "verge_mixed_port should be used before falling back to HTTP port.");

                File.Delete(Path.Combine(directory, "verge.yaml"));
                result = ClashProxyReader.ReadConfigDirectory(directory);
                Assert(
                    result.Available && result.HttpProxy == "http://127.0.0.1:7899",
                    "HTTP port should be used when all mixed ports are disabled.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void TestMinimumWindowLayout()
        {
            using (MainForm form = new MainForm())
            {
                form.CreateControl();
                form.Size = form.MinimumSize;
                form.PerformLayout();

                TableLayoutPanel root = form.Controls[0] as TableLayoutPanel;
                Assert(root != null, "Root layout should be a TableLayoutPanel.");
                if (root == null)
                {
                    return;
                }

                GroupBox statusGroup = root.GetControlFromPosition(0, 3) as GroupBox;
                Assert(statusGroup != null, "Status group should be present.");
                if (statusGroup == null)
                {
                    return;
                }

                TableLayoutPanel statusLayout = statusGroup.Controls[0] as TableLayoutPanel;
                Assert(statusLayout != null, "Status layout should be present.");
                if (statusLayout == null)
                {
                    return;
                }

                Control status = statusLayout.GetControlFromPosition(0, 0);
                Control detail = statusLayout.GetControlFromPosition(0, 1);
                Control environment = statusLayout.GetControlFromPosition(0, 2);
                Assert(status != null && detail != null && environment != null,
                    "Status rows should all be present.");
                if (status == null || detail == null || environment == null)
                {
                    return;
                }

                Assert(detail.Top >= status.Bottom,
                    "Discovery detail must not overlap the status heading at minimum size.");
                Assert(environment.Top >= detail.Bottom,
                    "Environment values must not overlap discovery details at minimum size.");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Failures.Add(message);
            }
        }
    }
}
