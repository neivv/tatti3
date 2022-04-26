using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Requirement = Tatti3.GameData.Requirement;
using ArrayFileType = Tatti3.GameData.ArrayFileType;

namespace Tatti3
{
    public partial class DatRequirements : UserControl
    {
        public DatRequirements()
        {
            InitializeComponent();
            this.DataContextChanged += (o, e) => this.UpdateBinding();
            this.selectRequirement.ItemsSource = OpcodeNames;
            this.requirementList.SelectionChanged += (e, args) => {
                if (requirementList.SelectedIndex == -1)
                {
                    return;
                }
                var req = ((RequirementList.RequirementWrap)
                    requirementList.Items[requirementList.SelectedIndex]).Value;
                var op = req.Opcode < 0xff00 ? 0 : req.Opcode;
                var index = OpcodeNames.FindIndex(x => x.Item1 == op);
                selectRequirement.SelectedIndex = index;
            };
            this.selectRequirement.SelectionChanged += (e, args) => {
                if (selectRequirement.SelectedIndex == -1 || requirementList.SelectedIndex == -1)
                {
                    return;
                }
                var reqListIndex = requirementList.SelectedIndex;
                var opcode = OpcodeNames[selectRequirement.SelectedIndex].Item1;
                var req = new Requirement(opcode);
                var dat = (AppState.DatTableRef)this.DataContext;
                if (dat == null)
                {
                    return;
                }
                var reqs = dat.GetRequirementsRef(offsetField, dataField).Requirements;
                var oldOpcode = reqs[reqListIndex].Opcode;
                if (oldOpcode == 0xffff)
                {
                    // Not allowing to edit "End"
                    return;
                }
                if (oldOpcode < 0xff00)
                {
                    oldOpcode = 0;
                }
                if (oldOpcode != req.Opcode)
                {
                    reqs[reqListIndex] = new RequirementList.RequirementWrap(req);
                }
                // Keep index selected even if the req changed
                requirementList.SelectedIndex = reqListIndex;
            };
        }

        public uint OffsetFieldId
        {
            get => offsetField;
            set
            {
                offsetField = value;
                UpdateBinding();
            }
        }

        public uint DataFieldId
        {
            get => dataField;
            set
            {
                dataField = value;
                UpdateBinding();
            }
        }

        void UpdateBinding()
        {
            var dat = (AppState.DatTableRef)this.DataContext;
            if (dataField == 0 || offsetField == 0 || dat == null)
            {
                return;
            }
            requirementList.DataContext = dat.GetRequirementsRef(offsetField, dataField).Requirements;
            var binding = new Binding();
            BindingOperations.SetBinding(requirementList, ListBox.ItemsSourceProperty, binding);
        }

        void OnAddClick(object sender, RoutedEventArgs e)
        {
            var pos = selectRequirement.SelectedIndex;
            UInt16 opcode = 0;
            if (pos != -1)
            {
                opcode = OpcodeNames[pos].Item1;
            }
            var list = (RequirementList)requirementList.DataContext;
            var req = new Requirement(opcode);
            list.Insert(list.Count, new RequirementList.RequirementWrap(req));
        }

        void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var pos = requirementList.SelectedIndex;
            if (pos == -1)
            {
                return;
            }
            var list = (RequirementList)requirementList.DataContext;
            var copy = list[pos].Value;
            list.Insert(pos + 1, new RequirementList.RequirementWrap(copy));
            requirementList.SelectedIndex = pos;
        }

