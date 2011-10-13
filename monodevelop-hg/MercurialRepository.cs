// Copyright (C) 2007 by Jelmer Vernooij <jelmer@samba.org>
//		
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//	
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//   
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Threading;

using Gtk;
using MonoDevelop.Core;
using MonoDevelop.VersionControl;

namespace MonoDevelop.VersionControl.Mercurial
{
	public class MercurialRepository : UrlBasedRepository
	{
		private Dictionary<string,string> tempfiles;
		private Dictionary<FilePath,VersionInfo> statusCache;
		private Dictionary<FilePath,VersionInfo[]> directoryStatusCache;
		private HashSet<FilePath> updatedOnce;
		internal MercurialClient Client{ get; private set; }

		public override string[] SupportedProtocols {
			get { return MercurialVersionControl.protocols; }
		}		
		
		public MercurialRepository ()
		{
			Init ();
		}
		
		public MercurialRepository (MercurialVersionControl vcs, string url) : base (vcs)
		{
			Init ();
			Url = url;
			Client = new MercurialCommandClient (new Uri (url).AbsolutePath, null);
			Ide.IdeApp.Workspace.SolutionUnloaded += HandleSolutionUnloaded;
		}

		void HandleSolutionUnloaded (object sender, MonoDevelop.Projects.SolutionEventArgs e)
		{
			updatedOnce.Clear ();
		}
		
		~MercurialRepository ()
		{
			Ide.IdeApp.Workspace.SolutionLoaded -= HandleSolutionUnloaded;
		}

		private void Init ()
		{
			tempfiles = new Dictionary<string,string> ();
			updatedOnce = new HashSet<FilePath> ();
			statusCache = new Dictionary<FilePath, VersionInfo> ();
			directoryStatusCache = new Dictionary<FilePath, VersionInfo[]> ();
		}// Init

		/// <summary>
		/// Remove temp files
		/// </summary>
		~MercurialRepository ()
		{
			foreach (string tmpfile in tempfiles.Values) {
				if (File.Exists (tmpfile)){ File.Delete (tmpfile); }
			}
		}// finalizer
		
		public override bool HasChildRepositories { 
			get { return true; /* TODO */ }
		}
		
		public MercurialVersionControl Mercurial {
			get { return (MercurialVersionControl) VersionControlSystem; }
		}

		public override IEnumerable<Repository> ChildRepositories {
			get {
				List<Repository> repos = new List<Repository> ();

				// System.Console.WriteLine ("Getting children for {0}", Url);
				
				foreach (string directory in Client.List (Url, true, ListKind.Directory)) {
					int lastSep = directory.LastIndexOf ("/");
					if (0 < lastSep && Url.EndsWith (directory.Substring (0, lastSep))) {
						repos.Add (new MercurialRepository (Mercurial, directory));
					}// if the directory is a direct child of Url
				}// for each directory

				// foreach (Repository repo in repos){ System.Console.WriteLine ("Child repo {0}", ((UrlBasedRepository)repo).Url); }

				return repos;
			}// get
		}// ChildRepositories

		/// <value>
		/// The path where .bzr is located
		/// </value>
		public string LocalBasePath {
			get {
				return localBasePath;
			}// get
			set {
				localBasePath = value;
			}// set
		}// LocalBasePath
		private string localBasePath;

		/// <remarks>
		/// Since hg doesn't store a text-base like svn,
		/// we're catting the baseline text
		/// </remarks>
		public override string GetBaseText (FilePath localFilePath)
		{
			string localFile = localFilePath.FullPath;

			try {
				return Client.GetTextAtRevision (localFile, new MercurialRevision (this, MercurialRevision.HEAD));
			} catch (Exception e) {
				LoggingService.LogError ("Error getting base text", e);
			}

			return localFile;
		}

