using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Metah.Compilation;

namespace Metah.MSBuild.W {
    public sealed class MetahWBuilder : BuilderBase {
        public ITaskItem[] WFiles { get; set; }
        //public bool EmbedRuntime { get; set; }
        [Output]
        public ITaskItem[] OutputCSharpFiles { get; set; }
        //
        public override bool Execute() {
            var errorStore = new WBuildErrorStore();
            try {
                if ((WFiles == null || WFiles.Length == 0)) {
                    base.Log.LogMessage(MessageImportance.High, "Skip compilation");
                    return true;
                }
                var wFileList = CreateCompilationInputFileList(WFiles);
                var cSharpFileList = CreateCompilationInputFileList(CSharpFiles);
                //var mxFilePath = CopyFile("Metah.X.cs", EmbedRuntime);
                //if (EmbedRuntime) cSharpFileList.Add(new CompilationInputFile(mxFilePath));
                var preprocessorSymbolList = CreatePreprocessorSymbolList();
                var metadataReferenceList = CreateMetadataReferenceList();
                var compilationInput = new WCompilationInput(preprocessorSymbolList, cSharpFileList, metadataReferenceList, wFileList);
                var compilationOutput = WCompiler.Compile(compilationInput);
                foreach (var error in compilationOutput.ErrorList) LogError(error, errorStore);
                if (compilationOutput.HasErrors) return false;
                var outputCSharpFileList = new List<TaskItem>();
                //if (EmbedRuntime) outputCSharpFileList.Add(new TaskItem(mxFilePath));
                if (compilationOutput.Analyzer != null) {
                    foreach (var compilationUnit in compilationOutput.Analyzer.CompilationUnits) {
                        var filePath = compilationUnit.FilePath + ".cs";
                        File.WriteAllText(filePath, compilationUnit.CSText);
                        outputCSharpFileList.Add(new TaskItem(filePath));
                    }
                }
                OutputCSharpFiles = outputCSharpFileList.ToArray();
                return true;
            }
            catch (Exception ex) {
                base.Log.LogErrorFromException(ex, true, true, null);
                return false;
            }
            finally { errorStore.Save(ProjectDirectory); }
            //C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe D:\Test\TestPLX\TestPLX\TestPLX.csproj
            //v:detailed
        }
    }
}
