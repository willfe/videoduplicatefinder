using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DuplicateFinderEngine;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace VideoDuplicateFinderLinux
{
    public sealed class MainWindowVM : ReactiveObject
    {

        public ScanEngine Scanner { get; } = new ScanEngine();
        public ObservableCollection<LogItem> LogItems { get; } = new ObservableCollection<LogItem>();
        public ObservableCollection<string> Includes { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Blacklists { get; } = new ObservableCollection<string>();
        public ObservableCollection<DuplicateItemViewModel> Duplicates { get; } =
            new ObservableCollection<DuplicateItemViewModel>();

        bool _IsScanning;
        public bool IsScanning
        {
            get => _IsScanning;
            set => this.RaiseAndSetIfChanged(ref _IsScanning, value);
        }

        bool _IgnoreReadOnlyFolders;
        public bool IgnoreReadOnlyFolders
        {
            get => _IgnoreReadOnlyFolders;
            set => this.RaiseAndSetIfChanged(ref _IgnoreReadOnlyFolders, value);
        }

        bool _IncludeSubDirectories = true;
        public bool IncludeSubDirectories
        {
            get => _IncludeSubDirectories;
            set => this.RaiseAndSetIfChanged(ref _IncludeSubDirectories, value);
        }
        bool _IncludeImages = true;
        public bool IncludeImages {
	        get => _IncludeImages;
	        set => this.RaiseAndSetIfChanged(ref _IncludeImages, value);
        }
		string _ScanProgressText;
        public string ScanProgressText
        {
            get => _ScanProgressText;
            set => this.RaiseAndSetIfChanged(ref _ScanProgressText, value);
        }
        TimeSpan _RemainingTime;
        public TimeSpan RemainingTime
        {
            get => _RemainingTime;
            set => this.RaiseAndSetIfChanged(ref _RemainingTime, value);
        }
        private TimeSpan _TimeElapsed;
        public TimeSpan TimeElapsed
        {
            get => _TimeElapsed;
            set => this.RaiseAndSetIfChanged(ref _TimeElapsed, value);
        }
        int _ScanProgressValue;
        public int ScanProgressValue
        {
            get => _ScanProgressValue;
            set => this.RaiseAndSetIfChanged(ref _ScanProgressValue, value);
        }
        int _ScanProgressMaxValue = 100;
        public int ScanProgressMaxValue
        {
            get => _ScanProgressMaxValue;
            set => this.RaiseAndSetIfChanged(ref _ScanProgressMaxValue, value);
        }

        int _Percent = 95;
        public int Percent
        {
            get => _Percent;
            set => this.RaiseAndSetIfChanged(ref _Percent, value);
        }
        public MainWindowVM()
        {
            var dir = new DirectoryInfo(Utils.ThumbnailDirectory);
            if (!dir.Exists)
                dir.Create();
            Scanner.ScanDone += Scanner_ScanDone;
            Scanner.Progress += Scanner_Progress;
            Logger.Instance.LogItemAdded += Instance_LogItemAdded;
        }

        public void SaveSettings()
        {
            var path = DuplicateFinderEngine.Utils.SafePathCombine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Settings.xml");
            var includes = new object[Includes.Count];
            for (var i = 0; i < Includes.Count; i++)
            {
                includes[i] = new XElement("Include", Includes[i]);
            }
            var excludes = new object[Blacklists.Count];
            for (var i = 0; i < Blacklists.Count; i++)
            {
                excludes[i] = new XElement("Exclude", Blacklists[i]);
            }

            var xDoc = new XDocument(new XElement("Settings",
                    new XElement("Includes", includes),
                    new XElement("Excludes", excludes),
                    new XElement("Percent", Percent),
                    new XElement("IncludeSubDirectories", IncludeSubDirectories),
                    new XElement("IncludeImages", IncludeImages),
					new XElement("IgnoreReadOnlyFolders", IgnoreReadOnlyFolders)
                )
            );
            xDoc.Save(path);
        }
        public void LoadSettings()
        {
            var path = DuplicateFinderEngine.Utils.SafePathCombine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Settings.xml");
            if (!File.Exists(path)) return;
            Includes.Clear();
            Blacklists.Clear();
            var xDoc = XDocument.Load(path);
            foreach (var n in xDoc.Descendants("Include"))
                Includes.Add(n.Value);
            foreach (var n in xDoc.Descendants("Exclude"))
                Blacklists.Add(n.Value);
            var node = xDoc.Descendants("IncludeSubDirectories").SingleOrDefault();
            if (node?.Value != null)
                IncludeSubDirectories = bool.Parse(node.Value);
            node = xDoc.Descendants("IncludeImages").SingleOrDefault();
            if (node?.Value != null)
	            IncludeImages = bool.Parse(node.Value);
			node = xDoc.Descendants("IgnoreReadOnlyFolders").SingleOrDefault();
            if (node?.Value != null)
                IgnoreReadOnlyFolders = bool.Parse(node.Value);
        }
        private void Scanner_Progress(object sender, ScanEngine.OwnScanProgress e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScanProgressText = e.CurrentFile;
                RemainingTime = e.Remaining;
                ScanProgressValue = e.CurrentPosition;
                TimeElapsed = e.Elapsed;
                ScanProgressMaxValue = Scanner.ScanProgressMaxValue;
            });
        }

        private void Instance_LogItemAdded(object sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                while (Logger.Instance.LogEntries.Count > 0)
                {
                    if (Logger.Instance.LogEntries.TryTake(out var item))
                        LogItems.Add(item);
                }
            });
        }

        private void Scanner_ScanDone(object sender, EventArgs e)
        {
            //Reset properties
            ScanProgressText = string.Empty;
            RemainingTime = new TimeSpan();
            ScanProgressValue = 0;
            ScanProgressMaxValue = 100;

            //In Linux we cannot group, so let's make sure its sorted
            var l = new SortedSet<DuplicateFinderEngine.Data.DuplicateItem>(Scanner.Duplicates, new DuplicateItemComparer());
            //We no longer need the core duplicates
            Scanner.Duplicates.Clear();

            foreach (var itm in l)
            {
                var dup = new DuplicateItemViewModel(itm);
                //Set best property in duplicate group
                var others = Scanner.Duplicates.Where(a => a.GroupId == dup.GroupId && a.Path != dup.Path).ToList();
                dup.SizeBest = !others.Any(a => a.SizeLong < dup.SizeLong);
                dup.FrameSizeBest = !others.Any(a => a.FrameSizeInt > dup.FrameSizeInt);
                //dup.DurationBest = !others.Any(a => a.Duration.TrimMiliseconds() > dup.Duration.TrimMiliseconds());
                dup.BitrateBest = !others.Any(a => a.BitRateKbs > dup.BitRateKbs);
                Duplicates.Add(dup);
            }

            //And done
            IsScanning = false;
        }

        public ReactiveCommand AddIncludesToListCommand => ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await new OpenFolderDialog
            {
                Title = Properties.Resources.SelectFolder
            }.ShowAsync(Application.Current.MainWindow);
            if (string.IsNullOrEmpty(result)) return;
            if (!Includes.Contains(result))
                Includes.Add(result);
        });
        public ReactiveCommand<ListBox, Action> RemoveIncludesFromListCommand => ReactiveCommand.Create<ListBox, Action>(lbox =>
        {
            while (lbox.SelectedItems.Count > 0)
                Includes.Remove((string)lbox.SelectedItems[0]);
            return null;
        });
        public ReactiveCommand AddBlacklistToListCommand => ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await new OpenFolderDialog
            {
                Title = Properties.Resources.SelectFolder
            }.ShowAsync(Application.Current.MainWindow);
            if (string.IsNullOrEmpty(result)) return;
            if (!Blacklists.Contains(result))
                Blacklists.Add(result);
        });
        public ReactiveCommand<ListBox, Action> RemoveBlacklistFromListCommand => ReactiveCommand.Create<ListBox, Action>(lbox =>
        {
            while (lbox.SelectedItems.Count > 0)
                Blacklists.Remove((string)lbox.SelectedItems[0]);
            return null;
        });
        public ReactiveCommand ClearLogCommand => ReactiveCommand.Create(() => { LogItems.Clear(); });
        public ReactiveCommand SaveLogCommand => ReactiveCommand.CreateFromTask(async () =>
        {
	        var result = await new SaveFileDialog().ShowAsync(Application.Current.MainWindow);
			if (string.IsNullOrEmpty(result)) return;
			var sb = new StringBuilder();
            foreach (var l in LogItems)
                sb.AppendLine(l.ToString());
            try {
	            File.WriteAllText(result, sb.ToString());
			}
            catch (Exception e) {
	            Logger.Instance.Info(e.Message);
            }
        });


        public ReactiveCommand StartScanCommand => ReactiveCommand.Create(() =>
        {
            Duplicates.Clear();
            try
            {
                foreach (var f in new DirectoryInfo(Utils.ThumbnailDirectory).EnumerateFiles())
                    f.Delete();
            }
            catch (Exception e)
            {
                Logger.Instance.Info(e.Message);
                return;
            }
            IsScanning = true;
            //Set scan settings
            Scanner.Settings.IncludeSubDirectories = IncludeSubDirectories;
            Scanner.Settings.IncludeImages = IncludeImages;
			Scanner.Settings.IgnoreReadOnlyFolders = IgnoreReadOnlyFolders;
            Scanner.Settings.Percent = Percent;
            Scanner.Settings.IncludeList.Clear();
            foreach (var s in Includes)
                Scanner.Settings.IncludeList.Add(s);
            Scanner.Settings.BlackList.Clear();
            foreach (var s in Blacklists)
                Scanner.Settings.BlackList.Add(s);
            //Start scan
            Scanner.StartSearch();
        });
        public ReactiveCommand CheckWhenIdenticalCommand => ReactiveCommand.Create(() =>
        {
            var blackListGroupID = new HashSet<Guid>();
            foreach (var first in Duplicates)
            {
                if (blackListGroupID.Contains(first.GroupId)) continue; //Dup has been handled already
                var l = Duplicates.Where(d => d.Equals(first) && !d.Path.Equals(first.Path));
                var dupMods = l as DuplicateItemViewModel[] ?? l.ToArray();
                if (!dupMods.Any()) continue;
                foreach (var dup in dupMods)
                    dup.Checked = true;
                first.Checked = false;
                blackListGroupID.Add(first.GroupId);
            }
        });
        public ReactiveCommand CheckWhenIdenticalButSizeCommand => ReactiveCommand.Create(() =>
        {
            var blackListGroupID = new HashSet<Guid>();
            foreach (var first in Duplicates)
            {
                if (blackListGroupID.Contains(first.GroupId)) continue; //Dup has been handled already
                var l = Duplicates.Where(d => d.EqualsButSize(first) && !d.Path.Equals(first.Path));
                var dupMods = l as List<DuplicateItemViewModel> ?? l.ToList();
                if (!dupMods.Any()) continue;
                dupMods.Add(first);
                dupMods = dupMods.OrderBy(s => s.SizeLong).ToList();
                dupMods[0].Checked = false;
                for (int i = 1; i < dupMods.Count; i++)
                {
                    dupMods[i].Checked = true;
                }
                blackListGroupID.Add(first.GroupId);
            }
        });
        public ReactiveCommand CheckLowestQualityCommand => ReactiveCommand.Create(() =>
        {
            var blackListGroupID = new HashSet<Guid>();
            foreach (var first in Duplicates)
            {
                if (blackListGroupID.Contains(first.GroupId)) continue; //Dup has been handled already
                var l = Duplicates.Where(d => d.EqualsButQuality(first) && !d.Path.Equals(first.Path));
                var dupMods = l as List<DuplicateItemViewModel> ?? l.ToList();
                if (!dupMods.Any()) continue;
                dupMods.Insert(0, first);

                var keep = dupMods[0];
                //TODO: Make this order become an option for the user
                //Duration first
                for (int i = 1; i < dupMods.Count; i++)
                {
                    if (dupMods[i].Duration.TrimMiliseconds() > keep.Duration.TrimMiliseconds())
                        keep = dupMods[i];
                }
                //resolution next, but only when keep is unchanged
                if (keep.Path.Equals(dupMods[0].Path))
                    for (int i = 1; i < dupMods.Count; i++)
                    {
                        if (dupMods[i].Fps > keep.Fps)
                            keep = dupMods[i];
                    }
                //fps next, but only when keep is unchanged
                if (keep.Path.Equals(dupMods[0].Path))
                    for (int i = 1; i < dupMods.Count; i++)
                    {
                        if (dupMods[i].Fps > keep.Fps)
                            keep = dupMods[i];
                    }
                //Bitrate next, but only when keep is unchanged
                if (keep.Path.Equals(dupMods[0].Path))
                    for (int i = 1; i < dupMods.Count; i++)
                    {
                        if (dupMods[i].BitRateKbs > keep.BitRateKbs)
                            keep = dupMods[i];
                    }
                //Audio Bitrate next, but only when keep is unchanged
                if (keep.Path.Equals(dupMods[0].Path))
                    for (int i = 1; i < dupMods.Count; i++)
                    {
                        if (dupMods[i].AudioSampleRate > keep.AudioSampleRate)
                            keep = dupMods[i];
                    }

                keep.Checked = false;
                for (int i = 0; i < dupMods.Count; i++)
                {
                    if (!keep.Path.Equals(dupMods[i].Path))
                        dupMods[i].Checked = true;
                }

                blackListGroupID.Add(first.GroupId);
            }
        });
        public ReactiveCommand ClearSelectionCommand => ReactiveCommand.Create(() =>
        {
            for (var i = 0; i < Duplicates.Count; i++)
                Duplicates[i].Checked = false;
        });
        public ReactiveCommand DeleteSelectionCommand => ReactiveCommand.Create(() => { DeleteInternal(true); });
        public ReactiveCommand RemoveSelectionFromListCommand => ReactiveCommand.Create(() => { DeleteInternal(false); });

        void DeleteInternal(bool fromDisk)
        {
            if (Duplicates.Count == 0) return;
            //TODO: Confirmation prompt?
            for (var i = Duplicates.Count - 1; i >= 0; i--)
            {
                var dub = Duplicates[i];
                if (dub.Checked == false) continue;
                if (fromDisk)
                    try
                    {
                        File.Delete(dub.Path);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Info(string.Format(Properties.Resources.FailedToDeleteFileReasonStacktrace,
                            dub.Path, ex.Message, ex.StackTrace));
                    }
                Duplicates.RemoveAt(i);
            }
            //Hide groups with just one item left
            for (var i = Duplicates.Count - 1; i >= 0; i--)
            {
                var first = Duplicates[i];
                if (Duplicates.Any(s => s.GroupId == first.GroupId && s.Path != first.Path)) continue;
                Duplicates.RemoveAt(i);
            }
        }
        public ReactiveCommand CopySelectionCommand => ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await new OpenFolderDialog
            {
                Title = Properties.Resources.SelectFolder
            }.ShowAsync(Application.Current.MainWindow);
            if (string.IsNullOrEmpty(result)) return;
            FileHelper.CopyFile(Duplicates.Where(s => s.Checked).Select(s => s.Path), result, true, false,
                out _);
        });
        public ReactiveCommand MoveSelectionCommand => ReactiveCommand.CreateFromTask(async () => {
	        var result = await new OpenFolderDialog {
		        Title = Properties.Resources.SelectFolder
	        }.ShowAsync(Application.Current.MainWindow);
	        if (string.IsNullOrEmpty(result)) return;
		FileHelper.CopyFile(Duplicates.Where(s => s.Checked).Select(s => s.Path), result, true, true,
		        out _);
        });

		public ReactiveCommand SaveToHtmlCommand => ReactiveCommand.CreateFromTask(async (a) =>
        {
            if (Scanner == null || Duplicates.Count == 0) return;
            var ofd = new SaveFileDialog
            {
                Title = Properties.Resources.SaveDuplicates,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter
                    {
                        Name = "Html",
                        Extensions = new List<string> { "*.html" }
                    }
               }
            };

            var file = await ofd.ShowAsync(Application.Current.MainWindow);
            if (string.IsNullOrEmpty(file)) return;
            try
            {
                Duplicates.ToHtmlTable(file);
            }
            catch (Exception e)
            {
                Logger.Instance.Info(e.Message + Environment.NewLine + Environment.NewLine + e.StackTrace);
            }
        });

    }
}
