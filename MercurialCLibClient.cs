// Copyright (C) 2009 by Levi Bard <taktaktaktaktaktaktaktaktaktak@gmail.com>
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
using System.Xml;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using MonoDevelop.Ide;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;

namespace MonoDevelop.VersionControl.Mercurial
{
	
	/// <summary>
	/// Class for interacting with mercurial via the C api
	/// </summary>
	public class MercurialCLibClient: MercurialClient
	{
		#region " P/Invokes "
		[DllImport ("python26")]
		private static extern void Py_Initialize ();
		
		[DllImport ("python26")]
		private static extern void Py_DecRef (IntPtr pyobj);
		
		[DllImport ("python26")]
		private static extern IntPtr PyImport_AddModule (string module);
		
		[DllImport ("python26")]
		private static extern IntPtr PyModule_GetDict (IntPtr module);
		
		[DllImport ("python26")]
		private static extern IntPtr PyMapping_GetItemString (IntPtr dict, string itemname);
		
		[DllImport ("python26")]
		private static extern int PyRun_SimpleString (string command);
		
		[DllImport ("python26")]
		private static extern int PyInt_AsLong (IntPtr pyint);
		
		[DllImport ("python26")]
		private static extern int PyString_AsStringAndSize (IntPtr pystring, out IntPtr buffer, out int size);
		
		[DllImport ("python26")]
		private static extern void PyErr_Clear ();
		
		[DllImport ("python26")]
		private static extern void PyEval_InitThreads ();
		
		private static Regex unicodeRegex = new Regex (@"^\s*u'(?<realString>.*)'\s*$", RegexOptions.Compiled);
		private delegate string StringMarshaller (IntPtr pointer);
		private static StringMarshaller marshaller = (PropertyService.IsWindows)? 
		                                             (StringMarshaller)Marshal.PtrToStringAnsi: 
		                                             (StringMarshaller)Marshal.PtrToStringAuto;
		/// <summary>
		/// Get a .NET string from a python string pointer.
		/// </summary>
		private static string StringFromPython (IntPtr pystring)
		{
			int size = 0;
			IntPtr buffer = IntPtr.Zero;
			string stringVal = null;
			
			if (IntPtr.Zero == pystring){ return string.Empty; }
            
			try {
				PyString_AsStringAndSize (pystring, out buffer, out size);
				stringVal = marshaller (buffer);
				if (string.IsNullOrEmpty (stringVal)){ return string.Empty; }
			} finally {
				Py_DecRef (pystring);
			}
			
			Match match = unicodeRegex.Match (stringVal);
			if (match.Success) {
				stringVal = match.Groups["realString"].Value;
			}
			
			return stringVal;
		}// StringFromPython
		
		/// <summary>
		/// Convenience wrapper for PyRun_SimpleString
		/// </summary>
		/// <param name="variables">
		/// A <see cref="List[System.String]"/>: The names of the return variables in command
		/// </param>
		/// <param name="command">
		/// A <see cref="System.String"/>: The command to run
		/// </param>
		/// <param name="format">
		/// A <see cref="System.Object[]"/>: string.Format()-style args for command
		/// </param>
		/// <returns>
		/// A <see cref="List[IntPtr]"/>: The values of variables, if any
		/// </returns>
		private static List<IntPtr> run (List<string> variables, string command, params object[] format)
		{
			List<IntPtr> rv = new List<IntPtr> ();
			
			if (0 != PyRun_SimpleString (string.Format (command, format))) {
				string trace = "Unable to retrieve error data.";
				if (0 == PyRun_SimpleString ("trace = ''.join(traceback.format_exception(sys.last_type, sys.last_value, sys.last_traceback))\n")) {
					trace = StringFromPython (PyMapping_GetItemString (maindict, "trace"));
				}
				PyErr_Clear ();
				
				throw new MercurialClientException(string.Format ("Error running '{0}': {1}{2}", string.Format (command, format), Environment.NewLine, trace));
			}
		
			if (null != variables) {
				rv = variables.ConvertAll<IntPtr> (delegate(string variable){ 
					return PyMapping_GetItemString (maindict, variable);
				});
			}
			PyErr_Clear ();
			
			return rv;
		}// run
		#endregion
		
		
		public override string Version {
			get {
				if (null == version) {
					lock (lockme) {
						version = StringFromPython (run (new List<string>{"version"}, "version = util.version()")[0]);
					}
				}
				return version;
			}
		}
		static string version;
		
