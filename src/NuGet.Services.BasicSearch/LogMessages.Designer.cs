﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NuGet.Services.BasicSearch {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class LogMessages {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal LogMessages() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NuGet.Services.BasicSearch.LogMessages", typeof(LogMessages).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Application Startup called.
        /// </summary>
        internal static string AppStartup {
            get {
                return ResourceManager.GetString("AppStartup", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SearcherManager is not initialized.
        /// </summary>
        internal static string SearcherManagerNotInitialized {
            get {
                return ResourceManager.GetString("SearcherManagerNotInitialized", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Search index is already being reopened so thread ID {ThreadId} will not try to reopen the index again.
        /// </summary>
        internal static string SearchIndexAlreadyReopened {
            get {
                return ResourceManager.GetString("SearchIndexAlreadyReopened", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Search service is configured to refresh the index every {SearchIndexRefresh} seconds.
        /// </summary>
        internal static string SearchIndexRefreshConfiguration {
            get {
                return ResourceManager.GetString("SearchIndexRefreshConfiguration", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Reopening the search index took {ElapsedSeconds} seconds on thread ID {ThreadId}.
        /// </summary>
        internal static string SearchIndexReopenCompleted {
            get {
                return ResourceManager.GetString("SearchIndexReopenCompleted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Search index reopen failed with exception: {Exception}.
        /// </summary>
        internal static string SearchIndexReopenFailed {
            get {
                return ResourceManager.GetString("SearchIndexReopenFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Beginning to reopen the search index on thread ID {ThreadId}.
        /// </summary>
        internal static string SearchIndexReopenStarted {
            get {
                return ResourceManager.GetString("SearchIndexReopenStarted", resourceCulture);
            }
        }
    }
}
