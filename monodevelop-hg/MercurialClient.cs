// Copyright (C) 2008 by Levi Bard <taktaktaktaktaktaktaktaktaktak@gmail.com>
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

using MonoDevelop.Core;

namespace MonoDevelop.VersionControl.Mercurial
{
	public abstract class MercurialClient: IMercurialClient, IDisposable
	{
		/// <summary>
		/// Stores descriptions of list kinds
		/// </summary>
		protected static Dictionary<ListKind, string> listKinds = new Dictionary<ListKind, string> {
			{ ListKind.All, string.Empty },
			{ ListKind.Directory, "directory" },
			{ ListKind.File, "file" },
			{ ListKind.Symlink, "symlink" } 
		};// listKinds

		/// <summary>
		/// Indexes branch types
		/// </summary>
		protected static Dictionary<string, BranchType> branchTypes = new Dictionary<string, BranchType> {
			{ "parent", BranchType.Parent },
			{ "push", BranchType.Push },
			{ "submit", BranchType.Submit },
			{ "public", BranchType.Public }
		};// branchTypes
		
		protected static Dictionary<string,ItemStatus> longStatuses = new Dictionary<string, ItemStatus> {
			{ "unknown", ItemStatus.Unversioned },
			{ "unversioned", ItemStatus.Unversioned },
			{ "modified", ItemStatus.Modified },
			{ "added", ItemStatus.Added },
			{ "removed", ItemStatus.Deleted }
		};// longStatuses
		
		#region IMercurialClient implementation 

		public virtual bool CheckInstalled ()
		{
			try { 
				string v = Version; 
				if (!string.IsNullOrEmpty (v)) {
					string[] tokens = v.Split ('.');
					int major = int.Parse (tokens[0]),
					    minor = int.Parse (tokens[1]);
					return (1 <= major && 6 <= minor);
				}
			}
			catch (Exception e) {
				LoggingService.LogWarning ("Error: Mercurial not installed", e);
			}

			return false;
		}// CheckInstalled

		public virtual bool IsVersioned (string path)
		{
			try {
				IEnumerable<LocalStatus> statuses = Status (path, null);
				// System.Console.WriteLine ("IsVersioned: Got back {0} statuses for {1}", statuses.Count, path);
				// if (0 < statuses.Count){ System.Console.WriteLine ("{0} {1}", Path.GetFullPath (path), statuses[0].Filename); }
				if (1 != statuses.Count () || 
				!Path.GetFullPath (path).EndsWith (statuses.First ().Filename)) {
					return true;
				}// versioned directory

				// System.Console.WriteLine ("Status: {0}", statuses[0].Status);
				return statuses.First ().Status != ItemStatus.Unversioned;
			} catch (MercurialClientException mce) {
				LoggingService.LogWarning (string.Format ("Error checking whether {0} is versioned", path), mce);
			}
			
			return false;
		}// IsVersioned

