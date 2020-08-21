using System;
using System.Collections.Generic;
using System.Globalization;
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
using System.Windows.Shapes;

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

        public MainWindow()
        {
            var args = Environment.GetCommandLineArgs();
            GameData.GameData? gameData = null;
            Title = "Dat editing thing";
            if (args.Length > 1)
            {
                try
                {
                    gameData = GameData.GameData.Open(args[1]);
                    Opened(args[1]);
                }
                catch (Exception e)
                {
                    gameData = null;
                    MessageBox.Show(this, FormatException(e), "Opening data files failed");
                }
            }
            this.state = new AppState(gameData);
            UpdateTitle();
            foreach ((var type, var dat) in this.state.IterDats())
            {
                dat.FieldChanged += (obj, e) => this.UpdateTitle();
            }
            InitializeComponent();
            this.DataContext = state;
            this.rootTab.SelectionChanged += (e, args) => {
                // Tab selection events get sent a lot more often than just when the user
                // changes tab??
                if (this.currentTab == rootTab.SelectedIndex)
                {
                    return;
                }
                this.currentTab = rootTab.SelectedIndex;

                switch (rootTab.SelectedIndex)
                {
                    case 0:
                        state.SelectDat(ArrayFileType.Units);
                        break;
                    case 1:
                        state.SelectDat(ArrayFileType.Weapons);
                        break;
                    case 2:
                        state.SelectDat(ArrayFileType.Flingy);
                        break;
                    case 3:
                        state.SelectDat(ArrayFileType.Sprites);
                        break;
                    case 4:
                        state.SelectDat(ArrayFileType.Images);
                        break;
                    case 5:
                        state.SelectDat(ArrayFileType.Upgrades);
                        break;
                    case 6:
                        state.SelectDat(ArrayFileType.TechData);
                        break;
                    case 7:
                        state.SelectDat(ArrayFileType.Orders);
                        break;
                    default:
                        return;
                }

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
            int tab;
            switch (type)
            {
                case ArrayFileType.Units:
                    tab = 0;
                    break;
                case ArrayFileType.Weapons:
                    tab = 1;
                    break;
                case ArrayFileType.Flingy:
                    tab = 2;
                    break;
                case ArrayFileType.Sprites:
                    tab = 3;
                    break;
                case ArrayFileType.Images:
                    tab = 4;
                    break;
                case ArrayFileType.Upgrades:
                    tab = 5;
                    break;
                case ArrayFileType.TechData:
                    tab = 6;
                    break;
                case ArrayFileType.Orders:
                    tab = 7;
                    break;
                default:
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

        string FormatException(Exception e)
        {
            var result = new StringBuilder();
            while (true)
            {
                result.Append($"{e.Message}\n");
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
                    Int32.Parse(text.Substring(2), NumberStyles.HexNumber) :
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
            int startIndex = entryList.SelectedIndex;
            if (startIndex < 0)
            {
                startIndex = 0;
            }
            int i = startIndex + 1;
            while (i != startIndex)
            {
                if (names[i].Text.ToLowerInvariant().Contains(text))
                {
                    entryList.SelectedIndex = i;
                    entryList.ScrollIntoView(entryList.Items[(int)i]);
                    return;
                }
                i += 1;
                // Skip past disabled names and loop back to 0
                while (true)
                {
                    if (i >= names.Count)
                    {
                        i = 0;
                    }
                    if (!names[i].Enabled)
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
        }

        void OpenCmdCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
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
    }
}
