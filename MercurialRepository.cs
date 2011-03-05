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
		}

		private void Init ()
		{
			tempfiles = new Dictionary<string,string> ();
			statusCache = new Dictionary<FilePath, VersionInfo> ();
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
				
				foreach (string directory in Mercurial.List (Url, true, ListKind.Directory)) {
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
				return Mercurial.GetTextAtRevision (localFile, new MercurialRevision (this, MercurialRevision.HEAD));
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
			return Mercurial.GetHistory (this, localFilePath.FullPath, since);
		}
		
		public override VersionInfo GetVersionInfo (FilePath localPath, bool getRemoteStatus)
		{
			return statusCache[localPath] = Mercurial.GetVersionInfo (this, localPath.FullPath, getRemoteStatus);
		}
		
		/// <summary>
		/// Prefer cached version info for non-essential operations (menu display)
		/// </summary>
		private VersionInfo GetCachedVersionInfo (FilePath localPath, bool getRemoteStatus)
		{
			VersionInfo status = null;
			if (statusCache.ContainsKey (localPath)) {
				status = statusCache[localPath];
			} else {
				status = GetVersionInfo (localPath, getRemoteStatus);
			}
			return status;
		}

		public override VersionInfo[] GetDirectoryVersionInfo (FilePath localDirectory, bool getRemoteStatus, bool recursive)
		{
			VersionInfo[] versions = Mercurial.GetDirectoryVersionInfo (this, localDirectory.FullPath, getRemoteStatus, recursive);
			if (null != versions) {
				foreach (VersionInfo version in versions) {
					statusCache[version.LocalPath] = version;
				}
			}
			return versions;
		}
		
		// Deprecated
		public override Repository Publish (string serverPath, FilePath localPath, FilePath[] files, string message, IProgressMonitor monitor)
		{
			serverPath = string.Format ("{0}{1}{2}", Url, Url.EndsWith ("/")? string.Empty: "/", serverPath);
			// System.Console.WriteLine ("Got publish {0} {1}", serverPath, localPath);
			Mercurial.StoreCredentials (serverPath);
			Mercurial.Push (serverPath, localPath.FullPath, false, false, false, monitor);
			return new MercurialRepository (Mercurial, serverPath);
		}
		
		public virtual void Push (string pushLocation, FilePath localPath, bool remember, bool overwrite, bool omitHistory, IProgressMonitor monitor) {
			Mercurial.StoreCredentials (pushLocation);
			Mercurial.Push (pushLocation, localPath.FullPath, remember, overwrite, omitHistory, monitor);
		}// Push

		public virtual void Pull (string pullLocation, FilePath localPath, bool remember, bool overwrite, IProgressMonitor monitor) {
			Mercurial.StoreCredentials (pullLocation);
			Mercurial.Pull (pullLocation, localPath.FullPath, remember, overwrite, monitor);
		}// Pull

		public virtual void Rebase (string pullLocation, FilePath localPath, bool remember, bool overwrite, IProgressMonitor monitor) {
			Mercurial.StoreCredentials (pullLocation);
			Mercurial.Rebase (pullLocation, localPath.FullPath, monitor);
		}// Rebase

		public virtual void Merge (string mergeLocation, FilePath localPath, bool remember, bool overwrite, IProgressMonitor monitor) {
			Mercurial.StoreCredentials (mergeLocation);
			Mercurial.Merge (mergeLocation, localPath.FullPath, remember, overwrite, new MercurialRevision (this, MercurialRevision.NONE), new MercurialRevision (this, MercurialRevision.NONE), monitor);
		}// Merge
		
		public override void Update (FilePath[] localPaths, bool recurse, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Mercurial.Update (localPath.FullPath, recurse, monitor);
		}

		public override void Commit (ChangeSet changeSet, IProgressMonitor monitor)
		{
			Mercurial.Commit (changeSet, monitor);
		}

		public override void Checkout (FilePath targetLocalPath, Revision rev, bool recurse, IProgressMonitor monitor)
		{
			Mercurial.StoreCredentials (Url);
			MercurialRevision brev = (null == rev)? new MercurialRevision (this, MercurialRevision.HEAD): (MercurialRevision)rev;
			Mercurial.Checkout (Url, targetLocalPath.FullPath, brev, recurse, monitor);
		}

		public override void Revert (FilePath[] localPaths, bool recurse, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Mercurial.Revert (localPath.FullPath, recurse, monitor, new MercurialRevision (this, MercurialRevision.HEAD));
		}
		
		public override void Add (FilePath[] localPaths, bool recurse, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Mercurial.Add (localPath.FullPath, recurse, monitor);
		}

		public override string GetTextAtRevision (FilePath repositoryPath, Revision revision)
		{
			// System.Console.WriteLine ("Got GetTextAtRevision for {0}", repositoryPath);
			MercurialRevision brev = (null == revision)? new MercurialRevision (this, MercurialRevision.HEAD): (MercurialRevision)revision;
			return Mercurial.GetTextAtRevision (repositoryPath.FullPath, brev);
		}

		public override void RevertToRevision (FilePath localPath, Revision revision, IProgressMonitor monitor)
		{
			if (IsModified (MercurialRepository.GetLocalBasePath (localPath))) {
				MessageDialog md = new MessageDialog (null, DialogFlags.Modal, 
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
			Mercurial.Revert (localPath.FullPath, true, monitor, brev);
		}

		public override void RevertRevision (FilePath localPath, Revision revision, IProgressMonitor monitor)
		{
			if (IsModified (MercurialRepository.GetLocalBasePath (localPath))) {
				MessageDialog md = new MessageDialog (null, DialogFlags.Modal, 
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

			MercurialRevision brev = (MercurialRevision)revision;
			string localPathStr = localPath.FullPath;
			Mercurial.Merge (localPathStr, localPathStr, false, true, brev, (MercurialRevision)(brev.GetPrevious ()), monitor);
		}

		public override bool IsVersioned (FilePath localPath)
		{
			if (string.IsNullOrEmpty (GetLocalBasePath (localPath.FullPath))) {
				return false;
			} 
			
			VersionInfo info = GetCachedVersionInfo (localPath, false);
			return (null != info && info.IsVersioned);
		}

		public override bool IsModified (FilePath localFile)
		{
			if (string.IsNullOrEmpty (GetLocalBasePath (localFile.FullPath))) {
				return false;
			}
			
			VersionInfo info = GetCachedVersionInfo (localFile, false);
			return (null != info && info.IsVersioned && info.HasLocalChanges);
		}

		public virtual bool IsConflicted (FilePath localFile)
		{
			if (string.IsNullOrEmpty (GetLocalBasePath (localFile.FullPath))) {
				return false;
			}
			
			VersionInfo info = GetCachedVersionInfo (localFile, false);
			return (null != info && info.IsVersioned && (0 != (info.Status & VersionStatus.Conflicted)));
		}

		public override bool CanRevert (FilePath localPath)
		{
			return IsModified (localPath) || IsConflicted (localPath);
		}

		public override bool CanCommit (FilePath localPath)
		{
			bool rv = IsModified (localPath);
			return rv;
		}

		public override bool CanAdd (FilePath localPath)
		{
			return !IsVersioned (localPath);
		}

		public virtual bool CanResolve (FilePath localPath)
		{
			return IsConflicted (localPath);
		}

		public virtual bool CanPull (FilePath localPath)
		{
			return Directory.Exists (localPath.FullPath) && base.CanUpdate (localPath);
		}// CanPull

		public virtual bool CanMerge (FilePath localPath)
		{
			return Directory.Exists (localPath.FullPath) && base.CanUpdate (localPath);
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
		
		public override bool CanGetAnnotations (MonoDevelop.Core.FilePath localPath)
		{
		    return IsVersioned (localPath);
		}// CanGetAnnotations
		
		public override bool CanUpdate (FilePath localPath)
		{
			return false;
		}

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
			
			return Mercurial.Diff (baseLocalPath.FullPath, localFiles);
		}// PathDiff

		public override DiffInfo[] PathDiff (FilePath localPath, Revision fromRevision, Revision toRevision)
		{
			return Mercurial.Diff (localPath, (MercurialRevision)fromRevision, (MercurialRevision)toRevision);
		}

		public override void DeleteFiles (FilePath[] localPaths, bool force, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Mercurial.Remove (localPath.FullPath, force, monitor);
		}// DeleteFiles

		public override void DeleteDirectories (FilePath[] localPaths, bool force, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Mercurial.Remove (localPath.FullPath, force, monitor);
		}// DeleteDirectories

		public virtual void Resolve (FilePath[] localPaths, bool recurse, IProgressMonitor monitor)
		{
			foreach (FilePath localPath in localPaths)
				Mercurial.Resolve (localPath.FullPath, recurse, monitor);
		}// Resolve

		public virtual Dictionary<string, BranchType> GetKnownBranches (FilePath localPath)
		{
			return Mercurial.GetKnownBranches (localPath.FullPath);
		}// GetKnownBranches
		
		public virtual void Ignore (FilePath localPath)
		{
			Mercurial.Ignore (localPath.FullPath);
		}// Ignore
		
		public virtual bool IsBound (FilePath localPath)
		{
			return Mercurial.IsBound (localPath.FullPath);
		}// IsBound
		
		public virtual string GetBoundBranch (FilePath localPath)
		{
			return Mercurial.GetBoundBranch (localPath.FullPath);
		}// GetBoundBranch
		
		public virtual void Bind (string branchUrl, FilePath localPath, IProgressMonitor monitor)
		{
			Mercurial.Bind (branchUrl, localPath.FullPath, monitor);
		}// Bind
		
		public virtual void Unbind (FilePath localPath, IProgressMonitor monitor)
		{
			Mercurial.Unbind (localPath.FullPath, monitor);
		}// Unbind
		
		public virtual void Uncommit (FilePath localPath, IProgressMonitor monitor)
		{
			if (IsModified (localPath)) {
				MonoDevelop.Ide.DispatchService.GuiSyncDispatch(delegate{ 
					new MessageDialog (null, DialogFlags.Modal, MessageType.Warning, ButtonsType.Close,
					                   "There are uncommitted changed in the working copy. Aborting...").ShowAll ();
				});
			} else {
				Mercurial.Uncommit (localPath.FullPath, monitor);
			}
		}// Uncommit
		
		public override Annotation[] GetAnnotations (FilePath localPath)
		{
			return Mercurial.GetAnnotations (localPath.FullPath);
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
			Mercurial.Export (localPath.FullPath, exportLocation.FullPath, monitor);
		}// Export
		
		public virtual bool CanRebase ()
		{
			return Mercurial.CanRebase ();
		}// CanRebase
	}// MercurialRepository
}
