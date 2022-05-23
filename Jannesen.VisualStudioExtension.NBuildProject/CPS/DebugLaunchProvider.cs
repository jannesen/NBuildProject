using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jannesen.VisualStudioExtension.NBuildProject.CPS
{
    [ExportDebugger("DebuggerProject")] // name ProjectDebugger.xaml
    [AppliesTo(NBuildProjectUnconfiguredProject.UniqueCapability)]
    public class DebugLaunchProvider: DebugLaunchProviderBase
    {
        [ImportingConstructor]
        public                                                              DebugLaunchProvider(ConfiguredProject configuredProject, ProjectProperties projectProperties): base(configuredProject)
        {
        }

        public  override        Task<bool>                                  CanLaunchAsync(DebugLaunchOptions launchOptions)
        {
            // perform any necessary logic to determine if the debugger can launch
            return Task.FromResult(true);
        }
        public  override async  Task<IReadOnlyList<IDebugLaunchSettings>>   QueryDebugTargetsAsync(DebugLaunchOptions launchOptions)
        {
            var properties      = ConfiguredProject.Services.ProjectPropertiesProvider.GetCommonProperties();
            var projectPath     = Path.GetDirectoryName(properties.FileFullPath);
            var launchtype      = await properties.GetEvaluatedPropertyValueAsync("LanchType");
            var LanchExec       = Path.Combine(projectPath, await properties.GetEvaluatedPropertyValueAsync("LanchExec"));
            var LanchCwd        = Path.Combine(projectPath, await properties.GetEvaluatedPropertyValueAsync("LanchCwd"));
            var LanchArgs       = await properties.GetEvaluatedPropertyValueAsync("LanchArgs");
            var LanchEnv        = _normaliseEnv(await properties.GetEvaluatedPropertyValueAsync("LanchEnv"));
            var LanchSourceMaps = _normaliseSourceMap(projectPath, await properties.GetEvaluatedPropertyValueAsync("LanchSourceMaps"));
            var debugPost       = 5858;
            
            var settings = new DebugLaunchSettings(launchOptions)   {
                                LaunchOperation         = DebugLaunchOperation.CreateProcess,
                                Executable              = LanchExec,
                                Arguments               = LanchArgs,
                                CurrentDirectory        = LanchCwd,
                           };

            foreach (var e in LanchEnv) { 
                settings.Environment.Add(e);
            }

            if (launchOptions != DebugLaunchOptions.NoDebug) { 
                var settingsRuntimeArgs = new JArray();
                settingsRuntimeArgs.Add($"--inspect-brk={debugPost}");
                foreach (var a in _splitArgs(LanchArgs)) {
                    settingsRuntimeArgs.Add(a);
                }

                settings.LaunchDebugEngineGuid   = DebuggerEngines.JavaScriptForWebView2Engine;
                settings.Options = JsonConvert.SerializeObject(new JObject(
                        new JProperty("name",                      "Debug Nodejs program from Visual Studio"),
                        new JProperty("type",                      "node2"),
                        new JProperty("request",                   "launch"),
                        new JProperty("console",                   "externalTerminal"),
                        new JProperty("runtimeExecutable",         LanchExec),
                        new JProperty("runtimeArgs",               settingsRuntimeArgs),
                        new JProperty("cwd",                       LanchCwd),
                        new JProperty("env",                       JObject.FromObject(LanchEnv)),
                        new JProperty("port",                      debugPost),
                        new JProperty("resolveSourceMapLocations", JArray.FromObject(LanchSourceMaps))
                    ));
            }

            return new IDebugLaunchSettings[] { settings };
        }

        private static          Dictionary<string, string>                  _normaliseEnv(string lanchEnv)
        {
            var rtn   = new Dictionary<string, string>();

            if (lanchEnv != null) {
                foreach(var line in lanchEnv.Split('\n')) {
                    var tline = line.Trim();
                    if (tline.Length > 0) {
                        var i = tline.IndexOf('=');
                        rtn.Add(tline.Substring(0, i), tline.Substring(i + 1));
                    }
                }
            }

            return rtn;
        }
        private static          List<string>                                _normaliseSourceMap(string projectPath, string lanchEnv)
        {
            var rtn = new List<string>();

            if (lanchEnv != null) {
                foreach(var line in lanchEnv.Split('\n')) {
                    var tline = line.Trim();
                    if (tline.Length > 0) {
                        rtn.Add(Path.Combine(projectPath, tline));
                    }
                }
            }

            return rtn;
        }
        private static          List<string>                                _splitArgs(string args)
        {
            var rtn  = new List<string>();  
            var arg  = new StringBuilder();
            var argf = false;
            int p    = 0;

            while (p < args.Length) {
                switch(args[p]) {
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                    if (argf) {
                        rtn.Add(arg.ToString());
                        arg.Clear();
                        argf = false;
                    }
                    break;

                case '"':
                case '\'':
                    char c = args[p++];
                    argf = true;

                    while (p < args.Length && args[p] != c) {
                        if (args[p] == '\\') {
                            if (++p < args.Length) {
                                arg.Append(args[p]);
                            }
                        }
                        else { 
                            arg.Append(args[p]);
                        }
                        ++p;
                    }
                    break;

                default:
                    arg.Append(args[p]);
                    argf = true;
                    break;
                }
                ++p;
            }

            if (argf) {
                rtn.Add(arg.ToString());
            }

            return rtn;
        }
    }
}
