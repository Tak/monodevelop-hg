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
using System.Collections.Generic;

using MonoDevelop.Core;

namespace MonoDevelop.VersionControl.Mercurial
{
	public interface IMercurialClient
	{
		/// <summary>
		/// Checks whether Mercurial support is installed
		/// </summary>
		bool CheckInstalled ();

		/// <value>
		/// The installed version of Mercurial
		/// </value>
		string Version{ get; }

		/// <summary>
		/// Lists files in a version-controlled path
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>: The path to list
		/// </param>
		/// <param name="recurse">
		/// A <see cref="System.Boolean"/>: Whether to list recursively
		/// </param>
		/// <param name="kind">
		/// A <see cref="ListKind"/>: The kind of files to list
		/// </param>
		/// <returns>
		/// A <see cref="IList`1"/> of filenames
		/// </returns>
		IList<string> List (string path, bool recurse, ListKind kind);

		/// <summary>
		/// Gets the status of a path at a revision
		/// </summary>
		/// <returns>
		/// A <see cref="IList`1"/>: The list of statuses applying to that path
		/// </returns>
		IList<LocalStatus> Status (string path, MercurialRevision revision);

		/// <summary>
		/// Checks whether a path is versioned
		/// </summary>
		bool IsVersioned (string path);

		/// <summary>
		/// Gets the root url for a path
		/// </summary>
		string GetPathUrl (string path);

		/// <summary>
		/// Updates a local path
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The path to update
		/// </param>
		/// <param name="recurse">
		/// A <see cref="System.Boolean"/>: Whether to update recursively
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void Update (string localPath, bool recurse, IProgressMonitor monitor);

		/// <summary>
		/// Reverts a local path
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The path to revert
		/// </param>
		/// <param name="recurse">
		/// A <see cref="System.Boolean"/>: Whether to revert recursively
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		/// <param name="toRevision">
		/// A <see cref="MercurialRevision"/> to which to revert
		/// </param>
		void Revert (string localPath, bool recurse, IProgressMonitor monitor, MercurialRevision toRevision);

		/// <summary>
		/// Adds a local path
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The path to add
		/// </param>
		/// <param name="recurse">
		/// A <see cref="System.Boolean"/>: Whether to add recursively
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void Add (string localPath, bool recurse, IProgressMonitor monitor);

		/// <summary>
		/// Perform a checkout
		/// </summary>
		/// <param name="url">
		/// A <see cref="System.String"/>: The URI from which to check out
		/// </param>
		/// <param name="targetLocalPath">
		/// A <see cref="System.String"/>: The local path to be used
		/// </param>
		/// <param name="rev">
		/// A <see cref="MercurialRevision"/>: The revision to check out
		/// </param>
		/// <param name="recurse">
		/// A <see cref="System.Boolean"/>: Whether to check out recursively
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void Checkout (string url, string targetLocalPath, MercurialRevision rev, bool recurse, IProgressMonitor monitor);

		/// <summary>
		/// Perform a branching operation
		/// </summary>
		/// <param name="branchLocation">
		/// A <see cref="System.String"/>: The branch location from which to branch
		/// </param>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The location to which to branch
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void Branch (string branchLocation, string localPath, IProgressMonitor monitor);

		/// <summary>
		/// Get a file's text at a given revision
		/// </summary>
		string GetTextAtRevision (string path, MercurialRevision rev);

		/// <summary>
		/// Get the history for a given file
		/// </summary>
		/// <param name="repo">
		/// A <see cref="MercurialRepository"/>: The repo to which the file belongs
		/// </param>
		/// <param name="localFile">
		/// A <see cref="System.String"/>: The filename
		/// </param>
		/// <param name="since">
		/// A <see cref="MercurialRevision"/>: The revision since which to get the file's history
		/// </param>
		/// <returns>
		/// A <see cref="MercurialRevision[]"/>: The revisions which have affected localFile since since
		/// </returns>
		MercurialRevision[] GetHistory (MercurialRepository repo, string localFile, MercurialRevision since);
		
		/// <summary>
		/// Performs a merge
		/// </summary>
		/// <param name="mergeLocation">
		/// A <see cref="System.String"/>: The path from which to merge
		/// </param>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The path to which to merge
		/// </param>
		/// <param name="remember">
		/// A <see cref="System.Boolean"/>: Whether mergeLocation should be remembered
		/// </param>
		/// <param name="overwrite">
		/// A <see cref="System.Boolean"/>: Whether to overwrite uncommitted changes at localPath
		/// </param>
		/// <param name="start">
		/// A <see cref="MercurialRevision"/>: The revision to begin merging
		/// </param>
		/// <param name="end">
		/// A <see cref="MercurialRevision"/>: The revision to stop merging
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to use
		/// </param>
		void Merge (string mergeLocation, string localPath, bool remember, bool overwrite, MercurialRevision start, MercurialRevision end, IProgressMonitor monitor);

		/// <summary>
		/// Performs a push
		/// </summary>
		/// <param name="pushLocation">
		/// A <see cref="System.String"/>: The branch URI to which to push
		/// </param>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The local path to push
		/// </param>
		/// <param name="remember">
		/// A <see cref="System.Boolean"/>: Whether pushLocation should be remembered
		/// </param>
		/// <param name="overwrite">
		/// A <see cref="System.Boolean"/>: Whether to overwrite stale changes at pushLocation
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void Push (string pushLocation, string localPath, bool remember, bool overwrite, IProgressMonitor monitor);

