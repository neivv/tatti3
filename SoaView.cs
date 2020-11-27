using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows;

namespace Tatti3
{
    // View to DatTable list entry (Soa as it's kind of struct of arrays; though
    // loosely typed so it's actually array of arrays)
    // Enumerating through this produces a list of values in aos layout.
    class SoaView : IList, IList<SoaStruct>, INotifyCollectionChanged
    {
        public SoaView()
        {
            arrays = new uint[][] {};
            structs = new SoaStruct?[] {};
        }

        SoaStruct GetStruct(int index)
        {
            var ret = structs[index];
            if (ret == null)
            {
                uint[] array = new uint[Arrays.Length];
                for (int i = 0; i < Arrays.Length; i++)
                {
                    array[i] = Arrays[i][index];
                }
                ret = new SoaStruct(this, index, array);
                structs[index] = ret;
            }
            return ret;
        }

        // Meant only be called from SoaStruct.
        // Would be nice to not have public but can't come up with a way that doesn't
        // require declaring boilerplate class/interface/something so a comment will do.
        // (Not feeling like moving SoaStruct inside this either)
        public void SetStruct(int index, uint[] array)
        {
            for (int i = 0; i < Arrays.Length; i++)
            {
                Arrays[i][index] = array[i];
            }
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset
            ));
        }

        public SoaStruct this[int index] { get => GetStruct(index); set => throw new NotImplementedException(); }
        object? IList.this[int index] { get => GetStruct(index); set => throw new NotImplementedException(); }
        SoaStruct IList<SoaStruct>.this[int index] { get => GetStruct(index); set => throw new NotImplementedException(); }

        uint[][] arrays;
        SoaStruct?[] structs;

        public uint[][] Arrays {
            get => arrays;
            set {
                arrays = value;
                structs = new SoaStruct?[this.Count];
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Reset
                ));
            }
        }

        public bool IsFixedSize => false;

        public bool IsReadOnly => false;

        public int Count => Arrays.Length == 0 ? 0 : Arrays[0].Length;

        public bool IsSynchronized => false;

        public object SyncRoot => throw new NotImplementedException();

        public int Add(object? value)
        {
            throw new NotImplementedException();
        }

        public void Add(SoaStruct item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(object? value)
        {
            if (value is SoaStruct val)
            {
                return Contains(val);
            }
            return false;
        }

        public bool Contains(SoaStruct item)
        {
            return structs.Any(x => ReferenceEquals(x, item));
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(SoaStruct[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            return new Enumerator(this, 0);
        }

        public int IndexOf(object? value)
        {
            if (value is SoaStruct val)
            {
                return IndexOf(val);
            }
            return -1;
        }

        public int IndexOf(SoaStruct item)
        {
            for (int i = 0; i < Count; i++)
            {
                if (structs[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public void Insert(int index, object? value)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, SoaStruct item)
        {
            throw new NotImplementedException();
        }

        public void Remove(object? value)
        {
            throw new NotImplementedException();
        }

        public bool Remove(SoaStruct item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator<SoaStruct> IEnumerable<SoaStruct>.GetEnumerator()
        {
            return new Enumerator(this, 0);
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        class Enumerator : IEnumerator, IEnumerator<SoaStruct>
        {
            public Enumerator(SoaView parent, int pos)
            {
                Parent = parent;
                Pos = pos - 1;
            }

            object IEnumerator.Current => Parent.GetStruct(Pos);
            SoaStruct IEnumerator<SoaStruct>.Current => Parent.GetStruct(Pos);
            public bool MoveNext()
            {
                Pos += 1;
                return Pos < Parent.Count;
            }
            public void Reset()
            {
                Pos = -1;
            }

            public void Dispose()
            {
            }

            SoaView Parent { get; set; }
            int Pos { get; set; }
        }
    }

    // Fields of a single entry in list
    class SoaStruct : INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public SoaStruct(SoaView parent, int parentIndex, uint[] array)
        {
            values = array;
            this.parent = parent;
            this.parentIndex = parentIndex;
        }

        uint[] values;
        public uint[] Values {
            get => values;
            set
            {
                values = value;
                parent.SetStruct(parentIndex, values);
                NotifyChanged();
            }
        }
        public uint this[int index]
        {
            get => Values[index];
            set
            {
                if (Values[index] != value)
                {
                    Values[index] = value;
                    parent.SetStruct(parentIndex, Values);
                    NotifyChanged();
                }
            }
        }

        void NotifyChanged()
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset
            ));
        }

        SoaView parent;
        int parentIndex;
    }
}
