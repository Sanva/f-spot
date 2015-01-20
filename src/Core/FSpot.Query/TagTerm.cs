//  TagTerm.cs
//
//  Author:
//   Stephen Shaw <sshaw@decriptor.com>
//   Stephane Delcroix <sdelcroix@src.gnome.org>
//
// Copyright (C) 2013 Stephen Shaw
// Copyright (C) 2008 Novell, Inc.
// Copyright (C) 2008 Stephane Delcroix
//
//  Permission is hereby granted, free of charge, to any person obtaining
//  a copy of this software and associated documentation files (the
//  "Software"), to deal in the Software without restriction, including
//  without limitation the rights to use, copy, modify, merge, publish,
//  distribute, sublicense, and/or sell copies of the Software, and to
//  permit persons to whom the Software is furnished to do so, subject to
//  the following conditions:
//
//  The above copyright notice and this permission notice shall be
//  included in all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND,
//  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//

using System;
using System.Collections.Generic;

using FSpot.Core;

namespace FSpot.Query
{
	public class TagTerm : LogicalTerm, IDisposable
	{
		public Tag Tag { get; private set; }

		public TagTerm (Tag tag)
		{
			Tag = tag;
		}

		public override string SqlClause ()
		{
			return SqlClause (this);
		}

		internal static string SqlClause (params TagTerm [] tags)
		{
			var list = new List<string> (tags.Length);
			foreach (TagTerm tag in tags)
				list.Add (tag.Tag.Id.ToString ());
			return SqlClause (list.ToArray ());
		}

		static string SqlClause (string [] tagids)
		{
			if (tagids.Length == 0)
				return null;
			if (tagids.Length == 1)
				return String.Format (" (photos.id IN (SELECT photo_id FROM photo_tags WHERE tag_id = {0})) ", tagids [0]);
			return String.Format (" (photos.id IN (SELECT photo_id FROM photo_tags WHERE tag_id IN ({0}))) ", String.Join (", ", tagids));
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (Tag != null)
					Tag.Dispose ();
			}
			else
			{
				if (Tag != null)
					Tag.Dispose ();
			}
		}

		~TagTerm ()
		{
			Console.WriteLine ("Finalizer called on {0}. Should be Disposed", GetType ());
			Dispose (false);
		}
	}
}