		public abstract string Version{ get; }
		public abstract IList<string> List (string path, bool recurse, ListKind kind);
		public abstract IEnumerable<LocalStatus> Status (string path, MercurialRevision revision);
		// public abstract string GetPathUrl (string path);
		public abstract void Update (string localPath, bool recurse, IProgressMonitor monitor);
		public abstract void Revert (string localPath, bool recurse, IProgressMonitor monitor, MercurialRevision toRevision);
		public abstract void Add (string localPath, bool recurse, IProgressMonitor monitor);
		public abstract void Checkout (string url, string targetLocalPath, MercurialRevision rev, bool recurse, IProgressMonitor monitor);
		public abstract void Branch (string branchLocation, string localPath, IProgressMonitor monitor);
		public abstract string GetTextAtRevision (string path, MercurialRevision rev);
		public abstract MercurialRevision[] GetHistory (MercurialRepository repo, string localFile, MercurialRevision since);
		public abstract void Merge (MercurialRepository repository);
		public abstract void Push (string pushLocation, string localPath, bool remember, bool overwrite, IProgressMonitor monitor);
		public abstract void Pull (string pullLocation, string localPath, bool remember, bool overwrite, IProgressMonitor monitor);
		public abstract void Rebase (string pullLocation, string localPath, IProgressMonitor monitor);
		public abstract void Commit (ChangeSet changeSet, IProgressMonitor monitor);
		public abstract DiffInfo[] Diff (string basePath, string[] files);
		public abstract DiffInfo[] Diff (string path, MercurialRevision fromRevision, MercurialRevision toRevision);
		public abstract void Remove (string path, bool force, IProgressMonitor monitor);
		public abstract void Resolve (string path, bool recurse, IProgressMonitor monitor);
		public abstract Dictionary<string, BranchType> GetKnownBranches (string path);
		public abstract void Init (string path);
		public abstract void Ignore (string path);
		public abstract void Uncommit (string localPath, MonoDevelop.Core.IProgressMonitor monitor);
		public abstract Annotation[] GetAnnotations (string localPath);
		public abstract void Export (string localPath, string exportPath, IProgressMonitor monitor);
		public abstract MercurialRevision[] GetHeads (MercurialRepository repository);
		public abstract MercurialRevision[] GetIncoming (MercurialRepository repository, string remote);
		public abstract MercurialRevision[] GetOutgoing (MercurialRepository repository, string remote);
		public abstract void Move (string sourcePath, string destinationPath, bool forceOverwrite);
		// public abstract RevisionPath[] GetRevisionChanges (MercurialRepository repo, MercurialRevision revision);
		public virtual RevisionPath[] GetRevisionChanges (MercurialRepository repo, MercurialRevision revision)
		{
			List<RevisionPath> paths = new List<RevisionPath> ();
			foreach (LocalStatus status in Status (repo.LocalBasePath, revision)
			         .Where (s => s.Status != ItemStatus.Unchanged && s.Status != ItemStatus.Unversioned)) {
				paths.Add (new RevisionPath (Path.Combine (repo.LocalBasePath, status.Filename), ConvertAction (status.Status), status.Status.ToString ()));
			}
			
			return paths.ToArray ();
		}
		

		#endregion 
		
		public abstract void Dispose ();
		
//		public static string ListKindToString (ListKind kind) {
//			switch (kind) {
//			case ListKind.Directory:
//				return "directory";
//			case ListKind.File:
//				return "file";
//			case ListKind.Symlink:
//				return "symlink";
//			}// switch
//
//			return string.Empty;
//		}// ListKindToString
	
		public static RevisionAction ConvertAction (ItemStatus status) {
			switch (status) {
			case ItemStatus.Added:
				return RevisionAction.Add;
			case ItemStatus.Modified:
				return RevisionAction.Modify;
			case ItemStatus.Deleted:
				return RevisionAction.Delete;
			case ItemStatus.Replaced:
				return RevisionAction.Replace;
			}// switch

			return RevisionAction.Other;
		}// ConvertAction

		public static string GetLocalBasePath (string localPath) {
			if (null == localPath){ return string.Empty; }
			if (Directory.Exists (Path.Combine (localPath, ".hg"))){ return localPath; }

			return GetLocalBasePath (Path.GetDirectoryName (localPath));
		}// GetLocalBasePath

		public static readonly string[] ExportExtensions = {
			".tar",
			".tar.gz",
			".tgz",
			".tar.bz2",
			".tbz2",
			".zip"
		};
		
		/// <summary>
		/// Determines whether a given path is a valid export target.
		/// </summary>
		public virtual bool IsValidExportPath (string path) {
			if (!string.IsNullOrEmpty (path)) {
				if (Directory.Exists (path) && !IsVersioned (path)) {
					return true;
				}// existing, nonversioned directory
				
			    if (0 == Path.GetExtension (path).Length && !File.Exists (path)) {
					return true;
				}// new directory
				
				foreach (string extension in ExportExtensions) {
					if (path.EndsWith (extension, StringComparison.OrdinalIgnoreCase)) {
						return true;
					}
				}// supported archive format
			}
			
			return false;
		}// IsValidExportPath
		
