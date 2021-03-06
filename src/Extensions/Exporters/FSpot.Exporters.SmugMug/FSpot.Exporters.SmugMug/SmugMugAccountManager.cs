//
// SmugMugAccountManager.cs
//
// Author:
//   Paul Lange <palango@gmx.de>
//   Stephen Shaw <sshaw@decriptor.com>
//
// Copyright (C) 2010 Novell, Inc.
// Copyright (C) 2010 Paul Lange
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;

using Hyena;

using Gnome.Keyring;

namespace FSpot.Exporters.SmugMug
{
	public class SmugMugAccountManager
	{
		private static SmugMugAccountManager instance;
		private const string keyring_item_name = "SmugMug Account";
		List<SmugMugAccount> accounts;

		public delegate void AccountListChangedHandler (SmugMugAccountManager manager, SmugMugAccount changed_account);
		public event AccountListChangedHandler AccountListChanged;

		public static SmugMugAccountManager GetInstance ()
		{
			if (instance == null) {
				instance = new SmugMugAccountManager ();
			}

			return instance;
		}

		private SmugMugAccountManager ()
		{
			accounts = new List<SmugMugAccount> ();
			ReadAccounts ();
		}

		public void MarkChanged ()
		{
			MarkChanged (true, null);
		}

		public void MarkChanged (bool write, SmugMugAccount changed_account)
		{
			if (write)
				WriteAccounts ();

			if (AccountListChanged != null)
				AccountListChanged (this, changed_account);
		}

		public List<SmugMugAccount> GetAccounts ()
		{
			return accounts;
		}

		public void AddAccount (SmugMugAccount account)
		{
			AddAccount (account, true);
		}

		public void AddAccount (SmugMugAccount account, bool write)
		{
			accounts.Add (account);
			MarkChanged (write, account);
		}

		public void RemoveAccount (SmugMugAccount account)
		{
			string keyring = Ring.GetDefaultKeyring();
			Hashtable request_attributes = new Hashtable();
			request_attributes["name"] = keyring_item_name;
			request_attributes["username"] = account.Username;
			try {
				foreach(ItemData result in Ring.Find(ItemType.GenericSecret, request_attributes)) {
					Ring.DeleteItem(keyring, result.ItemID);
				}
			} catch (Exception e) {
				Log.Exception (e);
			}
			accounts.Remove (account);
			MarkChanged ();
		}

		public void WriteAccounts ()
		{
			string keyring = Ring.GetDefaultKeyring();
			foreach (SmugMugAccount account in accounts) {
				Hashtable update_request_attributes = new Hashtable();
				update_request_attributes["name"] = keyring_item_name;
				update_request_attributes["username"] = account.Username;

				Ring.CreateItem(keyring, ItemType.GenericSecret, keyring_item_name, update_request_attributes, account.Password, true);
			}
		}

		private void ReadAccounts ()
		{

			Hashtable request_attributes = new Hashtable();
			request_attributes["name"] = keyring_item_name;
			try {
				foreach(ItemData result in Ring.Find(ItemType.GenericSecret, request_attributes)) {
					if(!result.Attributes.ContainsKey("name") || !result.Attributes.ContainsKey("username") ||
						(result.Attributes["name"] as string) != keyring_item_name)
						continue;

					string username = (string)result.Attributes["username"];
					string password = result.Secret;

					if (username == null || username == String.Empty || password == null || password == String.Empty)
						throw new ApplicationException ("Invalid username/password in keyring");

					SmugMugAccount account = new SmugMugAccount(username, password);
					if (account != null)
						AddAccount (account, false);

				}
			} catch (Exception e) {
				Log.Exception (e);
			}

			MarkChanged ();
		}
	}
}
