using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace ProxyEnvManager
{
    internal static class AppIcon
    {
        private const string ResourceName = "ProxyEnvManager.app.ico";

        internal static Icon Load(Size preferredSize)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    return new Icon(stream, preferredSize);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
