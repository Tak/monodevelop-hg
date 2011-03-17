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
using System.Threading;
using System.IO;

using Gtk;
using MonoDevelop.Core;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;
using MonoDevelop.Projects;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide.Gui.Components;

namespace MonoDevelop.VersionControl.Mercurial
{
	class MercurialNodeExtension: NodeBuilderExtension
	{
		public override bool CanBuildNode (Type dataType)
		{
			return typeof(ProjectFile).IsAssignableFrom (dataType)
				|| typeof(SystemFile).IsAssignableFrom (dataType)
				|| typeof(ProjectFolder).IsAssignableFrom (dataType)
				|| typeof(IWorkspaceObject).IsAssignableFrom (dataType);
		}
		
		public override Type CommandHandlerType {
			get { return typeof(MercurialCommandHandler); }
		}
	}
	
	/// <summary>
	/// Enumeration of custom commands for Mercurial
	/// </summary>
	public enum MercurialCommands
	{
		Resolve,
		Pull,
		Merge,
		Branch,
		Init,
		Ignore,
		Bind,
		Unbind,
		Uncommit,
		Export,
		Push,
		Rebase,
		Incoming,
		Outgoing
	}

	/// <summary>
	/// Handles custom Mercurial commands
	/// </summary>
	class MercurialCommandHandler: VersionControlCommandHandler
	{
		/// <summary>
		/// Determines whether the selected items can be resolved
		/// </summary>
		[CommandUpdateHandler (MercurialCommands.Resolve)]
		protected void CanResolve (CommandInfo item)
		{
			bool visible = true;

			foreach (VersionControlItem vcitem in GetItems ()) {
				if(!(visible = (vcitem.Repository is MercurialRepository &&
				          ((MercurialRepository)vcitem.Repository).CanResolve (vcitem.Path))))
					break;
			}

			item.Visible = visible;
		}// CanResolve

		/// <summary>
		/// Resolves the selected items
		/// </summary>
		[CommandHandler (MercurialCommands.Resolve)]
		protected void OnResolve()
		{
			List<FilePath> files = null;
			MercurialRepository repo = null;
			
			foreach (VersionControlItemList repolist in GetItems ().SplitByRepository ()) {
				repo = (MercurialRepository)repolist[0].Repository;
				files = new List<FilePath> (repolist.Count);
				foreach (VersionControlItem item in repolist) {
					files.Add (new FilePath (item.Path));
				}
				
				MercurialTask worker = new MercurialTask ();
				worker.Description = string.Format ("Resolving {0}", files[0]);
				worker.Operation = delegate{ repo.Resolve (files.ToArray (), true, worker.ProgressMonitor); };
				worker.Start ();
			}
		}// OnResolve

		/// <summary>
		/// Determines whether a pull can be performed.
		/// </summary>
		[CommandUpdateHandler (MercurialCommands.Pull)]
		protected void CanPull (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				item.Visible = (vcitem.Repository is MercurialRepository &&
				          ((MercurialRepository)vcitem.Repository).CanPull (vcitem.Path));
			} else { item.Visible = false; }
		}// CanPull

		/// <summary>
		/// Performs a pull.
		/// </summary>
		[CommandHandler (MercurialCommands.Pull)]
		protected void OnPull()
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = ((MercurialRepository)vcitem.Repository);
			Dictionary<string, BranchType> branches = repo.GetKnownBranches (vcitem.Path);
			string   defaultBranch = string.Empty,
			         localPath = vcitem.IsDirectory? (string)vcitem.Path.FullPath: Path.GetDirectoryName (vcitem.Path.FullPath);

			foreach (KeyValuePair<string, BranchType> branch in branches) {
				if (BranchType.Parent == branch.Value) {
					defaultBranch = branch.Key;
					break;
				}
			}// check for parent branch