		public override Revision[] GetHistory (FilePath localFilePath, Revision since)
		{
			if (null == LocalBasePath) {
				// System.Console.WriteLine ("Getting local base path for {0}", localFile);
				LocalBasePath = GetLocalBasePath (localFilePath.FullPath);
				// System.Console.WriteLine ("Got base path {0}", LocalBasePath);
			}
			return Client.GetHistory (this, localFilePath.FullPath, (MercurialRevision)since);
		}
		
		public Revision[] GetIncoming (string remote) {
			return Client.GetIncoming (this, remote);
		}
		
		public Revision[] GetOutgoing (string remote) {
			return Client.GetOutgoing (this, remote);
		}
		
		protected override IEnumerable<VersionInfo> OnGetVersionInfo (IEnumerable<FilePath> localPaths, bool getRemoteStatus)
		{
			return localPaths.Select (localPath => {
				var status = GetVersionInfo (this, localPath.FullPath, getRemoteStatus);
				lock (statusCache) {
					statusCache[localPath.CanonicalPath] = status;
				}
				return status;
			});
		}
		
		/// <summary>
		/// Prefer cached version info for non-essential operations (menu display)
		/// </summary>
		private VersionInfo GetCachedVersionInfo (FilePath localPath, bool getRemoteStatus)
		{
			VersionInfo status = null;
			
			lock (statusCache) {
				if (statusCache.ContainsKey (localPath)) {
					status = statusCache[localPath.CanonicalPath];
				} else {
					status = new VersionInfo (localPath, GetLocalBasePath (localPath), Directory.Exists (localPath), VersionStatus.Unversioned, null, VersionStatus.Unversioned, null);
				}
			}
			
			return status;
		}
		
		protected override VersionInfo[] OnGetDirectoryVersionInfo (FilePath localDirectory, bool getRemoteStatus, bool recursive)
		{
			VersionInfo[] versions = GetDirectoryVersionInfo (this, localDirectory.FullPath, getRemoteStatus, recursive);
			if (null != versions) {
				lock (statusCache) {
					foreach (VersionInfo version in versions) {
						statusCache[version.LocalPath.CanonicalPath] = version;
					}
				}
			}
			return versions;
		}

		protected override RevisionPath[] OnGetRevisionChanges (Revision revision)
		{
			return Client.GetRevisionChanges (this, (MercurialRevision)revision);
		}		
		
		// Deprecated
		public override Repository Publish (string serverPath, FilePath localPath, FilePath[] files, string message, IProgressMonitor monitor)
		{
			serverPath = string.Format ("{0}{1}{2}", Url, Url.EndsWith ("/")? string.Empty: "/", serverPath);
			// System.Console.WriteLine ("Got publish {0} {1}", serverPath, localPath);
			Client.StoreCredentials (serverPath);
			Client.Push  (serverPath, localPath.FullPath, false, false, monitor);
			return new MercurialRepository (Mercurial, serverPath);
		}
		
		public virtual void Push (string pushLocation, FilePath localPath, bool remember, bool overwrite, bool omitHistory, IProgressMonitor monitor) {
			Client.StoreCredentials (pushLocation);
			Client.Push (pushLocation, localPath.FullPath, remember, overwrite, monitor);
		}// Push

		public virtual void Pull (string pullLocation, FilePath localPath, bool remember, bool overwrite, IProgressMonitor monitor) {
			Client.StoreCredentials (pullLocation); Client.Pull (pullLocation, localPath.FullPath, remember, overwrite, monitor);
		}// Pull

		public virtual void Rebase (string pullLocation, FilePath localPath, bool remember, bool overwrite, IProgressMonitor monitor) {
			Client.StoreCredentials (pullLocation);
			Client.Rebase (pullLocation, localPath.FullPath, monitor);
		}// Rebase

		public virtual void Merge () {
			Client.Merge (this);
		}// Merge
		
		public override void Update (FilePath[] localPaths, bool recurse, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Client.Update (localPath.FullPath, recurse, monitor);
		}

