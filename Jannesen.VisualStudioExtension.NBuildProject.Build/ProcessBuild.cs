using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Jannesen.VisualStudioExtension.NBuildProject.Build.Library;

namespace Jannesen.VisualStudioExtension.NBuildProject.Build
{
    public class ProcessBuild: BaseTask
    {
        public      const       UInt32                  StatusMagic = 17636778;

        class LogFilter
        {
            private readonly    Regex                   _regex;
            private readonly    MessageImportance       _importance;
            private readonly    bool                    _warning;
            private readonly    string                  _subcategory;

            public              Regex                   Regex
            {
                get {
                    return _regex;
                }
            }
            public              MessageImportance       Importance
            {
                get {
                    return _importance;
                }
            }
            public              bool                    Warning
            {
                get {
                    return _warning;
                }
            }
            public              string                  Subcategory
            {
                get {
                    return _subcategory;
                }
            }

            public                                      LogFilter(ConfigReader configReader)
            {
                _regex       = new Regex(configReader.GetValueString("regex"));
                _importance  = configReader.GetValueEnum<MessageImportance>("importance", MessageImportance.High);
                _warning     = configReader.GetValueBool("warning", false);
                _subcategory = configReader.GetValueString("subcategory", "");

                configReader.NoChildElements();
            }
        };

        [Required]
        public                  string                              ProcessBuildConfig      { get; set; }
        public                  string                              Args                    { get; set; }
        public                  string                              ExtraStatusFiles        { get; set; }

        private                 string                              _base;
        private                 List<LogFilter>                     _cfg_logFilters;
        private                 int                                 _errorCount;
        private                 ProcessStartInfo                    _startInfo;

        public                                                      ProcessBuild()
        {
        }

        protected   override    bool                                Run()
        {
            _loadConfig(FullFileName(ProcessBuildConfig), "processbuild-config");

            _execProcess(_startInfo);

            return (this._errorCount == 0);
        }

        private                 void                                _loadConfig(string configname, string rootname)
        {
            ConfigReader reader = null;

            _startInfo      = new ProcessStartInfo();
            _cfg_logFilters = new List<LogFilter>();

            try {
                _base = Path.GetDirectoryName(configname);

                reader = new ConfigReader(configname);

                reader.ReadRootNode(rootname);

                if (!reader.hasChildren)
                    throw new ConfigException("Empty configuration");

                if (reader.hasChildren) {
                    while (reader.ReadNextElement()) {
                        switch(reader.ElementName) {
                        case "process":
                            foreach(string name in reader.GetAttibutes()) {
                                string value = _expandValue(reader.GetValueString(name));

                                if (name.StartsWith("env.", StringComparison.Ordinal)) {
                                    _startInfo.EnvironmentVariables[name.Substring(4)] = value;
                                }
                                else {
                                    switch(name) {
                                    case "filename":
                                        if (!File.Exists(value)) {
                                            throw new Exception("process.filename '" + value + "' don't exists.");
                                        }
                                        _startInfo.FileName = value;
                                        break;

                                    case "workingdirectory":
                                        if (!Directory.Exists(value)) {
                                            throw new Exception("process.workingdirectory '" + value + "' don't exists.");
                                        }
                                        _startInfo.WorkingDirectory = value;
                                        break;

                                    case "arguments":
                                        _startInfo.Arguments = value;
                                        break;

                                    default:
                                        throw new Exception("Unknown process property '" + name + "'.");
                                    }
                                }
                            }

                            reader.NoChildElements();
                            break;

                        case "logfilter":
                            _cfg_logFilters.Add(new LogFilter(reader));
                            break;

                        default:
                            throw new ConfigException("Unknown element '" + reader.ElementName + "'.");
                        }
                    }
                }

                if (_startInfo.FileName == null || _startInfo.WorkingDirectory == null || _startInfo.Arguments == null)
                    throw new BuildException("Missing parameter in config '" + ProcessBuildConfig + "' missing.");

            }
            catch(Exception err) {
                throw new ConfigException("Loading configuration from " + configname + " failed" + (reader != null ? " at line "+reader.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)+ "." : "."), err);
            }
            finally {
                if (reader != null)
                    reader.Dispose();
            }
        }
        private                 void                                _execProcess(ProcessStartInfo startInfo)
        {
            try {
                startInfo.WorkingDirectory       = ProjectDirectory;
                startInfo.CreateNoWindow         = false;
                startInfo.ErrorDialog            = false;
                startInfo.UseShellExecute        = false;
                startInfo.RedirectStandardInput  = true;
                startInfo.RedirectStandardError  = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;

                Process process = Process.Start(startInfo);
                process.Start();
                process.StandardInput.Close();

                using (BlockingCollection<string> queue = new BlockingCollection<string>()) {
                    string      line;

                    Task<Exception> asyncReaderOutput = _copyStreamLinesToQueue(process.StandardOutput, queue);
                    Task<Exception> asyncReaderError  = _copyStreamLinesToQueue(process.StandardError,  queue);

                    while (process.StandardOutput.BaseStream != null || process.StandardError.BaseStream != null ) {
                        while ((line = queue.Take()) != null)
                            _log(line);
                    }

                    while (queue.TryTake(out line)) {
                        if (line != null)
                            _log(line);
                    }

                    if (asyncReaderOutput.Result != null)
                        throw new BuildException("Async reader failed.", asyncReaderOutput.Result);

                    if (asyncReaderError.Result != null)
                        throw new BuildException("Async reader failed.", asyncReaderError.Result);
                }

                if (process.ExitCode != 0) {
                    if (this._errorCount == 0) {
                        ++this._errorCount;
                        Log.LogError("Buildstep failed without error.");
                    }
                }
            }
            catch(Exception err) {
                throw new BuildException("ExecProcess failed.", err);
            }
        }

