// 
//  MercurialCommandClient.cs
//  
//  Author:
//       Levi Bard <levi@unity3d.com>
// 
//  Copyright (c) 2011 Levi Bard
// 
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//  
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
// 
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using Mercurial;

using MonoDevelop.Core;

namespace MonoDevelop.VersionControl.Mercurial
{
	public class MercurialCommandClient: MercurialClient
	{
		CommandClient client;
		
		// Requires mercurial >= 1.9
		public override bool CheckInstalled ()
		{
			try { 
				string v = Version; 
				if (!string.IsNullOrEmpty (v)) {
					return CheckVersion (v);
				}
			} catch (Exception e) {
				LoggingService.LogWarning ("Error: Mercurial not installed", e);
			}

			return false;
		}// CheckInstalled
		
		static bool CheckVersion (string version) {
			if (string.IsNullOrEmpty (version))
				throw new ArgumentException ("version cannot be empty", "version");
			string[] tokens = version.Split ('.');
			int major = int.Parse (tokens [0]),
			    minor = int.Parse (tokens [1]);
			return (1 < major || (1 == major || 9 <= minor));
		}
		
		public static bool IsInstalled {
			get {
				string path = Path.Combine (Path.GetTempPath (), DateTime.UtcNow.Ticks.ToString ());
				try {
					try {
						CommandClient.Initialize (path, null);
						using (var client = new CommandClient (path, null, null, null)) {
							return CheckVersion (client.Version);
						}
					} finally {
						try {
							Directory.Delete (path, true);
						} catch {}
					}
				} catch {
				}
				return false;
			}
		}
		
		public readonly string MercurialPath;
		public static readonly string DefaultMercurialPath = "hg";
		
		public MercurialCommandClient (string repositoryPath, string mercurialPath)
		{
			MercurialPath = mercurialPath;
			client = new CommandClient (repositoryPath, null, null, MercurialPath);
		}

		public override void Dispose ()
		{
			if (client != null)
				client.Dispose ();
			client = null;
		}
		
		#region implemented abstract members of MonoDevelop.VersionControl.Mercurial.MercurialClient
		
		public override string Version {
			get {
				return client.Version;
			}
		}

		// FIXME: Is this being used for anything?
		public override System.Collections.Generic.IList<string> List (string path, bool recurse, ListKind kind)
		{
			throw new NotImplementedException ("Is List being used for anything?");
		}

		public override System.Collections.Generic.IEnumerable<LocalStatus> Status (string path, MercurialRevision revision)
		{
			string normalizedPath = NormalizePath (path);
			string rootRelativePath = ((FilePath)normalizedPath).ToRelative (client.Root);
			string revString = null;
			if (null != revision && MercurialRevision.HEAD != revision.Rev && MercurialRevision.NONE != revision.Rev)
				revString = revision.Rev;
				
			IDictionary<string,global::Mercurial.Status> statuses = client.Status (new[]{normalizedPath}, onlyRevision: revString);
			if (!statuses.ContainsKey (path)) {
				if (statuses.ContainsKey (rootRelativePath)) {
					statuses [path] = statuses [rootRelativePath];
					statuses.Remove (rootRelativePath);
				} else if (statuses.ContainsKey (normalizedPath)) {
					statuses [path] = statuses [normalizedPath];
					statuses.Remove (normalizedPath);
				} else {
					statuses [path] = global::Mercurial.Status.Clean;
				}
			}
			
			// Convert relative paths to absolute
			return statuses.Select (pair => new LocalStatus (MercurialRevision.NONE,
				Path.IsPathRooted (pair.Key)? pair.Key: (string)((FilePath)Path.Combine (client.Root, pair.Key)),
				ConvertCommandStatus (pair.Value)));
		}
		
		static ItemStatus ConvertCommandStatus (global::Mercurial.Status status)
		{
			if (Enum.GetValues (typeof(ItemStatus)).Cast <ItemStatus> ().Any (x => ((char)x) == ((char)status)))
				return (ItemStatus)((char)status);
			return ItemStatus.Unversioned;
		}

		public override void Update (string localPath, bool recurse, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				client.Update (null);
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			monitor.ReportSuccess (string.Empty);
		}

