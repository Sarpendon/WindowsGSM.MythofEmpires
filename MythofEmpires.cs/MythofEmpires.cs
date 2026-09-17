using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class MythofEmpires : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.MythofEmpires", // WindowsGSM.XXXX
            author = "Sarpendon",
            description = "WindowsGSM plugin for supporting Myth of Empires Dedicated Server",
            version = "2.0",
            url = "https://github.com/Sarpendon/WindowsGSM.MythofEmpires", // Github repository link (Best practice)
            color = "#8802db" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "1794810"; // Game server appId, Myth of Empires is 1794810

        // - Standard Constructor and properties
        public MythofEmpires(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;

        // Error and Notice are deliberately not redeclared here. SteamCMDAgent already provides
        // both, and a field of the same name hides the base property: WindowsGSM reads
        // gameServer.Error dynamically and binds to the most derived member, so everything the
        // base wrote - the reason Install() failed, for one - never reached the UI.

        // - Game server Fixed variables
        public override string StartPath => @"MOE\Binaries\Win64\MOEServer.exe"; // Game server start path
        public string FullName = "Myth of Empires Dedicated Server"; // Game server FullName

        // Capability flag only. WindowsGSM overwrites it with the server's own Embed Console
        // setting (MainWindow.Server_BeginStart) right before calling Start().
        public bool AllowsEmbedConsole = true;

        // Was 2, which collided: with the game port at 7777 and the shutdown service at 7779,
        // a second server installed straight after the first was handed 7779 as its game port.
        public int PortIncrements = 3;
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string Port = "7777"; // Default port
        public string QueryPort = "7779"; // Shutdown service ("RCON") port - see README
        public string Defaultmap = "LargeTerrain_Central2_Main"; // Used for Server ID
        public string Maxplayers = "100"; // Default maxplayers

        // -ShutDownServicePort used to appear here as well as being built from the Server Query
        // Port below. The one built from the server settings is placed on the command line first
        // and wins, so the copy here only ever confused things. Removed.
        public string Additional = "-game -server -DataLocalFile -NotCheckServerSteamAuth -log log=123456.log -LOCALLOGTIMES -PrivateServer -disable_qim -UseACE -EnableVACBan=1 -ServerId=100 -ClusterId=1 -bStartShutDownServiceInPrivateServer=true -ShutDownServiceIP=127.0.0.1 -ShutDownServiceKey=302172 -ServerAdminAccounts=insert_steam_id_here -Description=please_use_quotation_marks_around_text -SaveGameIntervalMinute=10"; // Additional server start parameter

        // - Create a default cfg for the game server after installation
        //   Myth of Empires is configured entirely on the command line, so there is nothing to
        //   write - but WindowsGSM calls this after installing, so it has to exist.
        public void CreateServerCFG() { }

        // How long to wait for the public IP lookup before giving up on it and starting anyway.
        private const int PUBLIC_IP_TIMEOUT_MS = 5000;

        // WebClient has no Timeout property of its own; without this the lookup can sit on the
        // default 100 seconds before it gives up.
        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeoutMs;

            public TimeoutWebClient(int timeoutMs) { _timeoutMs = timeoutMs; }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request != null) { request.Timeout = _timeoutMs; }
                return request;
            }
        }

        // Myth of Empires needs its public address advertised through -OutAddress. Looking it up
        // used to happen inline on the calling thread with no error handling at all, which meant
        // the WindowsGSM window froze for the length of the request every single start, and any
        // failure - no connectivity, DNS, the lookup service being down - threw straight out of
        // Start(). Nothing above catches that (Server_BeginStart does not wrap the call), so it
        // took WindowsGSM down with it.
        //
        // Now it runs off the calling thread with a short timeout, and a failure just means the
        // server starts without -OutAddress instead of not starting at all.
        private static async Task<string> GetPublicIpAddress()
        {
            return await Task.Run(() =>
            {
                // ipv4-only endpoint, since WindowsGSM cannot handle IPv6 anyway.
                using (var webClient = new TimeoutWebClient(PUBLIC_IP_TIMEOUT_MS))
                {
                    string response = webClient.DownloadString("https://ipv4.icanhazip.com/");

                    // The previous code called Replace("\\r\\n", "") here, which looks for a
                    // literal backslash-r-backslash-n and so never matched anything. Trim is what
                    // actually strips the trailing newline.
                    return IPAddress.Parse(response.Trim()).ToString();
                }
            });
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            // Only worth looking up when it will actually be used - -OutAddress is bound to the
            // same condition as -MultiHome below.
            string publicIp = null;
            if (!string.IsNullOrWhiteSpace(_serverData.ServerIP))
            {
                try
                {
                    publicIp = await GetPublicIpAddress();
                }
                catch (Exception e)
                {
                    Notice = $"Could not determine the public IP address, starting without -OutAddress. ({e.Message})";
                }
            }

            // Prepare start parameter
            string param = string.Empty; // Set basic parameters
            param += string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $" {_serverData.ServerMap}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerIP) ? string.Empty : $" -MultiHome={_serverData.ServerIP}";
            param += string.IsNullOrWhiteSpace(publicIp) ? string.Empty : $" -OutAddress={publicIp}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerName) ? string.Empty : $" -SessionName=\"{_serverData.ServerName}\"";
            param += string.IsNullOrWhiteSpace(_serverData.ServerGSLT) ? string.Empty : $" -PrivateServerPassword={_serverData.ServerGSLT}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" -MaxPlayers={_serverData.ServerMaxPlayer}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -Port={_serverData.ServerPort}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? string.Empty : $" -ShutDownServicePort={_serverData.ServerQueryPort}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}";

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            bool embedConsole = AllowsEmbedConsole;
            if (embedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                // Without this, non-ASCII characters in server and player names arrive mangled
                // in the WindowsGSM console pane.
                p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                p.StartInfo.StandardErrorEncoding = Encoding.UTF8;

                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
            }
            catch (FileNotFoundException e)
            {
                Error = $"File not found: {e.Message}";
                return null;
            }
            catch (UnauthorizedAccessException e)
            {
                Error = $"Access denied: {e.Message}";
                return null;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }

            if (embedConsole)
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }

        // - Graceful shutdown support
        //
        // The previous stop path did two things through SendKeys: it sent the text "SaveWorld",
        // then Ctrl+C. Neither reached the server. WindowsGSM hides the server window right after
        // starting it (MainWindow.Server_BeginStart) and the embedded console gives the process no
        // window at all, so MainWindowHandle is IntPtr.Zero and SetForegroundWindow does nothing.
        // SendKeys.SendWait is global, so both went to whatever window happened to have focus on
        // the host machine - which means "SaveWorld" was being typed as literal text into whatever
        // the machine's user had open at that moment.
        //
        // MOEServer.exe is a console application, so it always has a console - even under
        // CREATE_NO_WINDOW. Attaching to that console and raising CTRL_C_EVENT there reaches the
        // server and asks Unreal to shut down cleanly instead of being killed.

        private delegate bool ConsoleCtrlDelegate(uint ctrlType);

        private const uint CTRL_C_EVENT = 0;

        // Server_BeginStop awaits Stop() without a timeout of its own, so a long shutdown is
        // honoured. Only wait it out when a signal was actually delivered.
        private const int GRACEFUL_EXIT_TIMEOUT_MS = 120000;
        private const int FORCED_EXIT_TIMEOUT_MS = 5000;

        // WindowsGSM clears the console pane as soon as Stop() returns, taking the shutdown
        // output with it. Set to 0 to hand back immediately.
        private const int CONSOLE_LINGER_MS = 3000;

        // Console attachment is per process, not per thread, and WindowsGSM can run two stops at
        // once (auto restart, restart crontab and update-on-start each drive their own timer).
        // Without this lock one stop can detach the console another is about to signal.
        private static readonly object _consoleSignalLock = new object();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

        // - Stop server function
        public async Task Stop(Process p)
        {
            if (p == null) { return; }

            await Task.Run(() =>
            {
                // Note: WindowsGSM builds the instance behind Stop() without a ServerConfig, so
                // _serverData is null in here. Everything this method needs comes from the
                // process itself.
                try
                {
                    if (p.HasExited) { return; }
                }
                catch (Exception e)
                {
                    Error = e.Message;
                    return;
                }

                lock (_consoleSignalLock)
                {
                    bool attached = false;
                    bool signalled = false;

                    try
                    {
                        // AttachConsole fails while this process still owns a console of its own.
                        FreeConsole();

                        attached = AttachConsole((uint)p.Id);
                        if (attached)
                        {
                            // The event reaches every process on that console, which now includes
                            // WindowsGSM. Ignore it here first, or the manager goes down with the
                            // server it is trying to stop.
                            SetConsoleCtrlHandler(null, true);
                            signalled = GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
                        }

                        if (!signalled && p.MainWindowHandle != IntPtr.Zero)
                        {
                            // Fallback for a server that really does have a visible window.
                            // Guarded on the handle because SendKeys is global: with no window to
                            // bring forward, the keystroke lands in whatever the user is working in.
                            ServerConsole.SetMainWindow(p.MainWindowHandle);
                            ServerConsole.SendWaitToMainWindow("^c");
                            signalled = true;
                        }

                        p.WaitForExit(signalled ? GRACEFUL_EXIT_TIMEOUT_MS : FORCED_EXIT_TIMEOUT_MS);

                        if (signalled && CONSOLE_LINGER_MS > 0 && p.HasExited)
                        {
                            Thread.Sleep(CONSOLE_LINGER_MS);
                        }
                    }
                    catch (Exception e)
                    {
                        Error = e.Message;
                    }
                    finally
                    {
                        if (attached)
                        {
                            try { SetConsoleCtrlHandler(null, false); } catch { /* ignore */ }
                            try { FreeConsole(); } catch { /* ignore */ }
                        }
                    }
                }
            });
        }

        // - Update server function
        public new async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;

            // UpdateEx hands back null when steamcmd could not be downloaded or the Steam account
            // is not set up. The old code dereferenced it unconditionally, so a failed update
            // threw a NullReferenceException out of WindowsGSM's update path - twice, since the
            // retry hits the same line - instead of reporting the error it was given.
            if (p == null) { return null; }

            // Auto Update restarts the server as soon as this returns, so the update has to be
            // finished by then, not merely started.
            await Task.Run(() => p.WaitForExit());
            return p;
        }

        public new bool IsInstallValid()
        {
            string installPath = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (File.Exists(installPath)) { return true; }

            // Keep a message the installer already left behind - that one says why it failed.
            if (string.IsNullOrWhiteSpace(Error))
            {
                Error = $"Fail to find {installPath}";
            }

            return false;
        }

        public new bool IsImportValid(string path)
        {
            // This used to look for PackageInfo.bin, carried over from the ARK plugin. Myth of
            // Empires does not ship that file, so importing an existing server always failed.
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }

        public new string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public new async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}
