using System;
using System.Threading;
using System.Windows.Forms;

namespace ProxyEnvManager
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs args)
            {
                MessageBox.Show(
                    "程序发生未处理错误：\n" + args.Exception.Message,
                    "代理环境变量管理器",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
        }
    }
}
