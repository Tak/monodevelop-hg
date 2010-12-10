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

using Gtk;

namespace MonoDevelop.VersionControl.Mercurial.Dialogs
{
	/// <summary>
	/// Dialog for selecting a branch
	/// </summary>
	public partial class BranchSelectionDialog : Gtk.Dialog
	{
		Gtk.ListStore branchStore = new Gtk.ListStore (typeof(string));
		
		public string SelectedLocation {
			get { 
				string loc = string.Empty;
				
				try {
					Gtk.TreeIter iter;
					branchTreeView.Selection.GetSelected (out iter);
					loc = (string)branchStore.GetValue (iter, 0);
				} catch { }
				
				return loc;
			}
		}// SelectedLocation

		public bool SaveDefault {
			get{ return defaultCB.Active; }
		}// SaveDefault

		public string LocalPath {
			get{ return localPathButton.Filename; }
		}// LocalPath
		
		public bool Overwrite {
			get{ return overwriteCB.Active; }
		}// Overwrite
		
		public bool OmitHistory {
			get{ return omitCB.Active; }
		}// OmitHistory

		protected virtual void OnOmitCBToggled (object sender, System.EventArgs e)
		{
			if (omitCB.Active) {
				overwriteCB.Active = false;
			}
			overwriteCB.Sensitive = omitCB.Active;
		}// OnOmitCBToggled
		
		public BranchSelectionDialog(ICollection<string> branchLocations, string defaultLocation, string localDirectory, bool enableLocalPathSelection, bool enableRemember, bool enableOverwrite, bool enableOmitHistory)
		{
			this.Build();

			Gtk.CellRendererText textRenderer = new Gtk.CellRendererText ();
			textRenderer.Editable = true;
			textRenderer.Edited += delegate(object o, EditedArgs args) {
				try {
					Gtk.TreeIter eiter;
					branchStore.GetIterFromString (out eiter, args.Path);
					branchStore.SetValue (eiter, 0, args.NewText);
				} catch {}
			};
			
			branchTreeView.Model = branchStore;
			branchTreeView.HeadersVisible = false;
			branchTreeView.AppendColumn ("Branch", textRenderer, "text", 0);
			
			Gtk.TreeIter iter,
			             defaultIter = default(Gtk.TreeIter);
			bool found = false;

			foreach (string location in branchLocations) {
				iter = branchStore.AppendValues (location);
				if (location == defaultLocation) {
					defaultIter = iter;
					found = true;
				}
			}
			iter = branchStore.AppendValues (string.Empty);
			
			if (1 == branchLocations.Count) {
				branchStore.GetIterFirst (out iter);
			}// when only one branch is known, default to it
			
			branchTreeView.Selection.SelectIter (found? defaultIter: iter);

			if (!string.IsNullOrEmpty (localDirectory))
				localPathButton.SetCurrentFolder (localDirectory);
			localPathButton.Sensitive = enableLocalPathSelection;
			omitCB.Visible = enableOmitHistory;
			defaultCB.Sensitive = enableRemember;
			overwriteCB.Sensitive = enableOverwrite;
		}// constructor
	}
}