		public override void Revert (string localPath, bool recurse, MonoDevelop.Core.IProgressMonitor monitor, MercurialRevision toRevision)
		{
			try {
				client.Revert (null, NormalizePath (localPath));
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}

		public override void Add (string localPath, bool recurse, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				client.Add (NormalizePath (localPath));
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}

		public override void Checkout (string url, string targetLocalPath, MercurialRevision rev, bool recurse, MonoDevelop.Core.IProgressMonitor monitor)
		{
			// Remap to clone? or stop caring?
			throw new NotImplementedException ();
		}

		public override void Branch (string branchLocation, string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				CommandClient.Clone (source: branchLocation, destination: localPath, mercurialPath: MercurialPath);
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}
		
		internal static void Clone (string branchLocation, string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				CommandClient.Clone (source: branchLocation, destination: localPath, mercurialPath: DefaultMercurialPath);
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}

		public override string GetTextAtRevision (string path, MercurialRevision rev)
		{
			path = NormalizePath (path);
			string revisionText = null;
			if (null != rev && MercurialRevision.NONE != rev.Rev && MercurialRevision.HEAD != rev.Rev)
				revisionText = rev.Rev;
			IDictionary<string,string > text = client.Cat (revisionText, path);
			if (null == text || !text.ContainsKey (path))
				return string.Empty;
			return text [path];
		}

		public override MercurialRevision[] GetHistory (MercurialRepository repo, string localFile, MercurialRevision since)
		{
			string revisions = null;
			if (since != null && since.Rev != MercurialRevision.NONE)
				revisions = since.Rev;
			return client.Log (revisions, NormalizePath (localFile)).Select (r => FromCommandRevision (repo, r)).ToArray ();
		}

		public override void Merge (MercurialRepository repository)
		{
			client.Merge (null);
		}

		public override void Push (string pushLocation, string localPath, bool remember, bool overwrite, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				client.Push (pushLocation, force: overwrite, allowNewBranch: overwrite);
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}

		public override void Pull (string pullLocation, string localPath, bool remember, bool overwrite, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				client.Pull (pullLocation, update: true, force: overwrite);
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			monitor.ReportSuccess (string.Empty);
		}

		public override void Rebase (string pullLocation, string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			// Implement hg rebase
			throw new NotImplementedException ();
		}

		public override void Commit (ChangeSet changeSet, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				client.Commit (changeSet.GlobalComment, changeSet.Items.Select (i => Path.Combine (changeSet.BaseLocalPath, i.LocalPath)).ToArray ());
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}

		public override DiffInfo[] Diff (string basePath, string[] files)
		{
			basePath = NormalizePath (basePath);
			
			if (null == files || 0 == files.Length) {
				if (Directory.Exists (basePath)) {
					IEnumerable<LocalStatus > statuses = Status (basePath, new MercurialRevision (null, MercurialRevision.HEAD));
					List<string > foundFiles = new List<string> ();
					foreach (LocalStatus status in statuses) {
						if (ItemStatus.Unchanged != status.Status && ItemStatus.Unversioned != status.Status) {
							foundFiles.Add (status.Filename);
						}
					}
					files = foundFiles.ToArray ();
				} else {
					files = new string[]{ basePath };
				}
			}

			return files.Select (f => new DiffInfo (basePath, f, client.Diff (null, Path.Combine (basePath, f)))).ToArray ();
		}

		public override DiffInfo[] Diff (string path, MercurialRevision fromRevision, MercurialRevision toRevision)
		{
			return new[]{ new DiffInfo (client.Root, NormalizePath (path).Substring (client.Root.Length), client.Diff (string.Format ("{0}:{1}", fromRevision, toRevision), NormalizePath (path))) };
		}

		public override void Remove (string path, bool force, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				client.Remove (new[]{path}, force: force);
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}

		public override void Resolve (string path, bool recurse, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				client.Resolve (new[]{path}, mark: true);
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}

		public override Dictionary<string, BranchType> GetKnownBranches (string path)
		{
			try {
				return client.Paths ().Aggregate (new Dictionary<string, BranchType> (), (dict, pair) => {
					dict[pair.Value] = BranchType.Parent;
					return dict;
				});
			} catch (CommandException ce) {
				LoggingService.LogWarning ("Error getting known branches", ce);
			}
			return new Dictionary<string, BranchType> ();
		}

		public override void Init (string path)
		{
			CommandClient.Initialize (NormalizePath (path), MercurialPath);
		}
		
		internal static void InitStatic (string path)
		{
			CommandClient.Initialize (NormalizePath (path), null);
		}

		public override void Ignore (string path)
		{
			// Implement .hgignore population
			throw new NotImplementedException ();
		}

		public override void Uncommit (string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				if (!client.Rollback ()) {
					monitor.ReportWarning ("Rollback failed");
				}
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
			
			monitor.ReportSuccess (string.Empty);
		}

		public override Annotation[] GetAnnotations (string localPath)
		{
			var lines = client.Annotate (null, new[]{NormalizePath (localPath)}, followCopies: true, showAuthor: true, showDate: true, showRevision: true, showChangeset: false, showLine: false, shortDate: true).Split ('\n');
			var annotations = new List<Annotation> ();
			char[] separators = new char[]{ ' ', '\t', ':' };
			Annotation previous = null;
			
			foreach (string line in lines) {
				string[] fields = line.Split (separators, StringSplitOptions.RemoveEmptyEntries);
				if (2 <= fields.Length && !char.IsWhiteSpace (fields [0] [0]) && '|' != fields [0] [0])
					previous = new Annotation (fields [1], fields [0], DateTime.ParseExact (fields [2], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
				annotations.Add (previous);
			}
			
			return annotations.ToArray ();
		}

		public override void Export (string localPath, string exportPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			try {
				client.Archive (NormalizePath (exportPath));
				monitor.ReportSuccess (string.Empty);
			} catch (CommandException ce) {
				monitor.ReportError (ce.Message, ce);
			}
		}

		public override MercurialRevision[] GetHeads (MercurialRepository repository)
		{
			return client.Heads ().Select (r => FromCommandRevision (repository, r)).ToArray ();
		}

		public override MercurialRevision[] GetIncoming (MercurialRepository repository, string remote)
		{
			return client.Incoming (remote, null).Select (r => FromCommandRevision (repository, r)).ToArray ();
		}

		public override MercurialRevision[] GetOutgoing (MercurialRepository repository, string remote)
		{
			return client.Outgoing (remote, null).Select (r => FromCommandRevision (repository, r)).ToArray ();
		}

		public override void Move (string sourcePath, string destinationPath, bool forceOverwrite)
		{
			client.Rename (NormalizePath (sourcePath), NormalizePath (destinationPath), forceOverwrite);
		}
		
		public override bool IsMergePending (string localPath)
		{
			return (1 < client.Parents (null, null).Count ());
		}

		public override bool CanRebase ()
		{
			return false;
		}
		
		#endregion
		
		internal static MercurialRevision FromCommandRevision (MercurialRepository repo, global::Mercurial.Revision revision)
		{
			return new MercurialRevision (repo, revision.RevisionId, revision.Date, revision.Author, revision.Message, null);
		}
	}
}