		public override void Commit (ChangeSet changeSet, IProgressMonitor monitor)
		{
			Client.Commit (changeSet, monitor);
		}

		public override void Checkout (FilePath targetLocalPath, Revision rev, bool recurse, IProgressMonitor monitor)
		{
			Client.StoreCredentials (Url);
			MercurialRevision brev = (null == rev)? new MercurialRevision (this, MercurialRevision.HEAD): (MercurialRevision)rev;
			Client.Checkout (Url, targetLocalPath.FullPath, brev, recurse, monitor);
		}

		public override void Revert (FilePath[] localPaths, bool recurse, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Client.Revert (localPath.FullPath, recurse, monitor, new MercurialRevision (this, MercurialRevision.HEAD));
		}
		
		public override void Add (FilePath[] localPaths, bool recurse, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Client.Add (localPath.FullPath, recurse, monitor);
		}

		public override string GetTextAtRevision (FilePath repositoryPath, Revision revision)
		{
			// System.Console.WriteLine ("Got GetTextAtRevision for {0}", repositoryPath);
			MercurialRevision brev = (null == revision)? new MercurialRevision (this, MercurialRevision.HEAD): (MercurialRevision)revision;
			return Client.GetTextAtRevision (repositoryPath.FullPath, brev);
		}

		public override void RevertToRevision (FilePath localPath, Revision revision, IProgressMonitor monitor)
		{
			if (IsModified (MercurialRepository.GetLocalBasePath (localPath))) {
				MessageDialog md = new MessageDialog (MonoDevelop.Ide.IdeApp.Workbench.RootWindow, DialogFlags.Modal, 
				                                      MessageType.Question, ButtonsType.YesNo, 
				                                      GettextCatalog.GetString ("You have uncommitted local changes. Revert anyway?"));
				try {
					if ((int)ResponseType.Yes != md.Run ()) {
						return;
					}
				} finally {
					md.Destroy ();
				}
			}// warn about uncommitted changes

			MercurialRevision brev = (null == revision)? new MercurialRevision (this, MercurialRevision.HEAD): (MercurialRevision)revision;
			Client.Revert (localPath.FullPath, true, monitor, brev);
		}

		public override void RevertRevision (FilePath localPath, Revision revision, IProgressMonitor monitor)
		{
//			if (IsModified (MercurialRepository.GetLocalBasePath (localPath))) {
//				MessageDialog md = new MessageDialog (null, DialogFlags.Modal, 
//				                                      MessageType.Question, ButtonsType.YesNo, 
//				                                      GettextCatalog.GetString ("You have uncommitted local changes. Revert anyway?"));
//				try {
//					if ((int)ResponseType.Yes != md.Run ()) {
//						return;
//					}
//				} finally {
//					md.Destroy ();
//				}
//			}// warn about uncommitted changes
//
//			MercurialRevision brev = (MercurialRevision)revision;
//			string localPathStr = localPath.FullPath;
//			Mercurial.Merge (localPathStr, localPathStr, false, true, brev, (MercurialRevision)(brev.GetPrevious ()), monitor);
		}
		
		internal bool IsVersioned (FilePath localPath) {
			if (string.IsNullOrEmpty (GetLocalBasePath (localPath.FullPath))) {
				return false;
			}
			
			VersionInfo info = GetCachedVersionInfo (localPath, false);
			return (null != info && info.IsVersioned);
		}
		
		internal bool IsModified (FilePath localFile)
		{
			if (string.IsNullOrEmpty (GetLocalBasePath (localFile.FullPath))) {
			return false;
			}
	
			VersionInfo info = GetCachedVersionInfo (localFile, false);
			return (null != info && info.IsVersioned && info.HasLocalChanges);
		}
		
