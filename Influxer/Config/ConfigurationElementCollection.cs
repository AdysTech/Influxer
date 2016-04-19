using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    public class ConfigurationElementCollection<ElementType> : ConfigurationElementCollection, IList<ElementType> where ElementType : ConfigurationElement, IConfigurationElementCollectionElement, new()
    {
        #region ConfigurationElementCollection
        protected override ConfigurationElement CreateNewElement()
        {
            return new ElementType();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ElementType)element).GetKey();
        }
        #endregion

        #region IList<T>
        public ElementType this[int index]
        {
            get
            {
                return (ElementType)BaseGet(index);
            }
            set
            {
                if (base.Count > index && base.BaseGet(index) != null)
                {
                    BaseRemoveAt(index);
                }
                BaseAdd(index, value);
            }
        }

        bool ICollection<ElementType>.IsReadOnly
        {
            get { return IsReadOnly(); }
        }

        public void Add(ElementType item)
        {
            BaseAdd(item, true);
        }


        public void Clear()
        {
            BaseClear();
        }

        public bool Contains(ElementType item)
        {
            return !(IndexOf(item) < 0);
        }

        public void CopyTo(ElementType[] array, int arrayIndex)
        {
            base.CopyTo(array, arrayIndex);
        }

        public int IndexOf(ElementType item)
        {
            return BaseIndexOf(item);
        }

        public void Insert(int index, ElementType item)
        {
            BaseAdd(index, item);
        }

        public bool Remove(ElementType item)
        {
            BaseRemove(item);
            return true;
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }


        IEnumerator<ElementType> IEnumerable<ElementType>.GetEnumerator()
        {
            foreach (ElementType type in this)
            {
                yield return type;
            }
        }
        #endregion
    }
}
