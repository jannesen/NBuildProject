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
        public                                                              DebugLaunchProvider(ConfiguredProject configuredProject): base(configuredProject)
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
            var launchType      = await properties.GetEvaluatedPropertyValueAsync("LanchType") ?? throw new Exception("Missing LanchType.");
            var lanchCwd        = await properties.GetEvaluatedPropertyValueAsync("LanchCwd");
            
            var settings            = new DebugLaunchSettings(launchOptions)   {
                                          LaunchOperation         = DebugLaunchOperation.CreateProcess,
                                          CurrentDirectory        = lanchCwd != null ? Path.Combine(projectPath, lanchCwd) : projectPath,
                                      };
            JObject jsonOptions = null;

            if (launchOptions != DebugLaunchOptions.NoDebug) { 
                settings.LaunchDebugEngineGuid   = DebuggerEngines.JavaScriptForWebView2Engine;
                jsonOptions = new JObject();

                jsonOptions.Add(new JProperty("name",              "Debug nbuildproj from Visual Studio"));
                jsonOptions.Add(new JProperty("request",           "launch"));
                jsonOptions.Add(new JProperty("cwd",               settings.CurrentDirectory));
            }

            switch(launchType) {
            case "node": { 
                    var lanchExec     = await properties.GetEvaluatedPropertyValueAsync("LanchExec");
                    var lanchArgs     = await properties.GetEvaluatedPropertyValueAsync("LanchArgs");
                    var lanchUserArgs = await properties.GetEvaluatedPropertyValueAsync("LanchUserArgs");
                    var lanchEnv      = _normaliseEnv(await properties.GetEvaluatedPropertyValueAsync("LanchEnv"));

                    if (string.IsNullOrEmpty(lanchExec)) throw new Exception("Missing LanchExec.");
                    if (string.IsNullOrEmpty(lanchArgs)) throw new Exception("Missing LanchArgs.");

                    settings.Executable = Path.Combine(projectPath, lanchExec);
                    foreach (var e in lanchEnv) { 
                        settings.Environment.Add(e);
                    }
                    settings.Arguments = string.IsNullOrEmpty(lanchUserArgs) ? lanchArgs : lanchArgs + " " + lanchUserArgs;

                    if (jsonOptions != null) {
                        jsonOptions.Add(new JProperty("type",    "node2"));
                        jsonOptions.Add(new JProperty("console", "externalTerminal"));
                        jsonOptions.Add(new JProperty("runtimeExecutable", settings.Executable));

                        if (settings.Environment.Count > 0) {
                            jsonOptions.Add(new JProperty("env", JObject.FromObject(settings.Environment)));
                        }

                        var settingsRuntimeArgs = new JArray();
                        settingsRuntimeArgs.Add($"--inspect-brk={5858}");
                        jsonOptions.Add(new JProperty("port", 5858));
                        _argsToJsonArgs(settingsRuntimeArgs, settings.Arguments);
                        jsonOptions.Add("runtimeArgs", settingsRuntimeArgs);
                    }
                }
                break;

            case "chrome":
                goto chrome_edge;

            case "edge":
                goto chrome_edge;
chrome_edge:    {
                    if (jsonOptions == null) {
                        throw new Exception("Run '" + launchType + "' not supported.");
                    }

                    var lanchWebRoot     = await properties.GetEvaluatedPropertyValueAsync("LanchWebRoot");
                    var lanchUserUrl     = await properties.GetEvaluatedPropertyValueAsync("LanchUserUrl");
                    var lanchArgs        = await properties.GetEvaluatedPropertyValueAsync("LanchArgs");
                    if (string.IsNullOrEmpty(lanchUserUrl)) throw new Exception("Missing LanchUserUrl.");
                    if (string.IsNullOrEmpty(lanchWebRoot)) lanchWebRoot = await properties.GetEvaluatedPropertyValueAsync("TargetDirectory");

                    settings.Executable = "C:\\Windows\\System32\\cmd.exe";

                    if (jsonOptions != null) {
                        var lanchUserDataDir = await properties.GetEvaluatedPropertyValueAsync("LanchBrowserDataDir");
                        jsonOptions.Add(new JProperty("type",                launchType));
                        jsonOptions.Add(new JProperty("console",             "internalConsole"));
                        jsonOptions.Add(new JProperty("disableNetworkCache", true));
                        jsonOptions.Add(new JProperty("runtimeExecutable",   "*"));

                        switch(lanchUserDataDir) {
                        case "":
                        case null:
                            jsonOptions.Add(new JProperty("userDataDir", true));
                            break;
                        case "default":
                            jsonOptions.Add(new JProperty("userDataDir", false));
                            break;
                        default:
                            jsonOptions.Add(new JProperty("userDataDir", lanchUserDataDir));
                            break;
                        }

                        jsonOptions.Add(new JProperty("webRoot", lanchWebRoot != null ? Path.Combine(projectPath, lanchWebRoot) : projectPath));

                        if (!string.IsNullOrEmpty(lanchArgs)) {
                            var settingsRuntimeArgs = new JArray();
                            _argsToJsonArgs(settingsRuntimeArgs, lanchArgs);
                            jsonOptions.Add("runtimeArgs", settingsRuntimeArgs);
                        }
                        //pathMapping
                        jsonOptions.Add(new JProperty("url", lanchUserUrl));
                    }
                }
                break;

            default:
                throw new Exception("Unknown launchtype");
            }

            if (jsonOptions != null) { 
                var lanchOutFiles = _normaliseGlobMap(projectPath, await properties.GetEvaluatedPropertyValueAsync("LanchOutFiles"));

                if (lanchOutFiles.Count > 0) {
                    jsonOptions.Add(new JProperty("outFiles", lanchOutFiles));
                }

                var lanchSourceMaps = _normaliseGlobMap(projectPath, await properties.GetEvaluatedPropertyValueAsync("LanchSourceMaps"));

                if (lanchSourceMaps.Count > 0) {
                    jsonOptions.Add(new JProperty("resolveSourceMapLocations", lanchSourceMaps));
                }

                jsonOptions.Add("pauseForSourceMap", true);

                settings.Options = JsonConvert.SerializeObject(jsonOptions);
#if DEBUG
                System.Diagnostics.Debug.WriteLine(settings.Options);
#endif
            }
        
            return new IDebugLaunchSettings[] { settings };
        }

        private static          Dictionary<string, string>                  _normaliseEnv(string lanchEnv)
        {
            var rtn   = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(lanchEnv)) {
                foreach(var line in lanchEnv.Split(';')) {
                    var tline = line.Trim();
                    if (tline.Length > 0) {
                        var i = tline.IndexOf('=');
                        rtn.Add(tline.Substring(0, i), tline.Substring(i + 1));
                    }
                }
            }

            return rtn;
        }
        private static          JArray                                      _normaliseGlobMap(string projectPath, string lanchEnv)
        {
            var rtn = new JArray();

            if (lanchEnv != null) {
                foreach(var line in lanchEnv.Split(';')) {
                    var tline = line.Trim();
                    if (tline.Length > 0) {
                        rtn.Add(Path.Combine(projectPath, tline));
                    }
                }
            }

            return rtn;
        }
        private static          void                                        _argsToJsonArgs(JArray jsonArgs, string stringArgs)
        {
            if (!string.IsNullOrEmpty(stringArgs)) {
                var arg  = new StringBuilder();
                var argf = false;
                int p    = 0;

                while (p < stringArgs.Length) {
                    switch(stringArgs[p]) {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        if (argf) {
                            jsonArgs.Add(arg.ToString());
                            arg.Clear();
                            argf = false;
                        }
                        break;

                    case '"':
                    case '\'':
                        char c = stringArgs[p++];
                        argf = true;

                        while (p < stringArgs.Length && stringArgs[p] != c) {
                            if (stringArgs[p] == '\\') {
                                if (++p < stringArgs.Length) {
                                    arg.Append(stringArgs[p]);
                                }
                            }
                            else { 
                                arg.Append(stringArgs[p]);
                            }
                            ++p;
                        }
                        break;

                    default:
                        arg.Append(stringArgs[p]);
                        argf = true;
                        break;
                    }
                    ++p;
                }

                if (argf) {
                    jsonArgs.Add(arg.ToString());
                }
            }
        }
    }
}