        void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            // Swaps selected and one above it.
            // Don't allow swapping upgrade + end
            var pos = requirementList.SelectedIndex;
            if (pos < 1)
            {
                return;
            }
            if (Swap(pos - 1))
            {
                requirementList.SelectedIndex = pos - 1;
            }
        }

        void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            var pos = requirementList.SelectedIndex;
            if (pos == -1 || pos == requirementList.Items.Count - 1)
            {
                return;
            }
            if (Swap(pos))
            {
                requirementList.SelectedIndex = pos + 1;
            }
        }

        void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            var pos = requirementList.SelectedIndex;
            if (pos == -1)
            {
                return;
            }
            var list = (RequirementList)requirementList.DataContext;
            list.RemoveAt(pos);
            if (requirementList.Items.Count > pos)
            {
                requirementList.SelectedIndex = pos;
            }
            else if (requirementList.Items.Count > pos - 1)
            {
                requirementList.SelectedIndex = pos - 1;
            }
        }

        bool Swap(int pos)
        {
            var above = ((RequirementList.RequirementWrap)
                requirementList.Items[pos]).Value;
            var below = ((RequirementList.RequirementWrap)
                requirementList.Items[pos + 1]).Value;
            if ((above.IsEnd() && below.IsUpgradeLevelOpcode()) ||
                (above.IsUpgradeLevelOpcode() && below.IsEnd()))
            {
                return false;
            }
            var list = (RequirementList)requirementList.DataContext;
            list.Swap(pos, pos + 1);
            return true;
        }

        uint offsetField = 0;
        uint dataField = 0;

        public static List<(UInt16, string)> OpcodeNames { get; }
        public static Dictionary<UInt16, string> OpcodeNameDict { get; }

        static DatRequirements()
        {
            OpcodeNames = new List<(UInt16, string)>();
            OpcodeNameDict = new Dictionary<UInt16, string>();

            OpcodeNames.Add((0x0000, "Has unit..."));
            OpcodeNames.Add((0xff01, "*Or*"));
            OpcodeNames.Add((0xff02, "Current unit is..."));
            OpcodeNames.Add((0xff03, "Has unit [...] (Accept incomplete)"));
            OpcodeNames.Add((0xff04, "Has addon [...] attached"));
            OpcodeNames.Add((0xff05, "Is not lifted off"));
            OpcodeNames.Add((0xff06, "Is lifted off"));
            OpcodeNames.Add((0xff07, "Building is not busy"));
            OpcodeNames.Add((0xff08, "Is not constructing addon"));
            OpcodeNames.Add((0xff09, "Is not researching tech"));
            OpcodeNames.Add((0xff0a, "Is not upgrading"));
            OpcodeNames.Add((0xff0b, "Is not constructing building"));
            OpcodeNames.Add((0xff0c, "Has no addon"));
            OpcodeNames.Add((0xff0d, "Has no nydus exit"));
            OpcodeNames.Add((0xff0e, "Has hangar space"));
            OpcodeNames.Add((0xff0f, "Is researched"));
            OpcodeNames.Add((0xff10, "Has no nuke"));
            OpcodeNames.Add((0xff11, "Is unburrowed or AI controlled"));
            OpcodeNames.Add((0xff12, "Is not landed building"));
            OpcodeNames.Add((0xff13, "Is landed building"));
            OpcodeNames.Add((0xff14, "Can move"));
            OpcodeNames.Add((0xff15, "Can attack"));
            OpcodeNames.Add((0xff16, "Is worker"));
            OpcodeNames.Add((0xff17, "Can liftoff"));
            OpcodeNames.Add((0xff18, "Is transport"));
            OpcodeNames.Add((0xff19, "Is powerup"));
            OpcodeNames.Add((0xff1a, "Is subunit turret"));
            OpcodeNames.Add((0xff1b, "Has spider mines"));
            OpcodeNames.Add((0xff1c, "Is hero"));
            OpcodeNames.Add((0xff1d, "Can rally or right click"));
            OpcodeNames.Add((0xff1e, "*Allow on hallucinations*"));
            OpcodeNames.Add((0xff1f, "*Upgrade level 1 conditions*"));
            OpcodeNames.Add((0xff20, "*Upgrade level 2 conditions*"));
            OpcodeNames.Add((0xff21, "*Upgrade level 3+ conditions*"));
            OpcodeNames.Add((0xff22, "Always disabled"));
            OpcodeNames.Add((0xff23, "Always blank"));
            OpcodeNames.Add((0xff24, "Is Brood War"));
            OpcodeNames.Add((0xff25, "Tech [...] is researched"));
            OpcodeNames.Add((0xff26, "Is burrowed"));
            OpcodeNames.Add((0xff40, "Has less than [..] units (Including incomplete)"));
            foreach ((var op, var text) in OpcodeNames)
            {
                if (op != 0)
                {
                    OpcodeNameDict[op] = text;
                }
            }
        }
    }

    public class DatRequirementLine : UserControl
    {
        public DatRequirementLine()
        {
            panel = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
            };
            this.Content = panel;
            UpdateData();
        }

        void DataUpdated(DependencyPropertyChangedEventArgs args)
        {
            UpdateData();
        }

        void UpdateData()
        {
            if (RequirementData == null)
            {
                return;
            }
            var newParts = Parts((Requirement)RequirementData);
            if (newParts.Count != currentParts.Count)
            {
                currentParts = new List<Part>();
                panel.Children.Clear();
            }
            Requirement data = (Requirement)RequirementData;
            for (int i = 0; i < newParts.Count; i++)
            {
                var part = newParts[i];
                if (currentParts.Count > i && currentParts[i] == part)
                {
                    // No need to update
                    continue;
                }
                if (part.Text != null)
                {
                    TextDecorationCollection? decorations = null;
                    if (data.Opcode >= 0xff1f && data.Opcode <= 0xff21)
                    {
                        decorations = TextDecorations.Underline;
                    }
                    var text = new TextBlock
                    {
                        Text = part.Text,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextDecorations = decorations,
                    };
                    AddChild(i, text);
                }
                var paramIndex = part.ParamIndex;
                if (part.Dat != null)
                {
                    var dropdown = new ComboBox();
                    AddChild(i, dropdown);

                    var namesPath = $"DataContext.Root.Dat[{part.Dat}].IndexPrefixedNames";
                    var binding = new Binding
                    {
                        Path = new PropertyPath(namesPath),
                        RelativeSource = new RelativeSource
                        {
                            AncestorType = typeof(DatRequirements),
                        },
                        Mode = BindingMode.OneWay,
                        NotifyOnTargetUpdated = true,
                    };

                    EventHandler<DataTransferEventArgs> UpdateDropdownIndex = (obj, args) => {
                        if (this.RequirementData == null)
                        {
                            return;
                        }
                        var data = (Requirement)this.RequirementData;
                        dropdown.SelectedIndex =
                            data.Opcode < 0xff00 ? data.Opcode : data.Params[paramIndex];
                    };
                    Binding.AddTargetUpdatedHandler(dropdown, UpdateDropdownIndex);

                    BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, binding);
                    dropdown.SelectedIndex =
                        data.Opcode < 0xff00 ? data.Opcode : data.Params[paramIndex];
                    dropdown.SelectionChanged += (o, e) => {
                        var selection = dropdown.SelectedIndex;
                        if (selection != -1)
                        {
                            if (RequirementData is Requirement old)
                            {
                                Requirement newReq;
                                if (old.Opcode < 0xff00)
                                {
                                    newReq = new Requirement((UInt16)selection);
                                }
                                else
                                {
                                    newReq = new Requirement(old.Opcode);
                                    old.Params.CopyTo(newReq.Params, 0);
                                    newReq.Params[paramIndex] = (UInt16)selection;
                                }
                                if (old != newReq)
                                {
                                    RequirementData = newReq;
                                }
                            }
                        }
                    };
                }
                if (part.Uint16)
                {
                    var textbox = new TextBox();
                    AddChild(i, textbox);
                    var intValue = data.Params[paramIndex];
                    textbox.Text = intValue.ToString();
                    textbox.Width = 50;
                    textbox.MaxLength = 5;
                    textbox.TextChanged += (o, e) => {
                        if (RequirementData is Requirement old)
                        {
                            if (UInt16.TryParse(textbox.Text, out var result))
                            {
                                var newReq = new Requirement(old.Opcode);
                                old.Params.CopyTo(newReq.Params, 0);
                                newReq.Params[paramIndex] = (UInt16)result;
                                if (old != newReq)
                                {
                                    RequirementData = newReq;
                                }
                            }
                            else
                            {
                                if (textbox.Text.Trim() == "")
                                {
                                    textbox.Text = "0";
                                }
                                else
                                {
                                    textbox.Text = old.Params[paramIndex].ToString();
                                }
                            }
                        }
                    };
                }
            }
            currentParts = newParts;
        }

        void AddChild(int i, UIElement child)
        {
            if (panel.Children.Count > i)
            {
                panel.Children.RemoveAt(i);
                panel.Children.Insert(i, child);
            }
            else
            {
                panel.Children.Add(child);
            }
        }

        private static List<Part> Parts(Requirement req)
        {
            var result = new List<Part>();
            Func<string, Part> Text = x => new Part {
                Text = x,
            };
            Func<ArrayFileType, int, Part> DatRef = (x, idx) => new Part {
                Dat = x,
                ParamIndex = idx,
            };
            Func<int, Part> Uint16 = idx => new Part {
                Uint16 = true,
                ParamIndex = idx,
            };
            switch (req.Opcode)
            {
                case 0xff02:
                    result.Add(Text("Current unit is "));
                    result.Add(DatRef(ArrayFileType.Units, 0));
                    break;
                case 0xff03:
                    result.Add(Text("Has "));
                    result.Add(DatRef(ArrayFileType.Units, 0));
                    result.Add(Text(" (Accept incomplete)"));
                    break;
                case 0xff04:
                    result.Add(Text("Has addon "));
                    result.Add(DatRef(ArrayFileType.Units, 0));
                    result.Add(Text(" attached"));
                    break;
                case 0xff25:
                    result.Add(Text("Tech "));
                    result.Add(DatRef(ArrayFileType.TechData, 0));
                    result.Add(Text(" is researched"));
                    break;
                case 0xff40:
                    result.Add(Text("Has less than "));
                    result.Add(Uint16(1));
                    result.Add(Text(" "));
                    result.Add(DatRef(ArrayFileType.Units, 0));
                    result.Add(Text(" (Including incomplete)"));
                    break;
                case 0xffff:
                    result.Add(Text("-- End --"));
                    break;
                default:
                    if (DatRequirements.OpcodeNameDict.TryGetValue(req.Opcode, out string? val))
                    {
                        result.Add(Text(val));
                    }
                    else if (req.Opcode < 0xff00)
                    {
                        result.Add(Text("Has "));
                        result.Add(DatRef(ArrayFileType.Units, -1));
                    }
                    else
                    {
                        result.Add(Text($"Opcode {req.Opcode:x04}"));
                    }
                    break;
            }
            return result;
        }

        record Part
        {
            public string? Text;
            public ArrayFileType? Dat;
            public bool Uint16 = false;
            public int ParamIndex = -1;
        }

        List<Part> currentParts = new List<Part>();

        public static readonly DependencyProperty RequirementDataProperty = DependencyProperty.Register(
            "RequirementData",
            typeof(Requirement?),
            typeof(DatRequirementLine),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (o, args) => {
                    ((DatRequirementLine)o).DataUpdated(args);
                },
                (d, baseValue) => baseValue,
                false,
                UpdateSourceTrigger.PropertyChanged
            )
        );
        public Requirement? RequirementData
        {
            get { return (Requirement?)GetValue(RequirementDataProperty); }
            set
            {
                SetValue(RequirementDataProperty, value);
            }
        }

        StackPanel panel;
    }
}
