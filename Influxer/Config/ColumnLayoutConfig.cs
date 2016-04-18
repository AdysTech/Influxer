﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer.Config
{
    [ConfigurationCollection(typeof(ColumnConfig),AddItemName ="Column", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class ColumnLayoutConfig : ConfigurationElementCollection<ColumnConfig>
    {
    }
}
