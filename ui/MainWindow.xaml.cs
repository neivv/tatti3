using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using ArrayFileType = Tatti3.GameData.ArrayFileType;

namespace Tatti3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static RoutedCommand JumpCommand = new RoutedCommand();
        public static RoutedCommand NewEntryCommand = new RoutedCommand();
        public static RoutedCommand CopyEntryCommand = new RoutedCommand();
        public static RoutedCommand PasteEntryCommand = new RoutedCommand();
        public static RoutedCommand PasteNewEntryCommand = new RoutedCommand();

        static readonly ArrayFileType[] TabOrder = {
            ArrayFileType.Units,
            ArrayFileType.Weapons,
            ArrayFileType.Flingy,
            ArrayFileType.Sprites,
            ArrayFileType.Images,
            ArrayFileType.Upgrades,
            ArrayFileType.TechData,
            ArrayFileType.PortData,
            ArrayFileType.MapData,
            ArrayFileType.Orders,
            ArrayFileType.Buttons,
        };

        public MainWindow()
        {
            var args = Environment.GetCommandLineArgs();
            GameData.GameData? gameData = null;
            Title = "Dat editing thing";
            this.state = new AppState(gameData);
            this.DataContext = state;
            InitializeComponent();
            if (args.Length > 1)
            {
                this.OpenDat(args[1]);
            }
            this.rootTab.SelectionChanged += (e, args) => {
                // Tab selection events get sent a lot more often than just when the user
                // changes tab??
                if (this.currentTab == rootTab.SelectedIndex)
                {
                    return;
                }
                this.currentTab = rootTab.SelectedIndex;

                if (rootTab.SelectedIndex >= TabOrder.Length) {
                    return;
                }
                state.SelectDat(TabOrder[rootTab.SelectedIndex]);

                int index = AppState.DatFileTypeToIndex(state.CurrentDat);
                var selections = state.Selections;
                entryList.SelectedIndex = selections[index];
                if (entryList.SelectedIndex != -1)
                {
                    entryList.ScrollIntoView(entryList.Items[entryList.SelectedIndex]);
                }
            };
            this.entryList.SelectionChanged += (e, args) => {
                if (entryList.SelectedIndex == -1)
                {
                    return;
                }
                int index = AppState.DatFileTypeToIndex(state.CurrentDat);
                var selections = state.Selections;
                selections[index] = entryList.SelectedIndex;
                state.Selections = selections;
                // See comment at App.xaml.cs
                System.Runtime.InteropServices.Marshal.CleanupUnusedObjectsInCurrentContext();
            };
            rootTab.SelectedIndex = 0;
        }

        void OpenDat(string path)
        {
            GameData.GameData? gameData = null;
            try
            {
                gameData = GameData.GameData.Open(path);
            }
            catch (Exception e)
            {
                gameData = null;
                MessageBox.Show(this, FormatException(e), "Opening data files failed");
            }
            Opened(path);
            this.state = new AppState(gameData);
            this.DataContext = this.state;
            UpdateTitle();
            foreach ((var type, var dat) in this.state.IterDats())
            {
                dat.FieldChanged += (obj, e) => this.UpdateTitle();
            }
            if (rootTab.SelectedIndex >= 0 && rootTab.SelectedIndex < TabOrder.Length) {
                state.SelectDat(TabOrder[rootTab.SelectedIndex]);
                int index = AppState.DatFileTypeToIndex(state.CurrentDat);
                entryList.SelectedIndex = state.Selections[index];
            }
        }

        void GotoBackRef(object sender, MouseButtonEventArgs e)
        {
            var selIndex = backRefList.SelectedIndex;
            if (selIndex < 0)
            {
                return;
            }
            var state = (AppState)DataContext;
            (var type, var index) = state.CurrentBackRefs.Set.ElementAt(selIndex);
            JumpToEntry(type, index);
        }

        void JumpToEntry(ArrayFileType type, uint index_)
        {
            var index = (int)index_;
            // These names happen to tell whether the entry is disabled
            var names = state.IndexPrefixedArrayFileNames(type);
            if (names.Count <= index || !names[index].Enabled)
            {
                return;
            }
            int tab = Array.IndexOf(TabOrder, type);
            if (tab == -1) {
                return;
            }
            rootTab.SelectedIndex = tab;
            entryList.SelectedIndex = index;
            entryList.Focus();
            entryList.ScrollIntoView(entryList.Items[index]);
        }

        void Opened(string root)
        {
            this.root = root;
        }

        void UpdateTitle()
        {
            if (root == "") {
                Title = $"Dat editing thing";
            } else {
                if (state.IsDirty)
                {
                    Title = $"Dat editing thing - {root}*";
                }
                else
                {
                    Title = $"Dat editing thing - {root}";
                }
            }
        }

        static string FormatException(Exception e)
        {
            var result = new StringBuilder();
            while (true)
            {
                result.Append($"{e.GetType().Name} {e.Message}\n");
                if (e.InnerException == null)
                {
                    result.Append($"\n{e.StackTrace}");
                    break;
                }
                e = e.InnerException;
                result.Append("Caused by: ");
            }
            return result.ToString();
        }

        AppState state;
        int currentTab = -1;
        string root = "";

        void OnIdGotoButtonClicked(object sender, RoutedEventArgs e)
        {
            DoGoto();
        }

        void OnIdGotoBoxKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                DoGoto();
            }
        }

        void DoGoto()
        {
            string text = idJumpBox.Text;
            int index;
            try
            {
                index = text.StartsWith("0x") ?
                    Int32.Parse(text[2..], NumberStyles.HexNumber) :
                    Int32.Parse(text);
            }
            catch (Exception)
            {
                return;
            }
            // These names happen to tell whether the entry is disabled
            var names = state.IndexPrefixedArrayFileNames(state.CurrentDat);
            if (names.Count <= index || !names[index].Enabled)
            {
                return;
            }
            entryList.SelectedIndex = index;
            entryList.ScrollIntoView(entryList.Items[(int)index]);
        }

        void OnSearchButtonClicked(object sender, RoutedEventArgs e)
        {
            DoSearch();
        }

        void OnSearchBoxKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                DoSearch();
            }
        }

        void DoSearch()
        {
            string text = searchBox.Text.ToLowerInvariant();
            if (text == "")
            {
                return;
            }
            var names = state.IndexPrefixedArrayFileNames(state.CurrentDat);
            if (names.Count == 0)
            {
                return;
            }

            int startIndex = entryList.SelectedIndex;
            if (startIndex < 0 || startIndex >= names.Count)
            {
                startIndex = 0;
            }
            int i = startIndex + 1;
            while (i != startIndex)
            {
                if (i >= names.Count)
                {
                    i = 0;
                }
                if (names[i].Text.ToLowerInvariant().Contains(text))
                {
                    entryList.SelectedIndex = i;
                    entryList.ScrollIntoView(entryList.Items[(int)i]);
                    return;
                }
                i += 1;
                // Skip past disabled names and loop back to 0
                while (i != startIndex)
                {
                    if (i >= names.Count)
                    {
                        i = 0;
                    }
                    else if (!names[i].Enabled)
                    {
                        i += 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        void OpenCmdExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = ".dat files|*.dat";
            dialog.Title = "Select one of the .dat files (all will be opened)";
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                string path = dialog.FileName;
                string? parent = Path.GetDirectoryName(path);
                if (parent == null)
                {
                    return;
                }
                string? parentDir = Path.GetFileName(parent);
                if (parentDir == null || parentDir.ToLowerInvariant() != "arr")
                {
                    MessageBox.Show(this, "This program can only open dat files in arr/ subdirectory of a mod", "Invalid path");
                } else
                {
                    string? root = Path.GetDirectoryName(parent);
                    if (root != null)
                    {
                        OpenDat(root);
                    }
                }
            }
        }

        void OpenCmdCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        void SaveCmdExecuted(object target, ExecutedRoutedEventArgs e)
        {
            // If the user had changed text in a textbox, unfocus and refocus it
            // to update AppState.
            var focused = FocusManager.GetFocusedElement(this);
            if (focused != null)
            {
                FocusManager.SetFocusedElement(this, null);
                focused.Focus();
            }
            if (root != "" && state.IsDirty)
            {
                try
                {
                    state.Save(root);
                    UpdateTitle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, FormatException(ex), "Saving data files failed");
                }
            }
        }

        void SaveCmdCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        void JumpCmdExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var (dat, entry) = ((ArrayFileType, uint))e.Parameter;
            JumpToEntry(dat, entry);
        }

        void JumpCmdCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        void NewEntryCmdExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var sourceEntry = (uint)entryList.SelectedIndex;
            var dat = state.GetDat(state.CurrentDat);
            if (dat == null)
            {
                return;
            }
            dat.DuplicateEntry(sourceEntry);
            var newIndex = entryList.Items.Count - 1;
            entryList.SelectedIndex = newIndex;
            entryList.ScrollIntoView(entryList.Items[newIndex]);
        }

        void NewEntryCmdCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        void CopyEntryCmdExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var sourceEntry = (uint)entryList.SelectedIndex;
            var dat = state.GetDat(state.CurrentDat);
            if (dat == null)
            {
                return;
            }
            var text = dat.SerializeEntryToJson(sourceEntry);
            Clipboard.SetText(text);
        }

        void CopyEntryCmdCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        void PasteEntryCmdExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var entry = (uint)entryList.SelectedIndex;
            var dat = state.GetDat(state.CurrentDat);
            if (dat == null)
            {
                return;
            }
            var text = Clipboard.GetText();
            if (dat.IsValidEntryJson(text))
            {
                dat.DeserializeEntryFromJson(entry, text);
            }
        }

        void PasteEntryCmdCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            var dat = state.GetDat(state.CurrentDat);
            if (dat == null)
            {
                return;
            }
            var text = Clipboard.GetText();
            e.CanExecute = dat.IsValidEntryJson(text);
        }

        void PasteNewEntryCmdExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var dat = state.GetDat(state.CurrentDat);
            if (dat == null)
            {
                return;
            }
            var text = Clipboard.GetText();
            if (!dat.IsValidEntryJson(text))
            {
                return;
            }
            dat.DuplicateEntry(0);
            var newIndex = entryList.Items.Count - 1;
            dat.DeserializeEntryFromJson((uint)newIndex, text);
            entryList.SelectedIndex = newIndex;
            entryList.ScrollIntoView(entryList.Items[newIndex]);
        }

        void PasteNewEntryCmdCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            PasteEntryCmdCanExecute(target, e);
        }
    }

    class LimitVerticalGrowth : Decorator
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            Child.Measure(new Size(availableSize.Width, Math.Min(availableSize.Height, height)));
            return new Size(Child.DesiredSize.Width, 0);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            height = arrangeSize.Height;
            if (Child.DesiredSize.Height > arrangeSize.Height)
            {
                Child.Measure(new Size(Child.DesiredSize.Width, arrangeSize.Height));
            }
            Child.Arrange(new Rect(arrangeSize));
            return arrangeSize;
        }

        double height = 1000000.0;
    }
}
