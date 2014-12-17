using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Language.StandardClassification;
using Metah.Compilation;
using Metah.MSBuild;

namespace Metah.VisualStudio.Editors.W {
    internal static class ContentTypeDefinitions {
        internal const string WContentType = "MetahW";
        internal const string WFileExtension = ".mw";
        [Export, BaseDefinition("code"), Name(WContentType)]
        internal static ContentTypeDefinition WContentTypeDefinition = null;
        [Export, ContentType(WContentType), FileExtension(WFileExtension)]
        internal static FileExtensionToContentTypeDefinition WFileExtensionDefinition = null;
    }

    [Export(typeof(IClassifierProvider)), ContentType(ContentTypeDefinitions.WContentType)]
    internal sealed class LanguageClassifierProvider : IClassifierProvider {
        [Import]
        internal IStandardClassificationService StandardService = null;
        [Import]
        internal IClassificationTypeRegistryService RegistryService = null;
        public IClassifier GetClassifier(ITextBuffer textBuffer) {
            return textBuffer.Properties.GetOrCreateSingletonProperty<LanguageClassifier>(() => new LanguageClassifier(textBuffer, StandardService, RegistryService));
        }
    }
    internal sealed class LanguageClassifier : LanguageClassifierBase {
        internal LanguageClassifier(ITextBuffer textBuffer, IStandardClassificationService standardService, IClassificationTypeRegistryService registryService)
            : base(textBuffer, standardService, registryService, _keywordSet) {
        }
        private static readonly HashSet<string> _keywordSet = WTokens.KeywordSet;
    }
    [Export(typeof(ITaggerProvider)), TagType(typeof(IErrorTag)), ContentType(ContentTypeDefinitions.WContentType)]
    internal sealed class LanguageErrorTaggerProvider : LanguageErrorTaggerProviderBase {
        internal LanguageErrorTaggerProvider() : base(WBuildErrorStore.FileName, WBuildErrorStore.TryLoad) { }
    }

}
