using FSpot.Core;
using FSpot.UI.Dialog;
using FSpot.Widgets;
using FSpot.Utils;
using FSpot.Import;
using Gtk;
using Hyena;
using System;
using System.Collections.Generic;
using Mono.Unix;

namespace FSpot.UI.Dialog
{
    public class ImportDialog : BuilderDialog
    {
        static readonly string select_folder_label = Catalog.GetString ("Choose Folder...");
        private ImportController Controller { get; set; }
        private TreeStore Sources { get; set; }

        private static Dictionary<string, ImportSource> history_sources = new Dictionary<string, ImportSource> ();

        [GtkBeans.Builder.Object] Button cancel_button;
        [GtkBeans.Builder.Object] Button import_button;
        [GtkBeans.Builder.Object] CheckButton copy_check;
        [GtkBeans.Builder.Object] CheckButton duplicate_check;
        [GtkBeans.Builder.Object] CheckButton recurse_check;
        [GtkBeans.Builder.Object] CheckButton remove_check;
        [GtkBeans.Builder.Object] Button remove_warning_button;
        [GtkBeans.Builder.Object] ComboBox sources_combo;
        [GtkBeans.Builder.Object] HBox tagentry_box;
        [GtkBeans.Builder.Object] HPaned import_hpaned;
        [GtkBeans.Builder.Object] ProgressBar progress_bar;
        [GtkBeans.Builder.Object] ScrolledWindow icon_scrolled;
        [GtkBeans.Builder.Object] ScrolledWindow photo_scrolled;
        [GtkBeans.Builder.Object] Label attachtags_label;

        private PhotoImageView photo_view;
        private TagEntry tag_entry;

        public ImportDialog (ImportController controller, Window parent) : base ("import.ui", "import_dialog")
        {
            Controller = controller;
            BuildUI (parent);
            ResetPreview ();
            LoadPreferences ();
            ScanSources ();
            ConnectEvents ();
        }

        void BuildUI (Window parent)
        {
            TransientFor = parent;
            WindowPosition = WindowPosition.CenterOnParent;

            photo_view = new PhotoImageView (Controller.Photos);
            photo_scrolled.Add (photo_view);
            photo_scrolled.SetSizeRequest (200, 200);
            photo_view.Show ();

            GtkUtil.ModifyColors (photo_scrolled);
            GtkUtil.ModifyColors (photo_view);

            var tray = new ScalingIconView (Controller.Photos);
            tray.Selection.Changed += (c) => {
                if (tray.Selection.Count > 0)
                    photo_view.Item.Index = tray.Selection.Ids[0];
            };
            icon_scrolled.Add (tray);
            tray.DisplayTags = false;
            tray.Show ();

            progress_bar.Hide ();

            import_button.Sensitive = false;

            tag_entry = new TagEntry (App.Instance.Database.Tags, false);
            tag_entry.UpdateFromTagNames (new string []{});
            tagentry_box.Add (tag_entry);
            tag_entry.Show ();
            attachtags_label.MnemonicWidget = tag_entry;
        }

        void ResetPreview ()
        {
            photo_view.Pixbuf = GtkUtil.TryLoadIcon (FSpot.Core.Global.IconTheme, "f-spot", 128, (Gtk.IconLookupFlags)0);
            photo_view.ZoomFit (false);
        }

        void LoadPreferences ()
        {
            if (Preferences.Get<int> (Preferences.IMPORT_WINDOW_WIDTH) > 0) {
                Resize (Preferences.Get<int> (Preferences.IMPORT_WINDOW_WIDTH), Preferences.Get<int> (Preferences.IMPORT_WINDOW_HEIGHT));
            }

            if (FSpot.Preferences.Get<int> (Preferences.IMPORT_WINDOW_PANE_POSITION) > 0) {
                import_hpaned.Position = Preferences.Get<int> (Preferences.IMPORT_WINDOW_PANE_POSITION);
            }

            copy_check.Active = Controller.CopyFiles;
            recurse_check.Active = Controller.RecurseSubdirectories;
            duplicate_check.Active = Controller.DuplicateDetect;
            remove_check.Active = Controller.RemoveOriginals;
            remove_check.Sensitive = copy_check.Active;
            remove_warning_button.Sensitive = copy_check.Active && remove_check.Active;
        }