		protected override VersionControlOperation GetSupportedOperations (VersionInfo vinfo)
		{
			VersionControlOperation operations = VersionControlOperation.None;
			bool exists = !vinfo.LocalPath.IsNullOrEmpty && (File.Exists (vinfo.LocalPath) || Directory.Exists (vinfo.LocalPath));
			if (vinfo.IsVersioned) {
				if (exists) {
					operations = VersionControlOperation.Update | VersionControlOperation.Log | VersionControlOperation.Remove | VersionControlOperation.Annotate;
					if (vinfo.HasLocalChanges || vinfo.IsDirectory)
						operations |= VersionControlOperation.Revert | VersionControlOperation.Commit;
					if ((vinfo.Status & VersionStatus.Conflicted) == VersionStatus.Conflicted)
						operations |= VersionControlOperation.Revert;
				}
			} else if (exists) {
				operations = VersionControlOperation.Add;
			}
			return operations;
		}

		public virtual bool IsConflicted (FilePath localFile)
		{
			if (string.IsNullOrEmpty (GetLocalBasePath (localFile.FullPath))) {
				return false;
			}
			
			VersionInfo info = GetCachedVersionInfo (localFile, false);
			return (null != info && info.IsVersioned && (0 != (info.Status & VersionStatus.Conflicted)));
		}

		public virtual bool CanResolve (FilePath localPath)
		{
			return IsConflicted (localPath);
		}

		public virtual bool CanPull (FilePath localPath)
		{
			return Directory.Exists (localPath.FullPath) && IsVersioned (localPath);
		}// CanPull

		public virtual bool CanMerge (FilePath localPath)
		{
			return (Client.GetHeads (this).Length > 1);
		}// CanMerge
		
		public virtual bool CanBind (FilePath localPath)
		{
			return Directory.Exists (localPath.FullPath) && !IsBound (localPath);
		}// CanBind
		
		public virtual bool CanUnbind (FilePath localPath)
		{
			return Directory.Exists (localPath.FullPath) && IsBound (localPath);
		}// CanUnbind
		
		public virtual bool CanUncommit (FilePath localPath)
		{
			return Directory.Exists (localPath.FullPath) && IsVersioned (localPath);
		}// CanUncommit
		
		/// <summary>
		/// Finds the repository root for a path
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: A path somewhere in a hg repository
		/// </param>
		/// <returns>
		/// A <see cref="System.String"/>: The path to the branch root,
		/// or string.Empty
		/// </returns>
		public static string GetLocalBasePath (string localPath) {
			if (null == localPath){ return string.Empty; }
			if (Directory.Exists (Path.Combine (localPath, ".hg"))){ return localPath; }

			return GetLocalBasePath (Path.GetDirectoryName (localPath));
		}// GetLocalBasePath

		public override DiffInfo[] PathDiff (FilePath baseLocalPath, FilePath[] localPaths, bool remoteDiff)
		{
			string[] localFiles = new string[null == localPaths? 0: localPaths.Length];
			for(int i=0; i<localFiles.Length; ++i){ 
				localFiles[i] = localPaths[i].ToRelative (baseLocalPath.FullPath);
			}
			
			return Client.Diff (baseLocalPath.FullPath, localFiles);
		}// PathDiff

		public override DiffInfo[] PathDiff (FilePath localPath, Revision fromRevision, Revision toRevision)
		{
			return Client.Diff (localPath, (MercurialRevision)fromRevision, (MercurialRevision)toRevision);
		}

		public override void DeleteFiles (FilePath[] localPaths, bool force, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Client.Remove (localPath.FullPath, force, monitor);
		}// DeleteFiles

		public override void DeleteDirectories (FilePath[] localPaths, bool force, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Client.Remove (localPath.FullPath, force, monitor);
		}// DeleteDirectories

		public virtual void Resolve (FilePath[] localPaths, bool recurse, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Client.Resolve (localPath.FullPath, recurse, monitor);
		}// Resolve

