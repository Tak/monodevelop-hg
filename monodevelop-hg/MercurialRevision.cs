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

using MonoDevelop.VersionControl;

namespace MonoDevelop.VersionControl.Mercurial
{
		/// <summary>
		/// Represents a revision in a Mercurial branch.
		/// </summary>
		public class MercurialRevision : Revision
		{
			public readonly string Rev;
			
			public static readonly string HEAD = "tip";
			public static readonly string FIRST = "0";
			public static readonly string NONE = "NONE";
			
			public MercurialRevision (Repository repo, string rev): base (repo) 
			{
				Rev = rev;
			}
			
			public MercurialRevision (Repository repo, string rev, DateTime time, string author, string message, RevisionPath[] changedFiles)
				: base (repo, time, author, message)
			{
				Rev = rev;
			}
			
			public override string ToString ()
			{
				return Rev;
			}
			
			public override Revision GetPrevious ()
			{
				int revno;
				if (int.TryParse (Rev, out revno))
					return new MercurialRevision (Repository, (revno-1).ToString ());
				return new MercurialRevision (Repository, string.Format ("p1({0})", Rev));
			}
		}// MercurialRevision
}