		private static IntPtr pymain;
		private static IntPtr maindict;
		private static readonly string lockme = "lockme";
		// private static Regex UrlRegex = new Regex (@"^(?<protocol>[^:\s]+)://((?<username>[^:\s]+?)(:(?<password>[^@\s]+?))?@)?(?<host>[^:/\s]+)(:(?<port>\d+))?(?<path>/[^\s]*)$", RegexOptions.Compiled);
		
		static MercurialCLibClient ()
		{
			try {
				PyEval_InitThreads ();
				Py_Initialize ();
				
				pymain = PyImport_AddModule ("__main__");
				maindict = PyModule_GetDict (pymain);
				
				// Imports
				string[] imports = new string[]{
					"import sys",
					"import os",
					"import os.path",
					"if('win32'==sys.platform): sys.path.append('C:/Program Files/Mercurial')",
					"if('win32'==sys.platform): sys.path.append('C:/Program Files (x86)/Mercurial')",
					"if('win32'==sys.platform): sys.path.append('C:/Program Files/Mercurial/library.zip')",
					"if('win32'==sys.platform): sys.path.append('C:/Program Files (x86)/Mercurial/library.zip')",
					"import traceback",
					"import StringIO",
					"import mercurial",
					"from mercurial import hg",
					"from mercurial import ui",
					"from mercurial import util",
					"from mercurial import commands",
				};
				
				foreach (string import in imports) {
					run (null, import);
				}
			} catch (DllNotFoundException dnfe) {
				LoggingService.LogWarning ("Unable to initialize MercurialCLibClient", dnfe);
			}
		}// static constructor
		
		public MercurialCLibClient()
		{
		}
		
		string RunMercurialCommand (string baseCommand, params object[] args)
		{
			StringBuilder command = new StringBuilder ();
			string output;
			
			command.Append ("myui = ui.ui()\n");
			command.Append ("myui.pushbuffer()\n");
			command.AppendFormat (baseCommand, args);
			command.Append ("\noutput=myui.popbuffer()\n");
			lock (lockme){ output = StringFromPython (run (new List<string>{"output"}, command.ToString ())[0]); }
			
			return output;
		}

		string RunMercurialRepoCommand (string repoPath, string baseCommand, params object[] args)
		{
			StringBuilder command = new StringBuilder ();
			string output;
			
			command.AppendFormat ("repo = hg.repository(ui.ui(),'{0}')\n", GetLocalBasePath (repoPath));
			command.Append ("repo.ui.pushbuffer()\n");
			command.AppendFormat (baseCommand, args);
			command.Append ("\noutput=repo.ui.popbuffer()\n");
			// Console.WriteLine ("Running: {0}", command.ToString());
			lock (lockme){ output = StringFromPython (run (new List<string>{"output"}, command.ToString ())[0]); }
			
			return output;
		}

		// TODO: Recurse not supported
		public override void Add (string localPath, bool recurse, MonoDevelop.Core.IProgressMonitor monitor)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			RunMercurialRepoCommand (localPath, "commands.add (repo.ui, repo, os.path.realpath('{0}')", localPath);
		}

		public override void Branch (string branchLocation, string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			if (null == monitor){ monitor = new MonoDevelop.Core.ProgressMonitoring.NullProgressMonitor (); }
			
			string output = RunMercurialCommand ("commands.clone(myui,'{0}','{1}')", branchLocation, localPath);
			
			monitor.Log.WriteLine (output);
			monitor.Log.WriteLine ("Cloned to {0}", localPath);
		}

		public override void Checkout (string url, string targetLocalPath, MercurialRevision rev, bool recurse, MonoDevelop.Core.IProgressMonitor monitor)
		{
			// TODO: Support this?
			return;
			/*
			if (null == monitor){ monitor = new MonoDevelop.Core.ProgressMonitoring.NullProgressMonitor (); }
			string pyrev = "None";
			string realUrl = url;
			StringBuilder command = new StringBuilder ();
			
			Match match = UrlRegex.Match (url);
			if(match.Success) {
				realUrl = UrlRegex.Replace(url, @"${protocol}://${host}$3${path}");
			}
			
			lock (lockme) {
				run (null, "b = branch.Branch.open_containing(url=ur\"{0}\")[0]\n", realUrl);
			}
			
			monitor.Log.WriteLine ("Opened {0}", url);
			
			if (null != rev && MercurialRevision.HEAD != rev.Rev && MercurialRevision.NONE != rev.Rev) {
				command.AppendFormat ("revspec = revisionspec.RevisionSpec.from_string(spec=\"{0}\")\n", rev.Rev);
				pyrev = "revspec.in_history(branch=b).rev_id";
			}
			command.AppendFormat ("b.create_checkout(to_location=ur\"{1}\", revision_id={0})\n", pyrev, NormalizePath (targetLocalPath));

			lock (lockme){ run (null, command.ToString ()); }
			monitor.Log.WriteLine ("Checkout to {0} completed", targetLocalPath);
			*/
		}

