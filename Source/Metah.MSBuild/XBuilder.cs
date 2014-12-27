using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Metah.Compilation;

namespace Metah.MSBuild.X {
    public sealed class MetahXBuilder : BuilderBase {
        public ITaskItem[] XCSharpFiles { get; set; }
        public ITaskItem[] XFiles { get; set; }
        public bool EmbedSDOM { get; set; }
        [Output]
        public ITaskItem[] OutputCSharpFiles { get; set; }
        //
        public override bool Execute() {
            var errorStore = new XBuildErrorStore();
            try {
                var mxFilePath = CopyFile("Metah.X.cs", false);//EmbedSDOM);
                CopyFile("Metah.X.dll", false);
                if ((XCSharpFiles == null || XCSharpFiles.Length == 0) && (XFiles == null || XFiles.Length == 0))
                {
                    base.Log.LogMessage(MessageImportance.High, "Skip compilation");
                    return true;
                }
                var xCSharpFileList = CreateCompilationInputFileList(XCSharpFiles);
                var xFileList = CreateCompilationInputFileList(XFiles);
                var cSharpFileList = CreateCompilationInputFileList(CSharpFiles);
                if (EmbedSDOM) cSharpFileList.Add(new CompilationInputFile(mxFilePath));
                var preprocessorSymbolList = CreatePreprocessorSymbolList();
                var metadataReferenceList = CreateMetadataReferenceList();
                var compilationInput = new XCompilationInput(preprocessorSymbolList, cSharpFileList, metadataReferenceList, xCSharpFileList, xFileList);
                var compilationOutput = XCompiler.Compile(compilationInput);
                foreach (var error in compilationOutput.ErrorList) LogError(error, errorStore);
                if (compilationOutput.HasErrors) return false;
                var outputCSharpFileList = new List<TaskItem>();
                if (EmbedSDOM) outputCSharpFileList.Add(new TaskItem(mxFilePath));
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