        private                 string                              _expandValue(string value)
        {
            int         start = 0;
            int         i;

            while ((i = value.IndexOf("$(", start, StringComparison.Ordinal)) >= 0) {
                int     e = value.IndexOf(')', i+2);
                if (e < 0)
                    break;

                string name   = value.Substring(i+2, e-i-2);
                string lookup = _variableLookup(name);

                if (lookup == null)
                    throw new Exception("'" + name + "' not defined.");

                value = value.Substring(0, i) + lookup + value.Substring(e+1);
            }

            return value;
        }
        private                 string                              _variableLookup(string name)
        {
            switch(name) {
            case "base":
                return _base;

            case "ProjectDirectory":
                return ProjectDirectory;

            case "Args":
                return Args;

            default:
                throw new BuildException("Unknown variable '" + name + "'.");
            }
        }
        private static async    Task<Exception>                     _copyStreamLinesToQueue(StreamReader stream, BlockingCollection<string> queue)
        {
            try {
                string  line;

                while ((line = await stream.ReadLineAsync()) != null)
                    queue.Add(line);

                stream.Dispose();

                queue.Add(null);

                return null;
            }
            catch(Exception err) {
                return err;
            }
        }
        private                 void                                _log(string line)
        {
            try {
                Match       match;

                line = line.TrimEnd();

                if (line.Length == 0)
                    return ;

                foreach(LogFilter logFilter in _cfg_logFilters) {
                    if ((match = logFilter.Regex.Match(line)).Success) {
                        string msg      = match.Groups["msg"].Value;
                        string file     = match.Groups["file"].Value;

                        if (!String.IsNullOrEmpty(file)) {
                            file = FullFileName(file);
                            string lineno     = match.Groups["lineno"   ].Value;
                            string colno      = match.Groups["colno"    ].Value;
                            string endlineno  = match.Groups["endlineno"].Value;
                            string endcolno   = match.Groups["endcolno" ].Value;
                            string code       = match.Groups["code"     ].Value;

                            if(logFilter.Warning) {
                                BuildEngine.LogWarningEvent(new BuildWarningEventArgs(logFilter.Subcategory,
                                                                                      code,
                                                                                      file,
                                                                                      (!String.IsNullOrEmpty(lineno)    ? int.Parse(lineno, System.Globalization.CultureInfo.InvariantCulture)    : 0),
                                                                                      (!String.IsNullOrEmpty(colno)     ? int.Parse(colno, System.Globalization.CultureInfo.InvariantCulture)     : 0),
                                                                                      (!String.IsNullOrEmpty(endlineno) ? int.Parse(endlineno, System.Globalization.CultureInfo.InvariantCulture) : 0),
                                                                                      (!String.IsNullOrEmpty(endcolno)  ? int.Parse(endcolno, System.Globalization.CultureInfo.InvariantCulture)  : 0),
                                                                                      msg,
                                                                                      "",
                                                                                      ""));
                            }
                            else {
                                ++this._errorCount;
                                BuildEngine.LogErrorEvent(new BuildErrorEventArgs(logFilter.Subcategory,
                                                                                  code,
                                                                                  file,
                                                                                  (!String.IsNullOrEmpty(lineno)    ? int.Parse(lineno, System.Globalization.CultureInfo.InvariantCulture)    : 0),
                                                                                  (!String.IsNullOrEmpty(colno)     ? int.Parse(colno, System.Globalization.CultureInfo.InvariantCulture)     : 0),
                                                                                  (!String.IsNullOrEmpty(endlineno) ? int.Parse(endlineno, System.Globalization.CultureInfo.InvariantCulture) : 0),
                                                                                  (!String.IsNullOrEmpty(endcolno)  ? int.Parse(endcolno, System.Globalization.CultureInfo.InvariantCulture)  : 0),
                                                                                  msg,
                                                                                  "",
                                                                                  ""));
                            }
                        }
                        else
                            Log.LogMessage(logFilter.Importance, match.Groups["msg"].Value);

                        return;
                    }
                }

                Log.LogMessage(MessageImportance.High, line);
            }
            catch(Exception err) {
                Log.LogMessage(MessageImportance.High, "Error in _log: " + err.Message);
            }
        }
    }
}