        void ScanSources ()
        {
            // Populates the source combo box
            Sources = new TreeStore (typeof(ImportSource), typeof(string), typeof(string), typeof(bool));
            sources_combo.Model = Sources;
            sources_combo.RowSeparatorFunc = (m, i) => (m.GetValue (i, 1) as string) == String.Empty;
            var render = new CellRendererPixbuf ();
            sources_combo.PackStart (render, false);
            sources_combo.SetAttributes (render, "icon-name", 2, "sensitive", 3);
            var render2 = new CellRendererText ();
            sources_combo.PackStart (render2, true);
            sources_combo.SetAttributes (render2, "text", 1, "sensitive", 3);

            GLib.Idle.Add (() => {
                PopulateSourceCombo (null);
                QueueDraw ();
                return false;
            });
        }

        void PopulateSourceCombo (ImportSource source_to_activate)
        {
            int activate_index = 0;
            sources_combo.Changed -= OnSourceComboChanged;
            Sources.Clear ();
            Sources.AppendValues (null, Catalog.GetString ("Choose Import source..."), String.Empty, true);
            Sources.AppendValues (null, select_folder_label, "folder", true);
            Sources.AppendValues (null, String.Empty, String.Empty);
            bool mount_added = false;
            foreach (var source in Controller.Sources) {
                if (source == source_to_activate) {
                    activate_index = Sources.IterNChildren ();
                }
                Sources.AppendValues (source, source.Name, source.IconName, true);
                mount_added = true;
            }
            if (!mount_added) {
                Sources.AppendValues (null, Catalog.GetString ("(No Cameras Detected)"), String.Empty, false);
            }

            if (history_sources.Count > 0) {
                Sources.AppendValues (null, String.Empty, String.Empty);
                foreach (var source in history_sources.Values) {
                    if (source == source_to_activate) {
                        activate_index = Sources.IterNChildren ();
                    }
                    Sources.AppendValues (source, source.Name, source.IconName, true);
                }
            }
            sources_combo.Changed += OnSourceComboChanged;
            sources_combo.Active = activate_index;
        }

        void ConnectEvents ()
        {
            Controller.StatusEvent += OnControllerStatusEvent;
            Controller.ProgressUpdated += OnControllerProgressUpdated;
            copy_check.Toggled += (o, args) => {
                Controller.CopyFiles = copy_check.Active;
                remove_check.Sensitive = copy_check.Active;
                remove_warning_button.Sensitive = copy_check.Active && remove_check.Active;
            };
            recurse_check.Toggled += (o, args) => { Controller.RecurseSubdirectories = recurse_check.Active; };
            duplicate_check.Toggled += (o, args) => { Controller.DuplicateDetect = duplicate_check.Active; };
            remove_check.Toggled += (o, args) => {
                Controller.RemoveOriginals = remove_check.Active;
                remove_warning_button.Sensitive = copy_check.Active && remove_check.Active;
            };
            import_button.Clicked += (o, args) => StartImport ();
            cancel_button.Clicked += (o, args) => CancelImport ();
            remove_warning_button.Clicked += (o, args) => {
                var dialog = new MessageDialog (this, DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok, true,
                        Catalog.GetString ("Checking this box will remove the imported photos from the camera after the import finished successfully.\n\nIt is generally recommended to backup your photos before removing them from the camera. <b>Use this option at your own risk!</b>"));
                dialog.Title = Catalog.GetString ("Warning");
                dialog.Response += (s, arg) => {
                    dialog.Destroy ();
                };
                dialog.Run ();
            };
            Response += (o, args) => {
                if (args.ResponseId == ResponseType.DeleteEvent) {
                    CancelImport ();
                }
            };
        }

