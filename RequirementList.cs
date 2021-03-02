using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Requirement = Tatti3.GameData.Requirement;

namespace Tatti3
{
    class RequirementList : IList, IList<RequirementList.RequirementWrap>, INotifyCollectionChanged
    {
        public class RequirementWrap : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            public RequirementWrap(Requirement val)
            {
                req = val;
            }

            public Requirement Value {
                get => req;
                set
                {
                    if (req != value)
                    {
                        req = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
                    }
                }
            }
            Requirement req;
            public UInt16 Opcode { get => Value.Opcode; }
            public UInt16[] Params { get => Value.Params; }
        }

        public RequirementList()
        {
        }

        public RequirementList(RequirementList other)
        {
            this.list = new List<RequirementWrap>(other.list);
        }

        public Requirement[] ToArray()
        {
            return this.list.Select(x => x.Value).ToArray();
        }

        public override string ToString()
        {
            var parts = String.Join(", ", list.Select(x => {
                if (x.Params.Length == 0) {
                    return $"{x.Opcode:x04}";
                } else {
                    var paramsStr = String.Join("-", x.Params.Select(x => $"{x:x}"));
                    return $"{x.Opcode:x04}:{paramsStr}";
                }
            }));
            return $"[{parts}]";
        }

        object? IList.this[int index] {
            get => ((IList<RequirementWrap>)this)[index];
            set
            {
                ((IList<RequirementWrap>)this)[index] = (RequirementWrap)value!;
            }
        }

        public RequirementWrap this[int index]
        {
            get => list[index];
            set
            {
                var oldItem = list[index];
                var old = list[index].Value;
                var newValue = value.Value;
                list[index] = value;
                value.PropertyChanged += (o, args) => {
                    Mutated?.Invoke(this, new EventArgs());
                };
                if (old.Equals(newValue))
                {
                    return;
                }
                var oldIsUpgrade = old.IsUpgradeLevelOpcode();
                var newIsUpgrade = newValue.IsUpgradeLevelOpcode();
                bool complex = false;
                // If we changed to a upgrade level item, add list End,
                // Similarly, if we changed away, remove list End if any followed.
                if (oldIsUpgrade != newIsUpgrade && oldIsUpgrade)
                {
                    complex = true;
                    // Remove new end
                    for (int i = index + 1; i < list.Count; i++)
                    {
                        if (list[i].Opcode == 0xffff)
                        {
                            list.RemoveAt(i);
                            break;
                        }
                    }
                }
                else if (oldIsUpgrade != newIsUpgrade && newIsUpgrade)
                {
                    complex = true;
                    // Add end. If there's an upgrade jump above this add it one before this,
                    // otherwise add one after this.
                    // Also don't add end to the end of list.
                    int index2 = -1;
                    for (int i = index - 1; i >= 0; i--)
                    {
                        if (list[i].Value.IsUpgradeLevelOpcode())
                        {
                            index2 = i;
                            break;
                        }
                    }
                    int insertPos = index2 == -1 ? index + 1 : index;
                    if (insertPos != list.Count)
                    {
                        var end = new Requirement(0xffff);
                        list.Insert(insertPos, new RequirementWrap(end));
                    }
                }
                if (complex)
                {
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Reset
                    ));
                }
                else
                {
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Replace,
                        value,
                        oldItem,
                        index
                    ));
                }
                Mutated?.Invoke(this, new EventArgs());
            }
        }

        public void Insert(int index, RequirementWrap item)
        {
            var req = item.Value;
            // Not allowing inserting End
            if (req.IsEnd())
            {
                return;
            }
            list.Insert(index, item);
            item.PropertyChanged += (o, args) => {
                Mutated?.Invoke(this, new EventArgs());
            };
            if (req.IsUpgradeLevelOpcode())
            {
                var end = new Requirement(0xffff);
                list.Insert(index + 1, new RequirementWrap(end));
            }
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset
            ));
            Mutated?.Invoke(this, new EventArgs());
        }

        public void RemoveAt(int index)
        {
            var req = this[index].Value;
            // Not allowing removing End
            if (req.IsEnd())
            {
                return;
            }
            // If removing upgrade jump, remove the matching end as well
            list.RemoveAt(index);
            if (req.IsUpgradeLevelOpcode())
            {
                for (int i = index; i < list.Count; i++)
                {
                    if (list[i].Value.IsEnd())
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset
            ));
            Mutated?.Invoke(this, new EventArgs());
        }

        public void Swap(int a, int b)
        {
            var val1 = list[a];
            var val2 = list[b];
            list[a] = val2;
            list[b] = val1;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset
            ));
            Mutated?.Invoke(this, new EventArgs());
        }

        public void Rebuild(Action<Action<Requirement>> callback)
        {
            list.Clear();
            callback(x => list.Add(new RequirementWrap(x)));
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset
            ));
            foreach (RequirementWrap x in list)
            {
                x.PropertyChanged += (o, args) => {
                    Mutated?.Invoke(this, new EventArgs());
                };
            }
        }

        public int Add(object? value)
        {
            return ((IList)list).Add(value);
        }

        public void Clear()
        {
            ((IList)list).Clear();
        }

        public bool Contains(object? value)
        {
            return ((IList)list).Contains(value);
        }

        public int IndexOf(object? value)
        {
            return ((IList)list).IndexOf(value);
        }

        public void Insert(int index, object? value)
        {
            ((IList<RequirementWrap>)this).Insert(index, (RequirementWrap)value!);
        }

        public void Remove(object? value)
        {
            ((IList)list).Remove(value);
        }

        public void CopyTo(Array array, int index)
        {
            ((ICollection)list).CopyTo(array, index);
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)list).GetEnumerator();
        }

        public int IndexOf(RequirementWrap item)
        {
            return ((IList<RequirementWrap>)list).IndexOf(item);
        }

        public void Add(RequirementWrap item)
        {
            ((ICollection<RequirementWrap>)list).Add(item);
        }

        public bool Contains(RequirementWrap item)
        {
            return ((ICollection<RequirementWrap>)list).Contains(item);
        }

        public void CopyTo(RequirementWrap[] array, int arrayIndex)
        {
            ((ICollection<RequirementWrap>)list).CopyTo(array, arrayIndex);
        }

        public bool Remove(RequirementWrap item)
        {
            return ((ICollection<RequirementWrap>)list).Remove(item);
        }

        IEnumerator<RequirementWrap> IEnumerable<RequirementWrap>.GetEnumerator()
        {
            return ((IEnumerable<RequirementWrap>)list).GetEnumerator();
        }

        List<RequirementWrap> list = new List<RequirementWrap>();

        public bool IsFixedSize => ((IList)list).IsFixedSize;

        public bool IsReadOnly => ((IList)list).IsReadOnly;

        public int Count => ((ICollection)list).Count;

        public bool IsSynchronized => ((ICollection)list).IsSynchronized;

        public object SyncRoot => ((ICollection)list).SyncRoot;


        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public event EventHandler<EventArgs>? Mutated;
    }
}
