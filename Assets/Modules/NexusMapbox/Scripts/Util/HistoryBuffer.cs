using System.Collections;
using System.Collections.Generic;

namespace Nexus.Util
{
    /// <summary>
    /// Fixed size buffer of entries that loops around overwriting old values once full
    /// </summary>
    public class HistoryBuffer<T> : IEnumerable<T>
    {
        private T[] array;
        private int currentIdx;

        public HistoryBuffer(int count)
        {
            array = new T[count];
        }

        public void Add(T item)
        {
            array[currentIdx++] = item;

            if (currentIdx == array.Length)
            {
                currentIdx = 0;
            }
        }

        public int Length => array.Length;

        public void Clear()
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = default;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)array).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return array.GetEnumerator();
        }
    }
}
