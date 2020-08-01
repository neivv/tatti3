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

        uint offsetField = 0;
        uint dataField = 0;

        public static List<(UInt16, string)> OpcodeNames { get; }
        public static Dictionary<UInt16, string> OpcodeNameDict { get; }

        static DatRequirements()
        {
            OpcodeNames = new List<(UInt16, string)>();
            OpcodeNameDict = new Dictionary<UInt16, string>();

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
            OpcodeNames.Add((0xff1d, "Can rally or rightclick"));
            OpcodeNames.Add((0xff1e, "*Allow on hallucinations*"));
            OpcodeNames.Add((0xff1f, "*Upgrade level 1 conditions*"));
            OpcodeNames.Add((0xff20, "*Upgrade level 2 conditions*"));
            OpcodeNames.Add((0xff21, "*Upgrade level 3+ conditions*"));
            OpcodeNames.Add((0xff22, "Always disabled"));
            OpcodeNames.Add((0xff23, "Always blank"));
            OpcodeNames.Add((0xff24, "Is Brood War"));
            OpcodeNames.Add((0xff25, "Tech [...] is researched"));
            OpcodeNames.Add((0xff26, "Is burrowed"));
            foreach ((var op, var text) in OpcodeNames)
            {
                OpcodeNameDict[op] = text;
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
            panel.Children.Clear();
            if (RequirementData == null)
            {
                return;
            }
            Requirement data = (Requirement)RequirementData;
            foreach (var part in Parts(data))
            {
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
                    panel.Children.Add(text);
                }
                if (part.Dat != null)
                {
                    var dropdown = new ComboBox();
                    panel.Children.Add(dropdown);

                    var namesPath = $"DataContext.Root.Dat[{part.Dat}].Names";
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
                    BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, binding);
                    dropdown.SelectedIndex = data.Opcode < 0xff00 ? data.Opcode : data.Param;
                }
            }
        }

        private static List<Part> Parts(Requirement req)
        {
            var result = new List<Part>();
            Func<string, Part> Text = x => new Part {
                Text = x,
            };
            Func<ArrayFileType, Part> DatRef = x => new Part {
                Dat = x,
            };
            switch (req.Opcode)
            {
                case 0xff02:
                    result.Add(Text("Current unit is "));
                    result.Add(DatRef(ArrayFileType.Units));
                    break;
                case 0xff03:
                    result.Add(Text("Has "));
                    result.Add(DatRef(ArrayFileType.Units));
                    result.Add(Text(" (Accept incomplete)"));
                    break;
                case 0xff04:
                    result.Add(Text("Has addon "));
                    result.Add(DatRef(ArrayFileType.Units));
                    result.Add(Text(" attached"));
                    break;
                case 0xff25:
                    result.Add(Text("Tech "));
                    result.Add(DatRef(ArrayFileType.TechData));
                    result.Add(Text(" is researched"));
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
                        result.Add(DatRef(ArrayFileType.Units));
                    }
                    else
                    {
                        result.Add(Text($"Opcode {req.Opcode:x04}"));
                    }
                    break;
            }
            return result;
        }

        struct Part
        {
            public string? Text;
            public ArrayFileType? Dat;
        }

        public static readonly DependencyProperty RequirementDataProperty = DependencyProperty.Register(
            "RequirementData",
            typeof(Requirement?),
            typeof(DatRequirementLine),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (o, args) => {
                    ((DatRequirementLine)o).DataUpdated(args);
                }
            )
        );
        public Requirement? RequirementData
        {
            get { return (Requirement?)GetValue(RequirementDataProperty); }
            set { SetValue(RequirementDataProperty, value); }
        }

        StackPanel panel;
    }
}