		public override void Commit (ChangeSet changeSet, MonoDevelop.Core.IProgressMonitor monitor)
		{
			List<string> files = new List<string> ();
			string   basePath = MercurialRepository.GetLocalBasePath (changeSet.BaseLocalPath),
			         pyfiles = string.Empty,
			         messageFile = Path.GetTempFileName ();
			if (!basePath.EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.Ordinal)) {
				basePath += Path.DirectorySeparatorChar;
			}
			
			foreach (ChangeSetItem item in changeSet.Items) {
				files.Add (string.Format ("os.path.realpath('{0}')", item.LocalPath.FullPath));
			}
			
			if (!(0 == files.Count || (1 == files.Count && string.Empty.Equals (files[0], StringComparison.Ordinal)))) {
				pyfiles = string.Format ("','.join([{0}]),", string.Join (",", files.ToArray ()));
			}
			
			try {
				File.WriteAllText (messageFile, changeSet.GlobalComment);
				RunMercurialRepoCommand (basePath, "commands.commit(repo.ui,repo,{1}logfile='{0}')", messageFile, pyfiles);
			} finally {
				try {
					File.Delete (messageFile);
				} catch { }
			}
		}
		
		public override DiffInfo[] Diff (string basePath, string[] files)
		{
			List<DiffInfo> results = new List<DiffInfo> ();
			basePath = NormalizePath (Path.GetFullPath (basePath));
			
			if (null == files || 0 == files.Length) {
				if (Directory.Exists (basePath)) {
					IList<LocalStatus> statuses = Status (basePath, new MercurialRevision (null, MercurialRevision.HEAD));
					List<string> foundFiles = new List<string> ();
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

			foreach (string file in files) {
				string fullPath = Path.Combine (basePath, file);
				results.Add (new DiffInfo (basePath, file, RunMercurialRepoCommand (fullPath, "commands.diff(repo.ui,repo,os.path.realpath('{0}'))", fullPath)
				                                           .Replace ("\r\n", Environment.NewLine)));
			}
			
			return results.ToArray ();
		}

		public override DiffInfo[] Diff (string path, MercurialRevision fromRevision, MercurialRevision toRevision)
		{
			// Need history for this
			path = NormalizePath (Path.GetFullPath (path));
			return new[]{ new DiffInfo (GetLocalBasePath (path), path, 
			                  RunMercurialRepoCommand (path, "commands.diff(repo.ui,repo,os.path.realpath('{2}'),rev='{0}:{1}')", fromRevision.Rev, toRevision.Rev, path))
			};
			
			/*
			List<DiffInfo> results = new List<DiffInfo> ();
			path = NormalizePath (Path.GetFullPath (path));
			StringBuilder command = new StringBuilder ();
			
			command.AppendFormat ("outfile = StringIO.StringIO()\n");
			command.AppendFormat ("old_tree = None\n");
			command.AppendFormat ("new_tree = None\n");
			command.AppendFormat ("try:\n");
			command.AppendFormat ("  old_tree,new_tree,old_branch,new_branch,specific_files,extra_trees = diff.get_trees_and_branches_to_diff(path_list=None, revision_specs=[revisionspec.RevisionSpec.from_string(ur\"{0}\"), revisionspec.RevisionSpec.from_string(ur\"{1}\")], old_url=ur\"{2}\", new_url=ur\"{2}\")\n", 
			                      fromRevision, toRevision, path);
			command.AppendFormat ("  mydiff = bzrlib.diff.DiffTree(old_tree=old_tree, new_tree=new_tree, to_file=outfile)\n");
			command.AppendFormat ("  old_tree.lock_read()\n");
			command.AppendFormat ("  new_tree.lock_read()\n");
			command.AppendFormat ("  mydiff.show_diff(specific_files=specific_files, extra_trees=extra_trees)\n");
			command.AppendFormat ("  output = outfile.getvalue()\n");
			command.AppendFormat ("finally:\n");
			command.AppendFormat ("  outfile.close()\n");
			command.AppendFormat ("  if(old_tree): old_tree.unlock()\n");
			command.AppendFormat ("  if(new_tree): new_tree.unlock()\n");
		
			lock (lockme) {
				string output = StringFromPython (run (new List<string>{"output"}, command.ToString ())[0]);
				results.Add (new DiffInfo (Path.GetDirectoryName (path), Path.GetFileName (path), 
				                           output.Replace ("\r\n", Environment.NewLine)));
			}
			
			return results.ToArray ();
			*/
		}
		
		public override MercurialRevision[] GetIncoming (MercurialRepository repo, string remote)
		{
			List<MercurialRevision> revisions = new List<MercurialRevision> ();
			if (string.IsNullOrEmpty (remote)) remote = "default";
			
			string logText = RunMercurialRepoCommand (repo.LocalBasePath, "commands.incoming(repo.ui,repo,source='{0}',bundle=None,force=False,style='xml')", remote);
			int xmlIndex = logText.IndexOf ("<?xml");
			
			if (0 > xmlIndex) return revisions.ToArray ();
			
			XmlDocument doc = new XmlDocument ();
			try {
				doc.LoadXml (logText.Substring (xmlIndex));
			} catch (XmlException xe) {
				LoggingService.LogError ("Error getting incoming for " + remote, xe);
				return revisions.ToArray ();
			}
			
			foreach (XmlNode node in doc.SelectNodes ("/log/logentry")) {
				revisions.Add (NodeToRevision (repo, node));
			}
			
			return revisions.ToArray ();
		}

		public override MercurialRevision[] GetOutgoing (MercurialRepository repo, string remote)
		{
			List<MercurialRevision> revisions = new List<MercurialRevision> ();
			if (string.IsNullOrEmpty (remote)) remote = "default";
			
			string logText = RunMercurialRepoCommand (repo.LocalBasePath, "commands.outgoing(repo.ui,repo,dest='{0}',style='xml')", remote);
			int xmlIndex = logText.IndexOf ("<?xml");
			
			if (0 > xmlIndex) return revisions.ToArray ();
			
			XmlDocument doc = new XmlDocument ();
			try {
				doc.LoadXml (logText.Substring (xmlIndex));
			} catch (XmlException xe) {
				LoggingService.LogError ("Error getting outgoing for " + remote, xe);
				return revisions.ToArray ();
			}
			
			foreach (XmlNode node in doc.SelectNodes ("/log/logentry")) {
				revisions.Add (NodeToRevision (repo, node));
			}
			
			return revisions.ToArray ();
		}

		public override MercurialRevision[] GetHistory (MercurialRepository repo, string localFile, MercurialRevision since)
		{
			localFile = NormalizePath (Path.GetFullPath (localFile));
			string revText = ",rev=None"; // FIXME: string.Format (",rev='-1:{0}'", since.Rev);
			List<MercurialRevision> revisions = new List<MercurialRevision> ();
			string logText = RunMercurialRepoCommand (localFile, "commands.log(repo.ui,repo,os.path.realpath('{0}'),date=None,user=None{1},style='xml')", localFile, ",rev=None");
			
			XmlDocument doc = new XmlDocument ();
			try {
				doc.LoadXml (logText);
			} catch (XmlException xe) {
				LoggingService.LogError ("Error getting history for " + localFile, xe);
				return revisions.ToArray ();
			}
			
			foreach (XmlNode node in doc.SelectNodes ("/log/logentry")) {
				revisions.Add (NodeToRevision (repo, node));
			}
			
			ThreadPool.QueueUserWorkItem (delegate {
				string basePath = MercurialRepository.GetLocalBasePath (localFile);
				foreach (MercurialRevision rev in revisions) {
					List<RevisionPath> paths = new List<RevisionPath> ();
					foreach (LocalStatus status in Status (basePath, rev)
					         .Where (s => s.Status != ItemStatus.Unchanged && s.Status != ItemStatus.Unversioned)) {
						paths.Add (new RevisionPath (status.Filename, ConvertAction (status.Status), status.Status.ToString ()));
					}
					rev.ChangedFiles = paths.ToArray ();
				}
			});
			
			return revisions.ToArray ();
		}// GetHistory
		
		public override MercurialRevision[] GetHeads (MercurialRepository repo)
		{
			string localPath = NormalizePath (Path.GetFullPath (repo.LocalBasePath));
			string logText = RunMercurialRepoCommand (localPath, "commands.heads(repo.ui,repo,style='xml')", localPath);
			List<MercurialRevision> revisions = new List<MercurialRevision> ();
			
			XmlDocument doc = new XmlDocument ();
			try {
				doc.LoadXml (logText);
			} catch (XmlException xe) {
				LoggingService.LogError ("Error getting heads for " + localPath, xe);
				return revisions.ToArray ();
			}
			
			foreach (XmlNode node in doc.SelectNodes ("/log/logentry")) {
				revisions.Add (NodeToRevision (repo, node));
			}
			
			return revisions.ToArray ();
		}
		
		static MercurialRevision NodeToRevision (MercurialRepository repo, XmlNode node)
		{
			string changeset = node.Attributes["revision"].Value;
			string date = node.SelectSingleNode ("date").InnerText;
			string user = node.SelectSingleNode ("author").Attributes["email"].Value;
			string message = node.SelectSingleNode ("msg").InnerText;
			return new MercurialRevision (repo, changeset, DateTime.Parse (date), user, message, new RevisionPath[]{});
		}
		
		public override System.Collections.Generic.Dictionary<string, BranchType> GetKnownBranches (string path)
		{
			// TODO: Test more thoroughly with remote repos
			var branches = new System.Collections.Generic.Dictionary<string,BranchType> ();
			path = NormalizePath (Path.GetFullPath (path));
			foreach (string remote in RunMercurialRepoCommand (path, "commands.paths(repo.ui, repo)")
			         .Split (new[]{'\r','\n'}, StringSplitOptions.RemoveEmptyEntries)) {
				string[] tokens = remote.Split (new[]{'='}, 2);
				branches.Add (tokens[0].Trim (), BranchType.Public);
			}
			
			return branches;
		}// GetKnownBranches

		/*
		public override string GetPathUrl (string path)
		{
			IntPtr branch = IntPtr.Zero;
			
			lock (lockme) {
				branch = run (new List<string>{"mybase"}, "mybranch = branch.Branch.open_containing(url=ur\"{0}\")[0]\nmybase=mybranch.base\n", NormalizePath (Path.GetFullPath (path)))[0];
				string baseurl = StringFromPython (branch);
				return baseurl.StartsWith ("file://", StringComparison.Ordinal)? baseurl.Substring (7): baseurl;
			}
		}
		*/

		public override string GetTextAtRevision (string path, MercurialRevision rev)
		{
			path = NormalizePath (Path.GetFullPath (path));
			string tempfile = Path.GetTempFileName ();
			RunMercurialRepoCommand (path, "commands.cat(repo.ui,repo,os.path.realpath('{0}'),rev='{1}',output='{2}')", path, rev.Rev, tempfile);
			return File.ReadAllText (tempfile);
		}// GetTextAtRevision

		// FIXME: Is this being used for anything?
		public override System.Collections.Generic.IList<string> List (string path, bool recurse, ListKind kind)
		{
			List<string> found = new List<string> (){ path };
			
			/*
			List<IntPtr> pylist = null;
			string[] list = null;
			string relpath = string.Empty;

			StringBuilder command = new StringBuilder ();
			command.AppendFormat ("tree,relpath = workingtree.WorkingTree.open_containing(path=ur\"{0}\")\n", NormalizePath (path));
			command.AppendFormat ("mylist = \"\"\ntree.lock_read()\n");
			command.AppendFormat ("try:\n  for entry in tree.list_files():\n    mylist = mylist+entry[0]+\"|\"+entry[2]+\"\\n\"\n");
			command.AppendFormat ("finally:\n  tree.unlock()\n");
			
			lock (lockme) {
				try {
					pylist = run (new List<string>{"mylist","relpath"}, command.ToString ());
					list = StringFromPython (pylist[0]).Split('\n');
					relpath = StringFromPython (pylist[1]);
				} catch {
					return found;
				}
			}// lock
					
			foreach (string line in list) {
				string[] tokens = line.Split('|');
				if ((tokens[0].StartsWith (relpath, StringComparison.Ordinal)) && 
				    (ListKind.All == kind || listKinds[kind].Equals (tokens[1], StringComparison.Ordinal)) && 
					(recurse || !tokens[0].Substring (relpath.Length).Contains ("/"))) {
					found.Add (tokens[0]);
				}// if valid match
			}
			*/
			
			return found;
		}// List

		public override void Merge (MercurialRepository repository)
		{
			string localPath = NormalizePath (Path.GetFullPath (repository.LocalBasePath));
			RunMercurialRepoCommand (localPath, "commands.merge(repo.ui,repo)");
		}

		public override void Pull (string pullLocation, string localPath, bool remember, bool overwrite, MonoDevelop.Core.IProgressMonitor monitor)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			if (null == monitor){ monitor = new MonoDevelop.Core.ProgressMonitoring.NullProgressMonitor (); }
			string output = RunMercurialRepoCommand (localPath, "commands.pull(repo.ui,repo,'{0}',update=True)", pullLocation);
			monitor.Log.WriteLine (output);
			monitor.Log.WriteLine ("Pulled to {0}", localPath);
		}

		public override void Push (string pushLocation, string localPath, bool remember, bool overwrite, MonoDevelop.Core.IProgressMonitor monitor)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			if (null == monitor){ monitor = new MonoDevelop.Core.ProgressMonitoring.NullProgressMonitor (); }
			string output = RunMercurialRepoCommand (localPath, "commands.push(repo.ui,repo,dest='{0}',force={1})", pushLocation, overwrite? "True": "False");
			monitor.Log.WriteLine (output);
			monitor.Log.WriteLine ("Pushed to {0}", pushLocation);
		}

		public override void Remove (string path, bool force, MonoDevelop.Core.IProgressMonitor monitor)
		{
			path = NormalizePath (Path.GetFullPath (path));
			RunMercurialRepoCommand (path, "commands.remove(repo.ui,repo,os.path.realpath('{0}'),force={1})", path, force? "True": "False");
		}

		public override void Resolve (string path, bool recurse, MonoDevelop.Core.IProgressMonitor monitor)
		{
			path = NormalizePath (Path.GetFullPath (path));
			RunMercurialRepoCommand (path, "commands.resolve(repo.ui,repo,os.path.realpath('{0}'),mark=True)", path);
		}

		public override void Revert (string localPath, bool recurse, MonoDevelop.Core.IProgressMonitor monitor, MercurialRevision toRevision)
		{
			string rev = string.Empty;
			localPath = NormalizePath (Path.GetFullPath (localPath));
			if (null != toRevision && MercurialRevision.HEAD != toRevision.Rev && MercurialRevision.NONE != toRevision.Rev) {
				rev = string.Format (",rev='{0}',date=None", toRevision.Rev);
			} else {
				rev = ",rev='tip',date=None";
			}
			
			RunMercurialRepoCommand (localPath, "commands.revert(repo.ui,repo,os.path.realpath('{0}'){1})", localPath, rev);
		}

		public override System.Collections.Generic.IList<LocalStatus> Status (string path, MercurialRevision revision)
		{
			List<LocalStatus> statuses = new List<LocalStatus> ();
			string rev = string.Empty;
			ItemStatus itemStatus;
			LocalStatus mystatus = null;
			LocalStatus tmp;
			bool modified = false;
			
					
			path = NormalizePath (Path.GetFullPath (path).Replace ("{", "{{").Replace ("}", "}}"));// escape for string.format
			
			if (null != revision && MercurialRevision.HEAD != revision.Rev && MercurialRevision.NONE != revision.Rev) {
				rev = string.Format (",change={0}", revision.Rev);
			}
			
			string statusText = RunMercurialRepoCommand (path, "commands.status(repo.ui,repo,os.path.realpath('{0}'),all=True{1})\n", path, rev);
			// Console.WriteLine (statusText);
			
			foreach (string line in statusText.Split (new[]{'\r','\n'}, StringSplitOptions.RemoveEmptyEntries)) {
				string[] tokens = line.Split (new[]{' '}, 2);
				itemStatus = (ItemStatus)tokens[0][0];
				// Console.WriteLine ("Got status {0} for path {1}", tokens[0], tokens[1]);
				tmp = new LocalStatus (string.Empty, Path.GetFullPath (NormalizePath (tokens[1])), itemStatus);
				statuses.Add (tmp);
				if (itemStatus != ItemStatus.Ignored && itemStatus != ItemStatus.Unversioned && itemStatus != ItemStatus.Unchanged) {
					modified = true;
				}
				if (Path.GetFileName (path).Equals (Path.GetFileName (tokens[1]), StringComparison.OrdinalIgnoreCase)) {
					mystatus = tmp;
				}
			}
			
			string conflictText = RunMercurialRepoCommand (path, "commands.resolve(repo.ui,repo,os.path.realpath('{0}'),list=True)", path);
			// System.Console.WriteLine (conflictText);
			foreach (string line in conflictText.Split (new[]{'\r','\n'}, StringSplitOptions.RemoveEmptyEntries)) {
				string[] tokens = line.Split (new[]{' '}, 2);
				itemStatus = (ItemStatus)tokens[0][0];
				if (ItemStatus.Conflicted == itemStatus) {
					LocalStatus status = statuses.Find ((s)=>
						s.Filename.EndsWith (tokens[1], StringComparison.OrdinalIgnoreCase)
					);
					if (null == status) {
						statuses.Add (new LocalStatus (MercurialRevision.HEAD, tokens[1], ItemStatus.Conflicted));
					} else {
						status.Status = ItemStatus.Conflicted;
					}
				}
//				Console.WriteLine ("Got status {0} for path {1}", tokens[0], tokens[1]);
			}
			
			if (null == mystatus) {
				statuses.Insert (0, new LocalStatus (string.Empty, GetLocalBasePath (path), modified? ItemStatus.Modified: ItemStatus.Unchanged));
			}
			
			return statuses;
		}

		public override void Update (string localPath, bool recurse, MonoDevelop.Core.IProgressMonitor monitor)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			if (null == monitor){ monitor = new MonoDevelop.Core.ProgressMonitoring.NullProgressMonitor (); }
			string output = RunMercurialRepoCommand (localPath, "commands.update(repo.ui,repo)");
			monitor.Log.WriteLine (output);
		}
		
		public override void StoreCredentials (string url)
		{
			/*
			try {
				Match match = UrlRegex.Match (url);
				if ((!url.StartsWith ("lp:", StringComparison.Ordinal)) && 
				    match.Success &&
				    (match.Groups["username"].Success || match.Groups["password"].Success)) // No sense storing credentials with no username or password
				{ 
					string protocol = match.Groups["protocol"].Value.Trim();
					
					if ("sftp".Equals(protocol, StringComparison.OrdinalIgnoreCase) ||
					    protocol.EndsWith("ssh", StringComparison.OrdinalIgnoreCase)) {
						protocol = "ssh";
					}
					
					run (null, "config.AuthenticationConfig().set_credentials(name='{0}', host='{1}', user='{2}', scheme='{3}', password={4}, port={5}, path='{6}', verify_certificates=False)", 
					     UrlRegex.Replace(url, @"${protocol}://${host}$3${path}"),
					     match.Groups["host"].Value,
					     match.Groups["username"].Success? match.Groups["username"].Value: string.Empty,
					     protocol,
					     (match.Groups["password"].Success && !"ssh".Equals(protocol, StringComparison.OrdinalIgnoreCase))? string.Format("'{0}'", match.Groups["password"].Value): "None",
					     match.Groups["port"].Success? match.Groups["port"].Value: "None",
					     match.Groups["path"].Value);
				}  // ignore LP urls
				
				System.Console.WriteLine("Stored credentials to {0}", Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".bazaar"), "authentication.conf") );
			} catch { } // Don't care
			*/
		}// StoreCredentials
		
		public override void Init (string path)
		{
			RunMercurialCommand ("commands.init(myui,os.path.realpath('{0}'))", path);
		}// Init
		
		public override void Ignore (string path)
		{
			string hgignore = Path.Combine (NormalizePath (Path.GetFullPath (path)), ".hgignore");
			File.AppendAllText (hgignore, path);
		}// Ignore
		
		public override bool IsBound (string path)
		{
			// Mercurial doesn't support bound branches yet (by default)
			return false;
		}// IsBound
		
		public override string GetBoundBranch (string path)
		{
			// Mercurial doesn't support bound branches yet (by default)
			return string.Empty;
			
			/*
			string method = (IsBound (path)? "get_bound_location": "get_old_bound_location");
			string location = string.Empty;
			
			StringBuilder command = new StringBuilder ();
			command.AppendFormat ("b = branch.Branch.open_containing(url=ur'{0}')[0]\n", NormalizePath (path));
			command.AppendFormat ("bound = repr(b.{0}())\n", method);
			
			location = StringFromPython(run (new List<string>{"bound"}, command.ToString ())[0]);
			return ("None" == location? string.Empty: location);
			*/
		}// GetBoundBranch
		
		public override void Bind (string branchUrl, string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			// Mercurial doesn't support bound branches yet (by default)
			
			/*
			run (null, "b = branch.Branch.open_containing(url=ur'{0}')[0]\n", localPath);
			monitor.Log.WriteLine ("Opened {0}", NormalizePath (localPath));
			
			run (null, "remoteb = branch.Branch.open_containing(url=ur'{0}')[0]\n", branchUrl);
			monitor.Log.WriteLine ("Opened {0}", branchUrl);
			
			run (null, "b.bind(other=remoteb)\n");
			monitor.Log.WriteLine ("Bound {0} to {1}", localPath, branchUrl);
			*/
		}// Bind

		public override void Unbind (string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			// Mercurial doesn't support bound branches yet (by default)
			
			/*
			run (null, "b = branch.Branch.open_containing(url=ur'{0}')[0]\n", NormalizePath (localPath));
			monitor.Log.WriteLine ("Opened {0}", localPath);
			
			run (null, "b.unbind()\n");
			monitor.Log.WriteLine ("Unbound {0}", localPath);
			*/
		}// Unbind
		
		public override void Uncommit (string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			RunMercurialRepoCommand (localPath, "commands.backout(repo.ui,repo,'tip',message='Backout',logfile=None)");
		}// Uncommit
		
		public override Annotation[] GetAnnotations (string localPath)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			string annotations = RunMercurialRepoCommand (localPath, "repo.ui.quiet=True\ncommands.annotate(repo.ui,repo,repo,os.path.realpath('{0}'),user=True,number=True,date=True,rev='tip')", localPath);
			string[] lines = annotations.Split (new string[]{"\r","\n"}, StringSplitOptions.RemoveEmptyEntries);
			string[] tokens;
			char[] separators = new char[]{ ' ', '\t', ':' };
			List<Annotation> result = new List<Annotation> ();
			Annotation previous = new Annotation (string.Empty, string.Empty, DateTime.MinValue);
			
			foreach (string line in lines) {
				tokens = line.Split (separators, StringSplitOptions.RemoveEmptyEntries);
				if (2 < tokens.Length && !char.IsWhiteSpace (tokens[0][0]) && '|' != tokens[0][0]) {
					previous = new Annotation (tokens[1], tokens[0],
						DateTime.ParseExact (tokens[2], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
				}
				result.Add (previous);
			}
			return result.ToArray ();
		}// GetAnnotations
		
		public override void Export (string localPath, string exportPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			exportPath = NormalizePath (exportPath);
			if (!IsValidExportPath (exportPath)){ throw new MercurialClientException (string.Format ("Invalid export path: {0}", exportPath)); }
			if (null == monitor){ monitor = new MonoDevelop.Core.ProgressMonitoring.NullProgressMonitor (); }
			string output = RunMercurialRepoCommand (localPath, "commands.archive(repo.ui,repo,'{0}',prefix='')", exportPath);
			monitor.Log.WriteLine (output);
			monitor.Log.WriteLine ("Exported to {0}", exportPath);
		}// Export
		
		public override bool IsMergePending (string localPath)
		{
			string parentsSummary = RunMercurialRepoCommand (localPath, "commands.parents(repo.ui,repo)");
			int parentsCount = 0;
			
			parentsCount = parentsSummary.Split (new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)
				.Where (s=>s.Trim().StartsWith(GettextCatalog.GetString("changeset")))
				.Count ();
				
			return 1 < parentsCount;
		}// IsMergePending
		
		public override bool CanRebase ()
		{
			return true;
		}// CanRebase
		
		public override void Rebase (string mergeLocation, string localPath, MonoDevelop.Core.IProgressMonitor monitor)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			if (null == monitor){ monitor = new MonoDevelop.Core.ProgressMonitoring.NullProgressMonitor (); }
			string output = RunMercurialRepoCommand (localPath, "commands.pull(repo.ui,repo,'{0}',update=True,rebase=True)", mergeLocation);
			monitor.Log.WriteLine (output);
			monitor.Log.WriteLine ("Pulled to {0}", localPath);
		}// Rebase
		 
		/// <summary>
		/// Normalize a local file path (primarily for windows)
		/// </summary>
		static string NormalizePath (string path)
		{
			string normalizedPath = path;
			
			if (PropertyService.IsWindows && 
			    !string.IsNullOrEmpty (normalizedPath) &&
			    normalizedPath.Trim().EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.Ordinal)) {
				normalizedPath = normalizedPath.Trim().Remove (normalizedPath.Length-1);
			}// strip trailing backslash
			
			return normalizedPath;
		}// NormalizePath
		
		/* WIP
		public MercurialRevision[] GetHeads (string localPath)
		{
			localPath = NormalizePath (Path.GetFullPath (localPath));
			string output = RunMercurialRepoCommand (localPath, "commands.heads(repo.ui,repo,style='xml')");
			
			foreach (XmlNode node in doc.SelectNodes ("/log/logentry")) {
				changeset = node.Attributes["revision"].Value;
				date = node.SelectSingleNode ("date").InnerText;
				user = node.SelectSingleNode ("author").Attributes["email"].Value;
				message = node.SelectSingleNode ("msg").InnerText;
				revisions.Add (new MercurialRevision (repo, changeset, DateTime.Parse (date), user, message, new RevisionPath[]{}));
			}
			
			return null;
		}// GetHeads
		*/
		
	}// MercurialCLibClient
}
