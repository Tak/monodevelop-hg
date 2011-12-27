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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using Gtk;
using MonoDevelop.Core;
using MonoDevelop.VersionControl;

namespace MonoDevelop.VersionControl.Mercurial
{
	public class MercurialVersionControl : VersionControlSystem
	{
		/// <value>
		/// Protocols supported by this addin
		/// </value>
		public static readonly string[] protocols = {
			"http", "https", "ssh", "file"
		};

		public override string Name {
			get { return "Mercurial"; }
		}// Name
		
		public override bool IsInstalled {
			get {
				if (!installChecked) {
					// TODO: Provide better abstraction
					installed = MercurialCommandClient.IsInstalled;
					installChecked = true;
				}
				return installed;
			}
		}// IsInstalled
		private static bool installed;
		private static bool installChecked;
		
		public override IRepositoryEditor CreateRepositoryEditor (Repository repo)
		{
			return IsInstalled ?
				new UrlBasedRepositoryEditor ((MercurialRepository)repo):
				null;
		}// CreateRepositoryEditor

		protected override Repository OnCreateRepositoryInstance ()
		{
			return new MercurialRepository ();
		}// OnCreateRepositoryInstance

		/// <summary>
		/// Initialize a new Mercurial repo
		/// </summary>
		/// <param name="newRepoPath">
		/// A <see cref="System.String"/>: The path at which to initialize a new repo
		/// </param>
		public void Init (string newRepoPath)
		{
			MercurialCommandClient.InitStatic (newRepoPath);
		}// Init
		
		public void Branch (string branchLocation, string localPath, IProgressMonitor monitor)
		{
			MercurialCommandClient.Clone (branchLocation, localPath, monitor);
		}// Branch
		
		public override Repository GetRepositoryReference (FilePath path, string id)
		{
			// System.Console.WriteLine ("Requested repository reference for {0}", path);
			try {
				string url = MercurialRepository.GetLocalBasePath (path.FullPath);
				if (string.IsNullOrEmpty (url)) {
					return null;
				}
				return new MercurialRepository (this, string.Format ("file://{0}", url));
			} catch (Exception ex) {
				// No bzr
				LoggingService.LogError (ex.ToString ());
				return null;
			}
		}// GetRepositoryReference
	}// MercurialVersionControl
}