		public virtual Dictionary<string, BranchType> GetKnownBranches (FilePath localPath)
		{
			return Client.GetKnownBranches (localPath.FullPath);
		}// GetKnownBranches
		
		public virtual void Ignore (FilePath localPath)
		{
			Client.Ignore (localPath.FullPath);
		}// Ignore
		
		public virtual bool IsBound (FilePath localPath)
		{
			return Client.IsBound (localPath.FullPath);
		}// IsBound
		
		public virtual string GetBoundBranch (FilePath localPath)
		{
			return Client.GetBoundBranch (localPath.FullPath);
		}// GetBoundBranch
		
		public virtual void Bind (string branchUrl, FilePath localPath, IProgressMonitor monitor)
		{
			Client.Bind (branchUrl, localPath.FullPath, monitor);
		}// Bind
		
		public virtual void Unbind (FilePath localPath, IProgressMonitor monitor)
		{
			Client.Unbind (localPath.FullPath, monitor);
		}// Unbind
		
		public virtual void Uncommit (FilePath localPath, IProgressMonitor monitor)
		{
			if (IsModified (localPath)) {
				MonoDevelop.Ide.DispatchService.GuiSyncDispatch(delegate{ 
					new MessageDialog (MonoDevelop.Ide.IdeApp.Workbench.RootWindow, DialogFlags.Modal, MessageType.Warning, ButtonsType.Close,
					                   "There are uncommitted changed in the working copy. Aborting...").ShowAll ();
				});
			} else {
				Client.Uncommit (localPath.FullPath, monitor);
			}
		}// Uncommit
		
		public override Annotation[] GetAnnotations (FilePath localPath)
		{
			return Client.GetAnnotations (localPath.FullPath);
		}// GetAnnotations
		
		/// <summary>
		/// Export a (portion of a) local tree.
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="FilePath"/>: The path to be exported.
		/// </param>
		/// <param name="exportPath">
		/// A <see cref="FilePath"/>: The output path.
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>
		/// </param>
		public virtual void Export (FilePath localPath, FilePath exportLocation, IProgressMonitor monitor)
		{
			Client.Export (localPath.FullPath, exportLocation.FullPath, monitor);
		}// Export
		
		public virtual bool CanRebase ()
		{
			return Client.CanRebase ();
		}// CanRebase
		
		VersionInfo GetVersionInfo (Repository repo, string localPath, bool getRemoteStatus)
		{
			localPath = ((FilePath)localPath).CanonicalPath;
			// Console.WriteLine ("GetVersionInfo {0}", localPath);
			
			VersionInfo status = GetCachedVersionInfo (localPath, getRemoteStatus);
			
			ThreadPool.QueueUserWorkItem (delegate {
				Thread.Yield ();
				var info = GetFileStatus (this, localPath, getRemoteStatus);
				Thread.Yield ();
				
				bool notify = !updatedOnce.Contains (localPath);
				updatedOnce.Add (localPath);
				lock (statusCache) {
					statusCache[localPath] = info;
				}
				
				// Use the base notifier to make the first change
				// ripple back to here
				if (notify)
					Ide.DispatchService.GuiDispatch (delegate {
						// Console.WriteLine ("Notifying {0}", localPath);
						FileService.NotifyFileChanged (localPath);
					});
				NotifyFileChanged (localPath);
			});
			
			return status;
		}// GetVersionInfo

		VersionInfo[] GetDirectoryVersionInfo (Repository repo, string sourcepath, bool getRemoteStatus, bool recursive) {
			sourcepath = ((FilePath)sourcepath).CanonicalPath;
			// Console.WriteLine ("GetDirectoryVersionInfo {0}", sourcepath);
			
			ThreadPool.QueueUserWorkItem (delegate {
				Thread.Yield ();
				IEnumerable<LocalStatus> someStatuses = Client.Status (sourcepath, null);
				lock (statusCache) {
					var someInfos = CreateNodes (repo, someStatuses);
					directoryStatusCache[sourcepath] = someInfos;
					foreach (var status in someInfos) {
						// updatedOnce.Add (status.LocalPath.CanonicalPath);
						statusCache[status.LocalPath.CanonicalPath] = status;
					}
				}
				foreach (var status in someStatuses)
					NotifyFileChanged (status.Filename);
			});
			if (directoryStatusCache.ContainsKey (sourcepath)) {
				return directoryStatusCache[sourcepath];
			}
			return new []{ GetCachedVersionInfo (sourcepath, getRemoteStatus) };
		}
		
