using System;

namespace MonoDevelop.VersionControl.Mercurial.Dialogs
{
	public partial class PasswordPromptDialog : Gtk.Dialog
	{
		public PasswordPromptDialog(string prompt)
		{
			this.Build();
			this.Parent = MonoDevelop.Ide.IdeApp.Workbench.RootWindow;
			this.promptLabel.Text = GLib.Markup.EscapeText (prompt);
		}
	}
}
