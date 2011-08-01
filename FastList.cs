using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace ZExtensions
{
    public class FastList<T> : IList<T>, IList
    {        
        private int count;

        private FastListCluster<T> first;
        private FastListCluster<T> last;

        private readonly List<Dictionary<int, FastListCluster<T>>> lookupTables =
            new List<Dictionary<int, FastListCluster<T>>>();

        private FastListCluster<T> lastUsedCluster;
        private int lastUsedClusterStartIndex;

        public IEnumerator<T> GetEnumerator()
        {
            var enumerator = new FastListEnumerator(first);
            return enumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            if (first == null)
            {
                first = new FastListCluster<T>();
                last = first;
            }
            if (!last.IsFull)
            {
                last.Add(item);
            }
            else
            {
                var newLast = new FastListCluster<T>();
                newLast.Previous = last;
                newLast.Add(item);
                last.Next= newLast;
                last = newLast;
            }            
            this.AddToTable(item, last);
            Interlocked.Increment(ref count);
            this.ClearCache();
        }


        private void AddToTable(T item, FastListCluster<T> cluster)
        {
            int hash = GetItemHash(item);            
            Dictionary<int, FastListCluster<T>> table = null;
            foreach (var t in this.lookupTables)
            {
                if (!t.ContainsKey(hash))
                {
                    table = t;
                    break;
                }
            }
            if (table == null)
            {
                table = new Dictionary<int, FastListCluster<T>>();
                this.lookupTables.Add(table);
            }
            table.Add(hash, cluster);
        }

        public int Add(object value)
        {
            Add((T) value);
            return Count;
        }

        public bool Contains(object value)
        {
            return Contains((T) value);
        }

        public void Clear()
        {
            this.lookupTables.Clear();
            first = null;
            last = null;
            this.ClearCache();
        }

        private void ClearCache()
        {
            this.lastUsedCluster = first;
            this.lastUsedClusterStartIndex = 0;
        }

        public int IndexOf(object value)
        {
            return IndexOf((T) value);
        }

        public void Insert(int index, object value)
        {
            Insert(index, (T)value);
        }

        public void Remove(object value)
        {
            Remove((T) value);
        }

        public bool Contains(T item)
        {            
            var cluster = this.GetItemCluster(item);
            return cluster != null;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            int currentIndex = arrayIndex;
            var current = first;
            while (current != null)
            {
                current.CopyTo(array, currentIndex);
                currentIndex += current.ItemsCount;
                current = current.Next;
            }
        }

        public bool Remove(T item)
        {
            bool removed = false;
            int hash = GetItemHash(item);
            var table = this.GetItemTable(hash, item);
            if (table != null)
            {
                var cluster = table[hash];
                removed = cluster.Remove(item);
                if (removed)
                {
                    if (cluster.ItemsCount == 0)
                    {
                        var next = cluster.Next;
                        var previous = cluster.Previous;
                        cluster.Previous = next;
                        cluster.Next = previous;
                    }
                    table.Remove(hash);
                    Interlocked.Decrement(ref count);
                }
            }
            this.ClearCache();
            return removed;            
        }

        public void CopyTo(Array array, int index)
        {
            this.CopyTo((T[])array, index);
        }

        public int Count
        {
            get { return count; }
        }

        public object SyncRoot
        {
            get { return this; }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool IsFixedSize
        {
            get { return false; }
        }

        public int IndexOf(T item)
        {
            var itemCluster = this.GetItemCluster(item);
            int index = -1;
            if (itemCluster != null)
            {
                index = 0;
                var currentCluster = first;
                while (currentCluster != itemCluster)
                {
                    index += currentCluster.ItemsCount;
                    currentCluster = currentCluster.Next;
                }
                index += itemCluster.IndexOf(item);
            }
            return index;
        }

        public void Insert(int index, T item)
        {
            if (index >= this.Count)
            {
                throw new ArgumentOutOfRangeException();
            }
            int clusterStartIndex;
            var cluster = this.GetClusterOfIndex(index, out clusterStartIndex);
            int clusterIndex = index - clusterStartIndex;
            if (!cluster.IsFull)
            {
                cluster.Insert(clusterIndex, item);
                this.AddToTable(item, cluster);
            }
            else
            {
                FastListCluster<T> left;
                FastListCluster<T> right;
                SplitCluster(cluster, clusterIndex, out left, out right);
                left.Add(item);
                var prev = cluster.Previous;
                var next = cluster.Next;
                if (prev != null)
                {
                    prev.Next = left;
                    left.Previous = prev;
                }
                if (next != null)
                {
                    next.Previous = right;
                }
                right.Next = next;
                if (cluster == first)
                {
                    if (first == last)
                    {
                        last = left;
                    }
                    first = left;
                }
                
                for (int i = 0; i < left.ItemsCount; i++)
                {
                    ReaddToTable(left[i], left);
                }
                for (int i = 0; i < right.ItemsCount; i++)
                {
                    ReaddToTable(right[i], right);
                }
            }
            Interlocked.Increment(ref count);
            this.ClearCache();
        }

        private void ReaddToTable(T item, FastListCluster<T> cluster)
        {
            int hash = GetItemHash(item);
            var table = this.GetItemTable(hash, item);
            if (table != null)
            {
                table[hash] = cluster;
            }
            else
            {
                this.AddToTable(item, cluster);
            }
        }

        private int GetItemHash(T item)
        {
            int hash = 0;
            if (item != null)
            {
                hash = item.GetHashCode();
            }
            return hash;
        }

        private void SplitCluster(FastListCluster<T> cluster, int splitIndex, out FastListCluster<T> left, out FastListCluster<T> right)
        {
            left = new FastListCluster<T>();
            right = new FastListCluster<T>();

            for (int i = 0; i < splitIndex; i++)
            {
                left.Add(cluster[i]);
            }
            for (int i = splitIndex; i < cluster.ItemsCount; i++)
            {
                right.Add(cluster[i]);
            }

            left.Next = right;
            right.Previous = left;
        }

        public void RemoveAt(int index)
        {
            int clusterStartIndex;
            var cluster = this.GetClusterOfIndex(index, out clusterStartIndex);
            int clusterIndex = index - clusterStartIndex;
            cluster.RemoveAt(clusterIndex);
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (T) value; }
        }

        public T this[int index]
        {
            get
            {
                if (index >= this.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }
                int clusterStartIndex;
                var cluster = this.GetClusterOfIndex(index, out clusterStartIndex);
                int clusterIndex = index - clusterStartIndex;
                var item = cluster[clusterIndex];                
                return item;
            }
            set
            {
                if (index >= this.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }
                int clusterStartIndex;
                var cluster = this.GetClusterOfIndex(index, out clusterStartIndex);
                int clusterIndex = index - clusterStartIndex;
                cluster[clusterIndex] = value;
            }
        }


        private FastListCluster<T> GetClusterOfIndex(int index, out int clusterStartIndex)
        {
            clusterStartIndex = 0;            
            int lastItemIndex = lastUsedClusterStartIndex + lastUsedCluster.ItemsCount - 1;

            while (lastUsedClusterStartIndex > index && lastItemIndex > index)
            {
                lastUsedCluster = lastUsedCluster.Previous;
                lastUsedClusterStartIndex = lastUsedClusterStartIndex - lastUsedCluster.ItemsCount;
                lastItemIndex = lastUsedClusterStartIndex + lastUsedCluster.ItemsCount - 1;
            }

            while (index > lastItemIndex)
            {
                lastUsedClusterStartIndex += lastUsedCluster.ItemsCount;
                lastUsedCluster = lastUsedCluster.Next;                
                lastItemIndex = lastUsedClusterStartIndex + lastUsedCluster.ItemsCount - 1;
            } 

           
            if (this.lastUsedClusterStartIndex < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            clusterStartIndex = lastUsedClusterStartIndex;
            
            return lastUsedCluster;
        }

        private FastListCluster<T> GetItemCluster(T item)
        {
            FastListCluster<T> cluster = null;
            int hash = GetItemHash(item);
        
            var table = this.GetItemTable(hash, item);
            if (table != null)
            {
                cluster = table[hash];
            }
                        
            return cluster;
        }

        private Dictionary<int, FastListCluster<T>> GetItemTable(int hash, T item)
        {                        
            Dictionary<int, FastListCluster<T>> result = null;
            foreach (var table in this.lookupTables)
            {
                if (table.ContainsKey(hash) && table[hash].Contains(item))
                {
                    result = table;
                    break;
                }
            }
            return result;
        }
        
        private class FastListEnumerator : IEnumerator<T>
        {
            private FastListCluster<T> root;
            private FastListCluster<T> current;
            private int currentIndex;

            public FastListEnumerator(FastListCluster<T> root)
            {
                this.root = root;
            }

            public void Dispose()
            {
                root = null;
                current = null;
            }

            public bool MoveNext()
            {
                if (current == null)
                {
                    current = root;
                    currentIndex = 0;
                }
                else
                {
                    currentIndex++;
                    if (currentIndex > current.ItemsCount - 1)
                    {
                        current = current.Next;
                        currentIndex = 0;
                    }
                }
                return current != null;
            }

            public void Reset()
            {
                this.current = null;
            }

            public T Current
            {
                get { return current[currentIndex]; }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }
        }

        private class FastListCluster<T>
        {
            private const int StorageSize = 20;
            private readonly T[] storage = new T[StorageSize];
            private int current = 0;

            internal void Add(T item)
            {
                if (IsFull)
                {
                    throw new InvalidOperationException("Storage is full");
                }
                storage[current++] = item;
            }

            internal bool Remove(T item)
            {
                bool removed = false;
                for (int i = 0; i < current; i++)
                {
                    if (storage[i].Equals(item))
                    {
                        this.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
                return removed;
            }

            public bool IsFull
            {
                get
                {
                    bool result = current >= StorageSize;
                    return result;
                }
            }

            public int ItemsCount
            {
                get { return current; }
            }


            public FastListCluster<T> Next { get; set; }

            public FastListCluster<T> Previous { get; set; }

            public int IndexOf(T item)
            {
                int index = Array.IndexOf(storage, item);                
                return index;
            }

            public T this[int index]
            {
                get
                {
                    if (index >= current)
                    {
                        throw new IndexOutOfRangeException();
                    }
                    return storage[index];
                }
                set
                {
                    if (index >= current)
                    {
                        throw new IndexOutOfRangeException();
                    }
                    storage[index] = value;
                }
            }

            public bool Contains(T item)
            {
                return this.IndexOf(item) > -1;
            }

            public void Insert(int index, T item)
            {
                for (int i = current - 1; i >= index; i--)
                {
                    storage[i + 1] = storage[i];
                }
                storage[index] = item;
                current++;
            }

            public void RemoveAt(int index)
            {
                for (int j = index; j < current - 1; j++)
                {
                    storage[j] = storage[j + 1];
                }
                current--;                
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                Array.Copy(storage, 0, array, arrayIndex, current);
            }
        }
    }    
}