		public abstract bool IsMergePending (string localPath);
		public abstract bool CanRebase ();
		
		/// <summary>
		/// Normalize a local file path (primarily for windows)
		/// </summary>
		internal static string NormalizePath (string path, bool useFullPath=true)
		{
			string normalizedPath = path;
			if (useFullPath)
				normalizedPath = Path.GetFullPath (path);
			
			if (Platform.IsWindows && 
			    !string.IsNullOrEmpty (normalizedPath) &&
			    normalizedPath.Trim ().EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.Ordinal)) {
				normalizedPath = normalizedPath.Trim ().Remove (normalizedPath.Length - 1);
			}// strip trailing backslash
			
			normalizedPath = ResolveSymlink (normalizedPath);

			return normalizedPath;
		}// NormalizePath
		
		/// <summary>
		/// Returns the real path for a path containing a symbolic link
		/// </summary>
		/// <returns>
		/// A resolved path.
		/// </returns>
		/// <param name='path'>
		/// A path that may traverse a symbolic link.
		/// </param>
		/// <exception cref='ArgumentException'>
		/// Is thrown when path is empty.
		/// </exception>
		internal static string ResolveSymlink (string path)
		{
			if (string.IsNullOrEmpty (path))
				throw new ArgumentException ("Empty path not allowed", "path");
				
			if (!(File.Exists (path) || Directory.Exists (path)))
				return path;
				
#if !WINDOWS
			try {
				string[] chunks = path.Split (new[]{Path.DirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);
				string subpath = chunks[0];
				Mono.Unix.UnixSymbolicLinkInfo link;
				
				if (Path.IsPathRooted (path))
					subpath = Path.Combine (Path.GetPathRoot (path), subpath);
				
				for (int i=1; i<chunks.Length; ++i) {
					link = new Mono.Unix.UnixSymbolicLinkInfo (subpath);
					if (link.IsSymbolicLink) {
						subpath = link.GetContents ().FullName;
						--i;
						continue;
					}
					subpath = Path.Combine (subpath, chunks[i]);
				}
				
				link = new Mono.Unix.UnixSymbolicLinkInfo (subpath);
				if (link.IsSymbolicLink)
					subpath = link.GetContents ().FullName;
	//			if (path != subpath)
	//				Console.WriteLine ("Subsituting {0} for {1}", subpath, path);
				path = subpath;
			} catch (Exception ex) {
				LoggingService.LogWarning (string.Format ("Error checking symlink status for {0}", path), ex);
			}
#endif
			
			return path;
		}
	}

	public class LocalStatus {
		public readonly string Revision;
		public readonly string Filename;
		public ItemStatus Status;

		public LocalStatus (string revision, string filename, ItemStatus status) {
			Revision = revision;
			Filename = filename;
			Status = status;
		}// constructor
	}// Status

	/// <summary>
	/// Status for a file/dir/etc
	/// </summary>
	public enum ItemStatus
	{
		Unversioned = '?',
		Unchanged = 'C',
		Added = 'A',
		Conflicted = 'U',
		Deleted = 'R',
		Ignored = 'I',
		Modified = 'M',
		Replaced = 'Y', // FIXME
	}// ItemStatus

	public enum PropertyStatus
	{
		Unchanged = ' ',
		Added = '+',
		Deleted = '-',
		Replaced = 'R',
		Unknown = '?'
	}// PropertyStatus

	public enum LockStatus
	{
		Unlocked = ' ',
		Locked = 'L'
	}// LockStatus

	public enum ListKind
	{
		All,
		File,
		Directory,
		Symlink
	}// ListKind

	/// <summary>
	/// Types of branches
	/// </summary>
	public enum BranchType
	{
		Parent,
		Push,
		Submit,
		Public
	}// BranchType
}
