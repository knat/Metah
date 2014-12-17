using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Framework;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Metah.Compilation;

namespace Metah.MSBuild {
    public abstract class BuilderBase : Microsoft.Build.Utilities.Task {
        protected BuilderBase() { }
        [Required]
        public string ProjectDirectory { get; set; }
        public string PreprocessorSymbols { get; set; }
        public ITaskItem[] CSharpFiles { get; set; }
        public ITaskItem[] MetadataReferences { get; set; }
        //
        protected static readonly string BinDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        //
        private static readonly char[] _preprocessorSymbolSeparators = new char[] { ';', ',' };
        private static readonly char[] _aliasSeparators = new char[] { ',' };
        protected PreprocessorSymbolList CreatePreprocessorSymbolList() {
            var preprocessorSymbolList = new PreprocessorSymbolList();
            if (PreprocessorSymbols != null) {
                foreach (var s in PreprocessorSymbols.Split(_preprocessorSymbolSeparators, StringSplitOptions.RemoveEmptyEntries)) {
                    var s2 = s.Trim();
                    if (s2.Length > 0) {
                        preprocessorSymbolList.Add(s2);
                    }
                }
            }
            return preprocessorSymbolList;
        }
        protected MetadataReferenceList CreateMetadataReferenceList() {
            var list = new MetadataReferenceList();
            if (MetadataReferences != null) {
                foreach (var item in MetadataReferences) {
                    var fullPath = item.ItemSpec;
                    bool embedInteropTypes;
                    if (!TryGetBoolValue(item.GetMetadata("EmbedInteropTypes"), out embedInteropTypes)) {
                        embedInteropTypes = false;
                    }
                    var aliases = item.GetMetadata("Aliases");
                    var aliasArray = default(ImmutableArray<string>);
                    if (!string.IsNullOrEmpty(aliases)) {
                        var builder = ImmutableArray.CreateBuilder<string>();
                        foreach (var alias in aliases.Split(_aliasSeparators, StringSplitOptions.RemoveEmptyEntries)) {
                            var alias2 = alias.Trim();
                            if (alias2.Length > 0) {
                                builder.Add(alias2);
                            }
                        }
                        aliasArray = builder.ToImmutable();
                    }
                    list.Add(MetadataReference.CreateFromFile(
                        path: fullPath,
                        properties: new MetadataReferenceProperties(
                            kind: MetadataImageKind.Assembly,
                            aliases: aliasArray,
                            embedInteropTypes: embedInteropTypes)));
                }
            }
            return list;
        }
        protected static CompilationInputFileList CreateCompilationInputFileList(ITaskItem[] items, Func<string, bool> filter = null) {
            var fileList = new CompilationInputFileList();
            if (items != null) {
                foreach (var item in items) {
                    var fullPath = item.GetMetadata("FullPath");
                    if (filter == null || filter(fullPath))
                        fileList.Add(new CompilationInputFile(fullPath));
                }
            }
            return fileList;
        }
        protected string CopyFile(string fileName, bool overwrite) {
            var destPath = Path.Combine(ProjectDirectory, fileName);
            if (overwrite || !File.Exists(destPath))
                File.Copy(Path.Combine(BinDirectory, fileName), destPath, true);
            return destPath;
        }
        protected void LogError(Error error, BuildErrorStore errorStore) {
            string subcategory = error.Subkind.ToString();
            var errorCodeString = error.CodeString;
            string helpKeyword = null, filePath = null;
            int startLine = 0, startCol = 0, endLine = 0, endCol = 0;
            var sourceSpan = error.SourceSpan;
            if (sourceSpan != null) {
                filePath = sourceSpan.FilePath;
                startLine = sourceSpan.StartPosition.Line;
                startCol = sourceSpan.StartPosition.Character;
                endLine = sourceSpan.EndPosition.Line;
                endCol = sourceSpan.EndPosition.Character;
            }
            var message = error.Message;
            switch (error.Severity) {
                case ErrorSeverity.Error:
                    base.Log.LogError(subcategory, errorCodeString, helpKeyword, filePath, startLine, startCol, endLine, endCol, message);
                    break;
                case ErrorSeverity.Warning:
                    base.Log.LogWarning(subcategory, errorCodeString, helpKeyword, filePath, startLine, startCol, endLine, endCol, message);
                    break;
                case ErrorSeverity.Info:
                    base.Log.LogMessage(subcategory, errorCodeString, helpKeyword, filePath, startLine, startCol, endLine, endCol, MessageImportance.Normal, message);
                    break;
            }
            //quick & dirty
            if ((error.IsError || error.IsWarning) && filePath != null && !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) {
                BuildErrorUnit errorUnit;
                if (!errorStore.TryGetValue(filePath, out errorUnit)) {
                    errorUnit = new BuildErrorUnit(filePath, File.GetLastWriteTime(filePath));
                    errorStore.Add(filePath, errorUnit);
                }
                errorUnit.ErrorList.Add(error);
            }
            //end quick & dirty
        }
        private static bool TryGetBoolValue(string str, out bool value) {
            value = false;
            if (string.IsNullOrEmpty(str)) return false;
            if (string.Compare(str, "true", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "yes", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "on", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "!false", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "!no", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "!off", StringComparison.OrdinalIgnoreCase) == 0) {
                value = true;
                return true;
            }
            if (string.Compare(str, "false", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "no", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "off", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "!true", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "!yes", StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(str, "!on", StringComparison.OrdinalIgnoreCase) == 0) {
                return true;
            }
            return false;
            //throw new ArgumentException("Invalid bool value: " + str);
        }
    }