		/// <summary>
		/// Gets the status of a version-controlled file
		/// </summary>
		/// <param name="repo">
		/// A <see cref="Repository"/> to which the file belongs
		/// </param>
		/// <param name="sourcefile">
		/// A <see cref="System.String"/>: The filename
		/// </param>
		/// <param name="getRemoteStatus">
		/// A <see cref="System.Boolean"/>: unused
		/// </param>
		/// <returns>
		/// A <see cref="VersionInfo"/> representing the file status
		/// </returns>
		private VersionInfo GetFileStatus (Repository repo, string sourcefile, bool getRemoteStatus)
		{
			IEnumerable<LocalStatus > statuses = Client.Status (sourcefile, null);
			Func<LocalStatus,bool> match = (status => status.Filename == sourcefile);
			
			if (null == statuses || statuses.Count () == 0 || !statuses.Any (match))
				throw new ArgumentException ("Path '" + sourcefile + "' does not exist in the repository.");
			
			return CreateNode (statuses.First (match), repo);
		}// GetFileStatus

		/// <summary>
		/// Create a VersionInfo from a LocalStatus
		/// </summary>
		private VersionInfo CreateNode (LocalStatus status, Repository repo) 
		{
			VersionStatus rs = VersionStatus.Unversioned;
			Revision rr = null;
			
			// Console.WriteLine ("Creating node for status {0}", status.Filename);
			
			VersionStatus vstatus = ConvertStatus (status.Status);
			// System.Console.WriteLine ("Converted {0} to {1} for {2}", status.Status, vstatus, status.Filename);

			VersionInfo ret = new VersionInfo (status.Filename, Path.GetFullPath (status.Filename), Directory.Exists (status.Filename),
			                                   vstatus, new MercurialRevision (repo, status.Revision),
			                                   rs, rr);
			return ret;
		}// CreateNode

		/// <summary>
		/// Create a VersionInfo[] from an IList<LocalStatus>
		/// </summary>
		private VersionInfo[] CreateNodes (Repository repo, IEnumerable<LocalStatus> statuses) {
			List<VersionInfo> nodes = new List<VersionInfo> (statuses.Count ());

			foreach (LocalStatus status in statuses) {
				nodes.Add (CreateNode (status, repo));
			}

			return nodes.ToArray ();
		}// CreateNodes
		

		/// <summary>
		/// Convert an ItemStatus to a VersionStatus
		/// </summary>
		private VersionStatus ConvertStatus (ItemStatus status) {
			switch (status) {
			case ItemStatus.Added:
				return VersionStatus.Versioned | VersionStatus.ScheduledAdd;
			case ItemStatus.Conflicted:
				return VersionStatus.Versioned | VersionStatus.Conflicted;
			case ItemStatus.Deleted:
				return VersionStatus.Versioned | VersionStatus.ScheduledDelete;
			case ItemStatus.Ignored:
				return VersionStatus.Versioned | VersionStatus.Ignored;
			case ItemStatus.Modified:
				return VersionStatus.Versioned | VersionStatus.Modified;
			case ItemStatus.Replaced:
				return VersionStatus.Versioned | VersionStatus.ScheduledReplace;
			case ItemStatus.Unchanged:
				return VersionStatus.Versioned;
			}

			return VersionStatus.Unversioned;
		}// ConvertStatus
	}// MercurialRepository
}
