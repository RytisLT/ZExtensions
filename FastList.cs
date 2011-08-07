using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace ZExtensions
{
    public class FastList<T> : IList<T>, IList
    {        
        private int count;

        private Cluster first;
        private Cluster last;

        private readonly List<Dictionary<int, Cluster>> lookupTables =
            new List<Dictionary<int, Cluster>>();

        private readonly Dictionary<Cluster, int> startIndexCache = new Dictionary<Cluster, int>();

        private readonly List<Rail> rails = new List<Rail>();
        private Rail lastUsedRail;

        public IEnumerator<T> GetEnumerator()
        {
            var enumerator = new Enumerator(first);
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
                first = new Cluster();
                last = first;
            }
            if (!last.IsFull)
            {
                last.Add(item);
            }
            else
            {
                var newLast = new Cluster();
                newLast.Previous = last;
                newLast.Add(item);
                last.Next= newLast;
                last = newLast;
            }            
            this.AddToTable(item, last);
            Interlocked.Increment(ref count);
            this.ClearCache();
        }


        private void AddToTable(T item, Cluster cluster)
        {
            int hash = GetItemHash(item);            
            Dictionary<int, Cluster> table = null;
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
                table = new Dictionary<int, Cluster>();
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
            rails.Clear();
            lastUsedRail = null;
            startIndexCache.Clear();
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
                        if (next != null)
                        {
                            next.Previous = previous;
                        }
                        if (previous != null)
                        {
                            previous.Next = next;
                        }
                        if (cluster == first)
                        {
                            first = next;
                        }
                        if (cluster == last)
                        {
                            if (previous != null)
                            {
                                previous.Next = null;
                            }
                            last = previous;
                        }
                        //cluster.Previous = next;
                        //cluster.Next = previous;
                    }
                    table.Remove(hash);
                    Interlocked.Decrement(ref count);
                }
            }
            this.ClearCache();
#if DEBUG
            this.SanityCheck();
#endif
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
                if (startIndexCache.ContainsKey(itemCluster))
                {
                    index = startIndexCache[itemCluster] + itemCluster.IndexOf(item);
                }
                else
                {
                    var startIndex  = 0;
                    var currentCluster = first;
                    while (currentCluster != itemCluster)
                    {
                        if (!startIndexCache.ContainsKey(currentCluster))
                        {
                            startIndexCache.Add(currentCluster, startIndex);
                        }
                        startIndex += currentCluster.ItemsCount;
                        currentCluster = currentCluster.Next;                        
                    }                    
                }
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
            if (index == clusterStartIndex && cluster.Previous != null && !cluster.Previous.IsFull)
            {
                cluster = cluster.Previous;
                clusterStartIndex -= cluster.ItemsCount;
            }
            int clusterIndex = index - clusterStartIndex;
            if (!cluster.IsFull)
            {
                cluster.Insert(clusterIndex, item);
                this.AddToTable(item, cluster);
            }
            else
            {
                Cluster left;
                Cluster right;
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
#if DEBUG
                this.SanityCheck();
#endif
            }
            Interlocked.Increment(ref count);
            this.ClearCache();
        }

#if DEBUG
        private void SanityCheck()
        {
            if (first != null && (first.Previous != null || last.Next != null || first.ItemsCount == 0) || 
                last != null && (last.ItemsCount == 0 || last.Next != null))
            {
                throw new InvalidOperationException();
            }
        }