    //
    //quick & dirty
    [Serializable]
    public sealed class BuildErrorUnit {
        internal BuildErrorUnit(string filePath, DateTime lastWriteTime) {
            FilePath = filePath;
            LastWriteTime = lastWriteTime;
            ErrorList = new ErrorList();
        }
        public readonly string FilePath;
        public readonly DateTime LastWriteTime;
        public readonly ErrorList ErrorList;
    }
    [Serializable]
    public abstract class BuildErrorStore : Dictionary<string, BuildErrorUnit> {//key:FilePath
        protected BuildErrorStore() { }
        protected BuildErrorStore(SerializationInfo info, StreamingContext context) : base(info, context) { }
        protected void Save(string filePath) {
            File.Delete(filePath);
            using (var fs = File.Create(filePath)) {
                new BinaryFormatter().Serialize(fs, this);
            }
        }
        protected static T TryLoad<T>(string filePath) where T : BuildErrorStore {
            try {
                using (var fs = File.OpenRead(filePath)) {
                    var formatter = new BinaryFormatter();
                    formatter.Binder = ThisBinder.Instance;
                    return (T)formatter.Deserialize(fs);
                }
            }
            catch (Exception) { return null; }
        }
        private sealed class ThisBinder : SerializationBinder {
            internal static readonly ThisBinder Instance = new ThisBinder();
            public override Type BindToType(string assemblyName, string typeName) {
                return Type.GetType(typeName + ", " + assemblyName);
            }
        }
    }
    [Serializable]
    public sealed class XBuildErrorStore : BuildErrorStore {
        internal XBuildErrorStore() { }
        private XBuildErrorStore(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public const string FileName = "MetahXBldErrs.bin";
        new internal void Save(string projectDirectory) { base.Save(Path.Combine(projectDirectory, "obj", FileName)); }
        public static XBuildErrorStore TryLoad(string filePath) { return TryLoad<XBuildErrorStore>(filePath); }
    }
    [Serializable]
    public sealed class WBuildErrorStore : BuildErrorStore {
        internal WBuildErrorStore() { }
        private WBuildErrorStore(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public const string FileName = "MetahWBldErrs.bin";
        new internal void Save(string projectDirectory) { base.Save(Path.Combine(projectDirectory, "obj", FileName)); }
        public static WBuildErrorStore TryLoad(string filePath) { return TryLoad<WBuildErrorStore>(filePath); }
    }
    //end quick & dirty

}