        void ShowFolderSelector ()
        {
            var file_chooser = new FileChooserDialog (
                Catalog.GetString ("Import"), this,
                FileChooserAction.SelectFolder,
                Stock.Cancel, ResponseType.Cancel,
                Stock.Open, ResponseType.Ok);

            file_chooser.SelectMultiple = false;
            file_chooser.LocalOnly = false;

            int response = file_chooser.Run ();
            if ((ResponseType) response == ResponseType.Ok) {
                var uri = new SafeUri (file_chooser.Uri, true);
                SwitchToFolderSource (uri);
            }

            file_chooser.Destroy ();
        }

        public void SwitchToFolderSource (SafeUri uri)
        {
            ImportSource source = null;
            if (!history_sources.TryGetValue (uri, out source)) {
                var name = uri.GetFilename ();
                source = new FileImportSource (uri, name, "folder");
                history_sources[uri] = source;
            }

            PopulateSourceCombo (source);
            Controller.ActiveSource = source;
        }

        int current_index = -1;
        void OnSourceComboChanged (object sender, EventArgs args)
        {
            // Prevent double firing.
            if (sources_combo.Active == current_index) {
                Log.Debug ("Skipping double fire!");
                return;
            } else {
                current_index = sources_combo.Active;
            }

            TreeIter iter;
            sources_combo.GetActiveIter (out iter);
            var source = Sources.GetValue (iter, 0) as ImportSource;
            if (source == null) {
                var label = (string) Sources.GetValue (iter, 1);
                if (label == select_folder_label) {
                    ShowFolderSelector ();
                    return;
                } else {
                    sources_combo.Active = 0;
                    return;
                }
            }
            Controller.ActiveSource = source;
        }

        void OnControllerStatusEvent (ImportEvent evnt)
        {
            Log.DebugFormat ("Received controller event: {0}", evnt);

            switch (evnt) {
                case ImportEvent.SourceChanged:
                    HideScanSpinner ();
                    ResetPreview ();
                    import_button.Sensitive = true;
                    break;

                case ImportEvent.PhotoScanStarted:
                    ShowScanSpinner ();
                    break;

                case ImportEvent.PhotoScanFinished:
                    HideScanSpinner ();
                    break;

                case ImportEvent.ImportStarted:
                    ShowImportProgress ();
                    break;

                case ImportEvent.ImportFinished:
                    ShowFailuresIfNeeded (Controller.FailedImports);
                    Controller = null;
                    Destroy ();
                    break;

                case ImportEvent.ImportError:
                    // TODO
                    break;
            }
        }

        void ShowFailuresIfNeeded (List<SafeUri> files)
        {
            if (Controller.FailedImports.Count == 0)
                return;

            new ImportFailureDialog (files).Show ();
        }

        void OnControllerProgressUpdated (int current, int total)
        {
            var importing_label = Catalog.GetString ("Importing Photos: {0} of {1}...");
            progress_bar.Text = String.Format (importing_label, current, total);
            progress_bar.Fraction = (double) current / Math.Max (total, 1);
        }

        void StartImport ()
        {
            Controller.AttachTags (tag_entry.GetTypedTagNames ());
            Controller.StartImport ();
            import_button.Sensitive = false;
            OptionsSensitive = false;
        }

        void CancelImport ()
        {
            Controller.CancelImport ();
            Controller = null;
            Destroy ();
        }

        bool pulse_timeout_running = false;

        void ShowImportProgress ()
        {
            progress_bar.Text = Catalog.GetString ("Importing photos...");
            progress_bar.Show ();
        }

        void ShowScanSpinner ()
        {
            // TODO: Using a GtkSpinner would be nicer here.
            progress_bar.Text = Catalog.GetString ("Searching for photos... (You can already click Import to continue)");
            progress_bar.Show ();
            pulse_timeout_running = true;
            GLib.Timeout.Add (40, () => {
                if (!pulse_timeout_running) {
                    return false;
                }

                progress_bar.Pulse ();
                return pulse_timeout_running;
            });
        }

        void HideScanSpinner ()
        {
            pulse_timeout_running = false;
            progress_bar.Hide ();
        }

        public bool OptionsSensitive
        {
            set {
                sources_combo.Sensitive = value;
                copy_check.Sensitive = value;
                recurse_check.Sensitive = value;
                duplicate_check.Sensitive = value;
                tagentry_box.Sensitive = value;
            }
        }
    }
}