			Dialogs.BranchSelectionDialog bsd = new Dialogs.BranchSelectionDialog (branches.Keys, defaultBranch, localPath, false, true, true, false);
			try {
				if ((int)Gtk.ResponseType.Ok == bsd.Run () && !string.IsNullOrEmpty (bsd.SelectedLocation)) {
					MercurialTask worker = new MercurialTask ();
					worker.Description = string.Format ("Pulling from {0}", bsd.SelectedLocation);
					worker.Operation = delegate{ repo.Pull (bsd.SelectedLocation, vcitem.Path, bsd.SaveDefault, bsd.Overwrite, worker.ProgressMonitor); };
					worker.Start ();
				}
			} finally {
				bsd.Destroy ();
			}
		}// OnPull

		/// <summary>
		/// Determines whether a rebase can be performed.
		/// </summary>
		[CommandUpdateHandler (MercurialCommands.Rebase)]
		protected void CanRebase (CommandInfo item)
		{
			CanPull (item);
		}// CanRebase

		/// <summary>
		/// Performs a pull.
		/// </summary>
		[CommandHandler (MercurialCommands.Rebase)]
		protected void OnRebase()
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = ((MercurialRepository)vcitem.Repository);
			Dictionary<string, BranchType> branches = repo.GetKnownBranches (vcitem.Path);
			string   defaultBranch = string.Empty,
			         localPath = vcitem.IsDirectory? (string)vcitem.Path.FullPath: Path.GetDirectoryName (vcitem.Path.FullPath);

			foreach (KeyValuePair<string, BranchType> branch in branches) {
				if (BranchType.Parent == branch.Value) {
					defaultBranch = branch.Key;
					break;
				}
			}// check for parent branch

			Dialogs.BranchSelectionDialog bsd = new Dialogs.BranchSelectionDialog (branches.Keys, defaultBranch, localPath, false, true, true, false);
			try {
				if ((int)Gtk.ResponseType.Ok == bsd.Run () && !string.IsNullOrEmpty (bsd.SelectedLocation)) {
					MercurialTask worker = new MercurialTask ();
					worker.Description = string.Format ("Rebasing on {0}", bsd.SelectedLocation);
					worker.Operation = delegate{ repo.Rebase (bsd.SelectedLocation, vcitem.Path, bsd.SaveDefault, bsd.Overwrite, worker.ProgressMonitor); };
					worker.Start ();
				}
			} finally {
				bsd.Destroy ();
			}
		}// OnRebase
		
		/// <summary>
		/// Determines whether a merge can be performed.
		/// </summary>
		[CommandUpdateHandler (MercurialCommands.Merge)]
		protected void CanMerge (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				item.Visible = (vcitem.Repository is MercurialRepository &&
				          ((MercurialRepository)vcitem.Repository).CanMerge (vcitem.Path));
			} else { item.Visible = false; }
		}// CanMerge

		/// <summary>
		/// Performs a merge.
		/// </summary>
		[CommandHandler (MercurialCommands.Merge)]
		protected void OnMerge()
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = ((MercurialRepository)vcitem.Repository);
			repo.Merge ();
		}// OnMerge
		
		/// <summary>
		/// Determines whether a new repository can be created for the selected item
		/// </summary>
		[CommandUpdateHandler (MercurialCommands.Init)]
		protected void CanInit (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				if (vcitem.WorkspaceObject is Solution && null == vcitem.Repository) {
					item.Visible = true;
					return;
				}
			} 
			item.Visible = false;
		}// CanInit

		/// <summary>
		/// Initializes a new repository and adds the current solution.
		/// </summary>
		[CommandHandler (MercurialCommands.Init)]
		protected void OnInit()
		{
			MercurialVersionControl bvc = null;
			MercurialRepository repo = null;
			VersionControlItem vcitem = GetItems ()[0];
			string path = vcitem.Path;
			List<FilePath> addFiles = null;
			Solution solution = (Solution)vcitem.WorkspaceObject;
			
			foreach (VersionControlSystem vcs in VersionControlService.GetVersionControlSystems ())
				if (vcs is MercurialVersionControl)
					bvc = (MercurialVersionControl)vcs;

			if (null == bvc || !bvc.IsInstalled)
				throw new Exception ("Can't use bazaar");

			bvc.Init (path);
			
			repo = new MercurialRepository (bvc, string.Format("file://{0}", path));
			addFiles = GetAllFiles (solution);
			
			repo.Add (addFiles.ToArray (), false, null);
			solution.NeedsReload = true;
		}// OnInit
		
		/// <summary>
		/// Returns a list of all files relevant to the solution
		/// </summary>
		private static List<FilePath> GetAllFiles (Solution s) {
			List<FilePath> files = new List<FilePath> ();
			
			files.Add (s.FileName);
			foreach (Solution child in s.GetAllSolutions ()) {
				if (s != child)
					files.AddRange (GetAllFiles (child));
			}// recurse!
			foreach (Project project in s.GetAllProjects ()) {
				files.Add (project.FileName);
				foreach (ProjectFile pfile in project.Files) {
					files.Add (pfile.FilePath);
				}// add project file
			}// add project files
			
			return files;
		}// GetAllFiles
		
		/// <summary>
		/// Determines whether a file can be ignored
		/// </summary>
		[CommandUpdateHandler (MercurialCommands.Ignore)]
		protected void CanIgnore (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				if (vcitem.Repository is MercurialRepository) {
					item.Visible = !((MercurialRepository)vcitem.Repository).IsVersioned (vcitem.Path);
					return;
				}
			} 
			item.Visible = false;
		}// CanIgnore

		/// <summary>
		/// Ignores a file
		/// </summary>
		[CommandHandler (MercurialCommands.Ignore)]
		protected void OnIgnore()
		{
			VersionControlItem vcitem = GetItems ()[0];
			((MercurialRepository)vcitem.Repository).Ignore (vcitem.Path);
		}// OnIgnore
		
		[CommandUpdateHandler (MercurialCommands.Bind)]
		protected void CanBind (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				if (vcitem.Repository is MercurialRepository) {
					item.Visible = ((MercurialRepository)vcitem.Repository).CanBind (vcitem.Path);
					return;
				}
			} 
			item.Visible = false;
		}// CanBind

		/// <summary>
		/// Binds a file
		/// </summary>
		[CommandHandler (MercurialCommands.Bind)]
		protected void OnBind()
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = (MercurialRepository)vcitem.Repository;
			string boundBranch = repo.GetBoundBranch (vcitem.Path);
			
			Dialogs.BranchSelectionDialog bsd = new Dialogs.BranchSelectionDialog (new string[]{boundBranch}, boundBranch, vcitem.Path.FullPath, false, false, false, false);
			try {
				if ((int)Gtk.ResponseType.Ok == bsd.Run () && !string.IsNullOrEmpty (bsd.SelectedLocation)) {
					MercurialTask worker = new MercurialTask ();
					worker.Description = string.Format ("Binding to {0}", bsd.SelectedLocation);
					worker.Operation = delegate{ repo.Bind (bsd.SelectedLocation, vcitem.Path, worker.ProgressMonitor); };
					worker.Start ();
				}
			} finally {
				bsd.Destroy ();
			}
		}// OnBind
		
		
		[CommandUpdateHandler (MercurialCommands.Unbind)]
		protected void CanUnbind (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				if (vcitem.Repository is MercurialRepository) {
					item.Visible = ((MercurialRepository)vcitem.Repository).CanUnbind (vcitem.Path);
					return;
				}
			} 
			item.Visible = false;
		}// CanUnbind

		/// <summary>
		/// Unbinds a file
		/// </summary>
		[CommandHandler (MercurialCommands.Unbind)]
		protected void OnUnbind()
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = (MercurialRepository)vcitem.Repository;
			
			MercurialTask worker = new MercurialTask ();
			worker.Description = string.Format ("Unbinding {0}", vcitem.Path);
			worker.Operation = delegate{ repo.Unbind (vcitem.Path, worker.ProgressMonitor); };
			worker.Start ();
		}// OnUnbind
		
		
		[CommandUpdateHandler (MercurialCommands.Uncommit)]
		protected void CanUncommit (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				if (vcitem.Repository is MercurialRepository) {
					item.Visible = ((MercurialRepository)vcitem.Repository).CanUncommit (vcitem.Path);
					return;
				}
			} 
			item.Visible = false;
		}// CanUncommit

		/// <summary>
		/// Removes the last committed revision from the current branch.
		/// </summary>
		[CommandHandler (MercurialCommands.Uncommit)]
		protected void OnUncommit()
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = (MercurialRepository)vcitem.Repository;
			
			MercurialTask worker = new MercurialTask ();
			worker.Description = string.Format ("Uncommitting {0}", vcitem.Path);
			worker.Operation = delegate{ repo.Uncommit (vcitem.Path, worker.ProgressMonitor); };
			worker.Start ();
		}// OnUncommit
		
		[CommandHandler (Commands.Publish)]
		[CommandHandler (MercurialCommands.Push)]
		protected void OnMercurialPublish() 
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = ((MercurialRepository)vcitem.Repository);
			Dictionary<string, BranchType> branches = repo.GetKnownBranches (vcitem.Path);
			string   defaultBranch = string.Empty,
			         localPath = vcitem.IsDirectory? (string)vcitem.Path.FullPath: Path.GetDirectoryName (vcitem.Path.FullPath);
			         
			if (repo.IsModified (MercurialRepository.GetLocalBasePath (vcitem.Path.FullPath))) {
				MessageDialog md = new MessageDialog (null, DialogFlags.Modal, 
				                                      MessageType.Question, ButtonsType.YesNo, 
				                                      GettextCatalog.GetString ("You have uncommitted local changes. Push anyway?"));
				try {
					if ((int)ResponseType.Yes != md.Run ()) {
						return;
					}
				} finally {
					md.Destroy ();
				}
			}// warn about uncommitted changes

			foreach (KeyValuePair<string, BranchType> branch in branches) {
				if (BranchType.Parent == branch.Value) {
					defaultBranch = branch.Key;
					break;
				}
			}// check for parent branch

			Dialogs.BranchSelectionDialog bsd = new Dialogs.BranchSelectionDialog (branches.Keys, defaultBranch, localPath, false, true, true, true);
			try {
				if ((int)Gtk.ResponseType.Ok == bsd.Run () && !string.IsNullOrEmpty (bsd.SelectedLocation)) {
					MercurialTask worker = new MercurialTask ();
					worker.Description = string.Format ("Pushing to {0}", bsd.SelectedLocation);
					worker.Operation = delegate{ repo.Push (bsd.SelectedLocation, vcitem.Path, bsd.SaveDefault, bsd.Overwrite, bsd.OmitHistory, worker.ProgressMonitor); };
					worker.Start ();
				}
			} finally {
				bsd.Destroy ();
			}
		}// OnPublish
		
		[CommandUpdateHandler (Commands.Publish)]
		[CommandUpdateHandler (MercurialCommands.Push)]
		protected void UpdateMercurialPublish(CommandInfo item) {
			// System.Console.WriteLine ("Updating mercurial publish");
			CanPull (item);
		}// UpdatePublish
		
		[CommandHandler (MercurialCommands.Export)]
		protected void OnExport() 
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = ((MercurialRepository)vcitem.Repository);

			FileChooserDialog fsd = new FileChooserDialog (GettextCatalog.GetString ("Choose export location"), 
			                                               null, FileChooserAction.Save, "Cancel", ResponseType.Cancel, 
			                                               "Save", ResponseType.Accept);
			fsd.SetCurrentFolder (vcitem.Path.FullPath.ParentDirectory);
			
			try {
				if ((int)Gtk.ResponseType.Accept == fsd.Run () && !string.IsNullOrEmpty (fsd.Filename)) {
					MercurialTask worker = new MercurialTask ();
					worker.Description = string.Format ("Exporting to {0}", fsd.Filename);
					worker.Operation = delegate{ repo.Export (vcitem.Path, fsd.Filename, worker.ProgressMonitor); };
					worker.Start ();
				}
			} finally {
				fsd.Destroy ();
			}
		}// OnExport
		
		[CommandUpdateHandler (MercurialCommands.Export)]
		protected void UpdateExport(CommandInfo item) {
			CanPull (item);
		}// UpdateExport
		
		/// <summary>
		/// Determines whether incoming changesets can be checked
		/// </summary>
		[CommandUpdateHandler (MercurialCommands.Incoming)]
		protected void CanGetIncoming (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				item.Visible = (vcitem.Repository is MercurialRepository);
			} else { item.Visible = false; }
		}// CanGetIncoming

		/// <summary>
		/// Lists incoming changesets
		/// </summary>
		[CommandHandler (MercurialCommands.Incoming)]
		protected void OnGetIncoming()
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = ((MercurialRepository)vcitem.Repository);
			Dictionary<string, BranchType> branches = repo.GetKnownBranches (vcitem.Path);
			string   defaultBranch = string.Empty,
			         localPath = vcitem.IsDirectory? (string)vcitem.Path.FullPath: Path.GetDirectoryName (vcitem.Path.FullPath);

			foreach (KeyValuePair<string, BranchType> branch in branches) {
				if (BranchType.Parent == branch.Value) {
					defaultBranch = branch.Key;
					break;
				}
			}// check for parent branch

			Dialogs.BranchSelectionDialog bsd = new Dialogs.BranchSelectionDialog (branches.Keys, defaultBranch, localPath, false, false, false, false);
			try {
				if ((int)Gtk.ResponseType.Ok == bsd.Run ()) {
					MercurialTask worker = new MercurialTask ();
					worker.Description = string.Format ("Incoming from {0}", bsd.SelectedLocation);
					worker.Operation = delegate {
						repo.LocalBasePath = MercurialRepository.GetLocalBasePath (localPath);
						Revision[] history = repo.GetIncoming (bsd.SelectedLocation);
						DispatchService.GuiDispatch (() => {
							var view = new MonoDevelop.VersionControl.Views.LogView (localPath, true, history, repo);
							IdeApp.Workbench.OpenDocument (view, true);
						});
					};
					worker.Start ();
				}
			} finally {
				bsd.Destroy ();
			}
			
		}// OnGetIncoming

		/// <summary>
		/// Determines whether outgoing changesets can be checked.
		/// </summary>
		[CommandUpdateHandler (MercurialCommands.Outgoing)]
		protected void CanGetOutgoing (CommandInfo item)
		{
			if (1 == GetItems ().Count) {
				VersionControlItem vcitem = GetItems ()[0];
				item.Visible = (vcitem.Repository is MercurialRepository);
			} else { item.Visible = false; }
		}// CanGetOutgoing

		/// <summary>
		/// Lists outgoing changesets.
		/// </summary>
		[CommandHandler (MercurialCommands.Outgoing)]
		protected void OnGetOutgoing()
		{
			VersionControlItem vcitem = GetItems ()[0];
			MercurialRepository repo = ((MercurialRepository)vcitem.Repository);
			Dictionary<string, BranchType> branches = repo.GetKnownBranches (vcitem.Path);
			string   defaultBranch = string.Empty,
			         localPath = vcitem.IsDirectory? (string)vcitem.Path.FullPath: Path.GetDirectoryName (vcitem.Path.FullPath);

			foreach (KeyValuePair<string, BranchType> branch in branches) {
				if (BranchType.Parent == branch.Value) {
					defaultBranch = branch.Key;
					break;
				}
			}// check for parent branch

			Dialogs.BranchSelectionDialog bsd = new Dialogs.BranchSelectionDialog (branches.Keys, defaultBranch, localPath, false, false, false, false);
			try {
				if ((int)Gtk.ResponseType.Ok == bsd.Run ()) {
					MercurialTask worker = new MercurialTask ();
					worker.Description = string.Format ("Outgoing to {0}", bsd.SelectedLocation);
					worker.Operation = delegate {
						repo.LocalBasePath = MercurialRepository.GetLocalBasePath (localPath);
						Revision[] history = repo.GetOutgoing (bsd.SelectedLocation);
						DispatchService.GuiDispatch (() => {
							var view = new MonoDevelop.VersionControl.Views.LogView (localPath, true, history, repo);
							IdeApp.Workbench.OpenDocument (view, true);
						});
					};
					worker.Start ();
				}
			} finally {
				bsd.Destroy ();
			}
		}// OnGetOutgoing

	}// MercurialCommandHandler

	/// <summary>
	/// Command handler for Branch command
	/// </summary>
	internal class BranchCommand : CommandHandler
	{
		protected override void Update (CommandInfo info)
		{
			MercurialVersionControl bvc = null;
			
			foreach (VersionControlSystem vcs in VersionControlService.GetVersionControlSystems ())
				if (vcs is MercurialVersionControl)
					bvc = (MercurialVersionControl)vcs;

			info.Visible = (null != bvc && bvc.IsInstalled);
		}

		protected override void Run()
		{
			Dialogs.BranchSelectionDialog bsd = new Dialogs.BranchSelectionDialog (new List<string>(), string.Empty, Environment.GetFolderPath (Environment.SpecialFolder.Personal), true, false, false, false);
			try {
				if ((int)Gtk.ResponseType.Ok == bsd.Run () && !string.IsNullOrEmpty (bsd.SelectedLocation)) {
					string branchLocation = bsd.SelectedLocation,
					       branchName = GetLastChunk (branchLocation),
					       localPath = Path.Combine (bsd.LocalPath, (string.Empty == branchName)? "branch": branchName);
					MercurialTask worker = new MercurialTask ();
					worker.Description = string.Format ("Branching from {0} to {1}", branchLocation, localPath);
					worker.Operation = delegate{ DoBranch (branchLocation, localPath, worker.ProgressMonitor); };
					worker.Start ();
				}
			} finally {
				bsd.Destroy ();
			}
		}

		delegate bool ProjectCheck (string path);

		/// <summary>
		/// Performs a bzr branch
		/// </summary>
		/// <param name="location">
		/// A <see cref="System.String"/>: The from location
		/// </param>
		/// <param name="localPath">
		/// A <see cref="System.String"/>: The to location
		/// </param>
		/// <param name="monitor">
		/// A <see cref="IProgressMonitor"/>: The progress monitor to be used
		/// </param>
		private static void DoBranch (string location, string localPath, IProgressMonitor monitor)
		{
			MercurialVersionControl bvc = null;
			
			foreach (VersionControlSystem vcs in VersionControlService.GetVersionControlSystems ())
				if (vcs is MercurialVersionControl)
					bvc = (MercurialVersionControl)vcs;

			if (null == bvc || !bvc.IsInstalled)
				throw new Exception ("Mercurial is not installed");

			// Branch
			bvc.Branch (location, localPath, monitor);

			// Search for solution/project file in local branch;
			// open if found
			string[] list = System.IO.Directory.GetFiles(localPath);

			ProjectCheck[] checks = {
				delegate (string path) { return path.EndsWith (".mds"); },
				delegate (string path) { return path.EndsWith (".mdp"); },
				MonoDevelop.Projects.Services.ProjectService.IsWorkspaceItemFile
			};

			foreach (ProjectCheck check in checks) {
				foreach (string file in list) {
					if (check (file)) {
						Gtk.Application.Invoke (delegate (object o, EventArgs ea) {
							IdeApp.Workspace.OpenWorkspaceItem (file);
						});
						return;
					}// found a project file
				}// on each file
			}// run check
		}// DoBranch

		/// <summary>
		/// Gets the last chunk of a branch location,
		/// e.g., GetLastChunk("lp:~taktaktaktaktaktaktaktaktaktak/monodevelop-bzr/tak/") => "tak"
		/// </summary>
		/// <param name="branchLocation">
		/// A <see cref="System.String"/>: The branch location to chunk
		/// </param>
		/// <returns>
		/// A <see cref="System.String"/>: The last nonempty chunk of branchLocation
		/// </returns>
		private static string GetLastChunk (string branchLocation) {
			string[] chunks = null,
			         separators = { "/", Path.DirectorySeparatorChar.ToString () };
			string chunk = string.Empty;

			foreach (string separator in separators) {
				if (branchLocation.Contains (separator)) {
					chunks = branchLocation.Split ('/');
					for (int i = chunks.Length-1; i>=0; --i) {
						if (string.Empty != (chunk = chunks[i].Trim ()))
							return chunk;
					}// accept last non-empty chunk
				}
			}// check each separation scheme

			return string.Empty;
		}// GetLastChunk
	}// BranchCommand
	
	public delegate void MercurialOperation ();

	/// <summary>
	/// A general run-once task for Mercurial operations
	/// </summary>
	public class MercurialTask
	{
		public string Description{ get; set; }
		public MercurialOperation Operation{ get; set; }
		public IProgressMonitor ProgressMonitor{ get; protected set; }

		public MercurialTask (): this (string.Empty, null) {}
		
		public MercurialTask (string description, MercurialOperation operation)
		{
			ProgressMonitor = IdeApp.Workbench.ProgressMonitors.GetOutputProgressMonitor("Version Control", null, true, true);
			Description = description;
			Operation = operation;
		}// constructor
		
		public void Start ()
		{
			ThreadPool.QueueUserWorkItem (delegate {
				try {
					ProgressMonitor.BeginTask (Description, 0);
					Operation ();
					ProgressMonitor.ReportSuccess (GettextCatalog.GetString ("Done."));
				} catch (Exception e) {
					ProgressMonitor.ReportError (e.Message, e);
				} finally {
					ProgressMonitor.EndTask ();
					ProgressMonitor.Dispose ();
				}
			});
		}// Start
	}
}