#endif

        private void ReaddToTable(T item, Cluster cluster)
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

        private void SplitCluster(Cluster cluster, int splitIndex, out Cluster left, out Cluster right)
        {
            left = new Cluster();
            right = new Cluster();

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


        private Cluster GetClusterOfIndex(int index, out int clusterStartIndex)
        {
            Cluster cluster;
            if (index < first.ItemsCount)
            {
                clusterStartIndex = 0;
                cluster = first;
            }
            else if (index >= count - last.ItemsCount)
            {
                clusterStartIndex = count - last.ItemsCount;
                cluster = last;
            }
            else
            {
                var rail = this.GetFastestRail(index);
                rail.MoveToIndex(index);
                clusterStartIndex = rail.ClusterStartIndex;
                cluster = rail.Cluster;
            }            
            return cluster;           
        }

        private Rail GetFastestRail(int index)
        {
            var totalRails = (int) Math.Ceiling(((decimal) this.Count / 5000));
            if (rails.Count < totalRails)
            {
                rails.Add(new Rail(first, last, count));
            }
            if (lastUsedRail == null)
            {
                lastUsedRail = rails[0];
            }

            int minDistance = Math.Abs(lastUsedRail.ClusterStartIndex - index);
            Rail fastestRail = lastUsedRail;
            foreach (var rail in rails)
            {
                if (minDistance <= Cluster.StorageSize * 2)
                {
                    break;
                }
                int distance = Math.Abs(rail.ClusterStartIndex - index);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    fastestRail = rail;
                }
            }
            lastUsedRail = fastestRail;
            return fastestRail;
        }

        private Cluster GetItemCluster(T item)
        {
            Cluster cluster = null;
            int hash = GetItemHash(item);
        
            var table = this.GetItemTable(hash, item);
            if (table != null)
            {
                cluster = table[hash];
            }
                        
            return cluster;
        }

        private Dictionary<int, Cluster> GetItemTable(int hash, T item)
        {                        
            Dictionary<int, Cluster> result = null;
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

        private class Rail
        {
            private readonly Cluster first;
            private readonly Cluster last;
            private readonly int totalCount;

            public Rail(Cluster first, Cluster last, int totalCount): this(first, last, totalCount, first, 0)
            {                
            }

            public Rail(Cluster first, Cluster last, int totalCount, Cluster cluster, int clusterStartIndex)
            {
                if (cluster == null)
                {
                    throw new NullReferenceException("cluster");
                }
                this.first = first;
                this.last = last;
                this.totalCount = totalCount;
                this.Cluster = cluster;
                this.ClusterStartIndex = clusterStartIndex;
            }

            public int ClusterStartIndex { get; private set; }

            public Cluster Cluster { get; private set; }

            public void MoveToIndex(int index)
            {
                this.MoveToClosestPosition(index);
                
                var clusterStartIndex = ClusterStartIndex;
                var cluster = Cluster;
                int lastItemIndex = clusterStartIndex + cluster.ItemsCount - 1;
                while (clusterStartIndex > index && lastItemIndex > index)
                {
                    cluster = cluster.Previous;
                    clusterStartIndex = clusterStartIndex - cluster.ItemsCount;
                    lastItemIndex = clusterStartIndex + cluster.ItemsCount - 1;
                }

                while (index > lastItemIndex)
                {
                    clusterStartIndex += cluster.ItemsCount;
                    cluster = cluster.Next;
                    lastItemIndex = clusterStartIndex + cluster.ItemsCount - 1;
                }
                Cluster = cluster;
                ClusterStartIndex = clusterStartIndex;
            }

            private void MoveToClosestPosition(int index)
            {
                int distance = ClusterStartIndex - index;
                if (distance < 0)
                {
                    distance *= -1;
                }
                int endDistance = totalCount - index;
                if (index < distance && index < endDistance)
                {
                    Cluster = first;
                    ClusterStartIndex = 0;
                }
                else if (endDistance < distance && endDistance < index)
                {
                    Cluster = last;
                    ClusterStartIndex = totalCount - last.ItemsCount;
                }
            }
        }

        private class Enumerator : IEnumerator<T>
        {
            private Cluster root;
            private Cluster current;
            private int currentIndex;

            public Enumerator(Cluster root)
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

        private class Cluster
        {
            public const int StorageSize = 20;
            private readonly T[] storage = new T[StorageSize];
            private int current;

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
                int index = this.IndexOf(item);
                if (index > -1)
                {
                    this.RemoveAt(index);
                    removed = true;
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


            public Cluster Next { get; set; }

            public Cluster Previous { get; set; }

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
