using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    [ConfigurationCollection(typeof(ColumnConfig),
    CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class ColumnLayoutConfig : ConfigurationElementCollection
    {

        public ColumnConfig this[int Index]
        {
            get
            {
                return base.BaseGet(Index) as ColumnConfig;
            }
            set
            {

                if (base.Count > Index && base.BaseGet(Index) != null)
                {
                    base.BaseRemoveAt(Index);
                }
                this.BaseAdd(Index, value);
            }
        }

        new public ColumnConfig this[string Key]
        {
            get
            {
                return base.BaseGet(Key) as ColumnConfig;
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new ColumnConfig();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as ColumnConfig).InfluxName;
        }
    }
}