		/// <summary>
		/// Performs a push
		/// </summary>
		/// <param name="pushLocation">
		/// A <see cref="System.String"/>: The branch URI to which to push
		/// </param>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The local path to push
		/// </param>
		/// <param name="remember">
		/// A <see cref="System.Boolean"/>: Whether pushLocation should be remembered
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void DPush (string pushLocation, string localPath, bool remember, MonoDevelop.Core.IProgressMonitor monitor);
		
		/// <summary>
		/// Performs a pull
		/// </summary>
		/// <param name="pullLocation">
		/// A <see cref="System.String"/>: The branch URI to pull
		/// </param>
		/// <param name="LocalPath">
		/// A <see cref="System.String"/>: The local path to which to pull
		/// </param>
		/// <param name="remember">
		/// A <see cref="System.Boolean"/>: Whether to remember this pull location
		/// </param>
		/// <param name="overwrite">
		/// A <see cref="System.Boolean"/>: Whether to overwrite local changes
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void Pull (string pullLocation, string LocalPath, bool remember, bool overwrite, IProgressMonitor monitor);

		/// <summary>
		/// Performs a commit
		/// </summary>
		void Commit (ChangeSet changeSet, IProgressMonitor monitor);

		/// <summary>
		/// Performs a diff
		/// </summary>
		/// <param name="basePath">
		/// A <see cref="System.String"/>: The base path to be diffed
		/// </param>
		/// <param name="files">
		/// A <see cref="System.String"/>: An array of files to be diffed,
		/// if not all
		/// </param>
		/// <returns>
		/// A <see cref="DiffInfo"/>: The differences
		/// </returns>
		DiffInfo[] Diff (string basePath, string[] files);

		/// <summary>
		/// Performs a recursive diff
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>: The path to be diffed
		/// </param>
		/// <param name="fromRevision">
		/// A <see cref="MercurialRevision"/>: The beginning revision
		/// </param>
		/// <param name="toRevision">
		/// A <see cref="MercurialRevision"/>: The ending revision
		/// </param>
		/// <returns>
		/// A <see cref="DiffInfo[]"/>: The differences
		/// </returns>
		DiffInfo[] Diff (string path, MercurialRevision fromRevision, MercurialRevision toRevision);
		
		/// <summary>
		/// Removes a path
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>: The path to be removed
		/// </param>
		/// <param name="force">
		/// A <see cref="System.Boolean"/>: Whether to force the removal
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void Remove (string path, bool force, IProgressMonitor monitor);

		/// <summary>
		/// Resolves a conflicted path
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>: The path to be resolved
		/// </param>
		/// <param name="recurse">
		/// A <see cref="System.Boolean"/>: Whether to recurse
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		void Resolve (string path, bool recurse, IProgressMonitor monitor);

		/// <summary>
		/// Gets a list of the known branches for path
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>: A path to a version-controlled location
		/// </param>
		/// <returns>
		/// A <see cref="Dictionary"/>: Known branch paths and their types
		/// </returns>
		Dictionary<string, BranchType> GetKnownBranches (string path);
		
		/// <summary>
		/// Stores credentials for a given url
		/// </summary>
		/// <param name="url">
		/// A <see cref="System.String"/>: A url of the form: 
		/// transport://[[user[:password]@]host[:port]]/path
		/// </param>
		void StoreCredentials (string url);
		
		/// <summary>
		/// Make a directory into a versioned branch.
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>: The path at which to create the branch
		/// </param>
		void Init (string path);
		
		/// <summary>
		/// Ignore specified file.
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>: The file to ignore
		/// </param>
		void Ignore (string path);
		
		/// <summary>
		/// Determines whether a path's branch is bound.
		/// </summary>
		bool IsBound (string path);
		
		/// <summary>
		/// Gets the branch bound to the current path, 
		/// or the branch that would be bound if the branch were bound.
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>: A path contained in a local branch
		/// </param>
		/// <returns>
		/// A <see cref="System.String"/>: The bound branch url, or empty string if none
		/// </returns>
		string GetBoundBranch (string path);
		
		/// <summary>
		/// Convert a local branch into a checkout of the supplied branch.
		/// </summary>
		/// <param name="branchUrl">
		/// A <see cref="System.String"/>: The url of the branch to bind
		/// </param>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The local branch
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>
		/// </param>
		void Bind (string branchUrl, string localPath, IProgressMonitor monitor);
		
		/// <summary>
		/// Convert a local checkout into a regular branch.
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The local checkout
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>
		/// </param>
		void Unbind (string localPath, IProgressMonitor monitor);
		
		/// <summary>
		/// Remove the last committed revision.
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: A path to a branch from which to uncommit
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>
		/// </param>
		void Uncommit (string localPath, IProgressMonitor monitor);
		
		/// <summary>
		/// Get the origin of each line in a file.
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The local file path
		/// </param>
		/// <returns>
		/// A <see cref="MonoDevelop.VersionControl.Annotation"/> for each line in the file at localPath
		/// </returns>
		Annotation[] GetAnnotations (string localPath);
		
		/// <summary>
		/// Export a (portion of a) local tree.
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The path to be exported.
		/// </param>
		/// <param name="exportPath">
		/// A <see cref="System.String"/>: The output path.
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>
		/// </param>
		void Export (string localPath, string exportPath, IProgressMonitor monitor);
		
		/// <summary>
		/// Determines whether the current working tree has 
		/// a merge pending commit.
		/// </summary>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: A path in the local working tree
		/// </param>
		/// <returns>
		/// A <see cref="System.Boolean"/>: Whether a merge is pending
		/// </returns>
		bool IsMergePending (string localPath);
		
		/// <summary>
		/// Whether the rebase plugin is installed
		/// </summary>
		bool CanRebase ();
	}
}
